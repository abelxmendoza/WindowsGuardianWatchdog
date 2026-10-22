using System.Security.Cryptography;
using System.Text.Json;
using WindowsGuardianWatchdog.Core.Incidents;
using WindowsGuardianWatchdog.Core.Storage;

namespace WindowsGuardianWatchdog.Core.Quarantine
{
    /// <summary>Something the person can understand and act on.</summary>
    public sealed class QuarantineException : Exception
    {
        public QuarantineException(string message) : base(message) { }
    }

    public sealed class QuarantineEntry
    {
        public string Id { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime QuarantinedAt { get; set; }
        public string Reason { get; set; } = string.Empty;

        public string FileName => Path.GetFileName(OriginalPath);
        public string SizeText => Size < 1024 * 1024 ? $"{Size / 1024.0:0.#} KB" : $"{Size / (1024.0 * 1024):0.#} MB";
        public string WhenText => QuarantinedAt.ToString("g");
    }

    /// <summary>
    /// Reversible quarantine, as in the Mac and Linux apps. Nothing is deleted unless you ask for exactly that.
    ///
    ///   quarantine  copied into a private folder, its hash checked, renamed so it can't be run, made read-only, and only
    ///               then removed from where it was. Origin, hash and reason are kept so it can go back
    ///   restore     back where it came from, never over something that is there now
    ///   delete      the only irreversible step, and only for something already in quarantine
    ///
    /// Windows' own files are refused: repair those with "sfc /scannow" instead.
    /// </summary>
    public static class QuarantineVault
    {
        private const long MaxBytes = 4L * 1024 * 1024 * 1024;
        private const string StoredExtension = ".quarantined";
        private static readonly object Gate = new();
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        public static string VaultDirectory => Path.Combine(GuardianPaths.DataDirectory, "Quarantine");

        public static IReadOnlyList<QuarantineEntry> List()
        {
            lock (Gate)
            {
                if (!Directory.Exists(VaultDirectory)) return Array.Empty<QuarantineEntry>();
                var entries = new List<QuarantineEntry>();
                foreach (var folder in Directory.EnumerateDirectories(VaultDirectory))
                {
                    var entry = ReadManifest(folder);
                    if (entry != null) entries.Add(entry);
                }
                return entries.OrderByDescending(e => e.QuarantinedAt).ToList();
            }
        }

        public static QuarantineEntry QuarantineFile(string path, string reason)
        {
            lock (Gate)
            {
                path = Path.GetFullPath(path);
                var file = new FileInfo(path);
                if (!file.Exists) throw new QuarantineException("That file is not there any more.");
                if (IsUnder(path, Environment.GetFolderPath(Environment.SpecialFolder.Windows)))
                    throw new QuarantineException("This is part of Windows. Moving it could stop the PC from starting. Run \"sfc /scannow\" as administrator to repair Windows files instead.");
                if (IsUnder(path, VaultDirectory)) throw new QuarantineException("That file is already in quarantine.");
                if (file.Length > MaxBytes) throw new QuarantineException("That file is larger than 4 GB, which is too big to quarantine safely.");

                var id = Guid.NewGuid().ToString("N")[..10];
                var folder = Path.Combine(VaultDirectory, id);
                var stored = Path.Combine(folder, id + StoredExtension);
                Directory.CreateDirectory(folder);

                try
                {
                    var hash = HashFile(path);
                    System.IO.File.Copy(path, stored);
                    if (HashFile(stored) != hash) throw new QuarantineException("The copy did not match the original, so nothing was moved.");
                    System.IO.File.SetAttributes(stored, FileAttributes.ReadOnly);

                    var entry = new QuarantineEntry
                    {
                        Id = id,
                        OriginalPath = path,
                        Sha256 = hash,
                        Size = file.Length,
                        QuarantinedAt = DateTime.Now,
                        Reason = string.IsNullOrWhiteSpace(reason) ? "Moved here by you" : reason
                    };
                    System.IO.File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(entry, Json));

                    var attributes = System.IO.File.GetAttributes(path);
                    try
                    {
                        if (attributes.HasFlag(FileAttributes.ReadOnly)) System.IO.File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                        System.IO.File.Delete(path);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Leave the original exactly as it was.
                        try { if (System.IO.File.Exists(path)) System.IO.File.SetAttributes(path, attributes); } catch (Exception) { }
                        throw new QuarantineException("The file is in use or protected. End the program that uses it (see \"What is running\") and try again.");
                    }

                    EventTimeline.Record(NoticeKind.Information, NoticeSeverity.Info, "Quarantine", $"Quarantined {entry.FileName}",
                        $"Moved from {path}. Reason: {entry.Reason}", "Restore it from the Quarantine page if it turns out to be safe.");
                    return entry;
                }
                catch
                {
                    RemoveFolder(folder);
                    throw;
                }
            }
        }

        public static void Restore(string id)
        {
            lock (Gate)
            {
                var (folder, entry) = Find(id);
                var stored = Path.Combine(folder, id + StoredExtension);
                if (System.IO.File.Exists(entry.OriginalPath))
                    throw new QuarantineException($"Something else is now at {entry.OriginalPath}. Move or rename it first, then restore.");
                if (HashFile(stored) != entry.Sha256)
                    throw new QuarantineException("The quarantined copy no longer matches what was put away, so it was not restored.");

                Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
                System.IO.File.Copy(stored, entry.OriginalPath);
                System.IO.File.SetAttributes(entry.OriginalPath, FileAttributes.Normal);
                RemoveFolder(folder);

                EventTimeline.Record(NoticeKind.Information, NoticeSeverity.Info, "Quarantine", $"Restored {entry.FileName}",
                    $"Put back at {entry.OriginalPath}.");
            }
        }

        public static void Delete(string id)
        {
            lock (Gate)
            {
                var (folder, entry) = Find(id);
                RemoveFolder(folder);
                EventTimeline.Record(NoticeKind.Information, NoticeSeverity.Info, "Quarantine", $"Deleted {entry.FileName} for good",
                    $"It came from {entry.OriginalPath}.");
            }
        }

        private static (string Folder, QuarantineEntry Entry) Find(string id)
        {
            if (id.Length != 10 || !id.All(Uri.IsHexDigit)) throw new QuarantineException("That quarantine item is not valid.");
            var folder = Path.Combine(VaultDirectory, id);
            var entry = ReadManifest(folder) ?? throw new QuarantineException("That item is no longer in quarantine.");
            return (folder, entry);
        }

        private static QuarantineEntry? ReadManifest(string folder)
        {
            try
            {
                var manifest = Path.Combine(folder, "manifest.json");
                return System.IO.File.Exists(manifest) ? JsonSerializer.Deserialize<QuarantineEntry>(System.IO.File.ReadAllText(manifest)) : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Quarantine manifest unreadable in {folder}: {ex.Message}");
                return null;
            }
        }

        private static void RemoveFolder(string folder)
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.EnumerateFiles(folder)) System.IO.File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(folder, recursive: true);
        }

        private static bool IsUnder(string path, string folder) =>
            !string.IsNullOrEmpty(folder) &&
            path.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static string HashFile(string path)
        {
            using var stream = System.IO.File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
