namespace WindowsGuardianWatchdog.Core.Files
{
    public sealed class FileHit
    {
        public string Name { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string SizeText { get; init; } = string.Empty;
        public string Modified { get; init; } = string.Empty;
        public string Detail => $"{SizeText} · {Modified}";
    }

    public static class FileSearcher
    {
        public const int ResultLimit = 400;

        public static Task<IReadOnlyList<FileHit>> SearchAsync(string query, bool allDrives, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var hits = new List<FileHit>();
                var comparison = query.Trim();
                if (comparison.Length < 2) return (IReadOnlyList<FileHit>)hits;

                var roots = allDrives
                    ? DriveInfo.GetDrives()
                        .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
                        .Select(drive => drive.RootDirectory.FullName)
                    : new[] { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };

                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    ReturnSpecialDirectories = false
                };

                foreach (var root in roots)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(root);
                    IEnumerable<string> files;
                    try
                    {
                        files = Directory.EnumerateFiles(root, "*", options);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Path.GetFileName(file).Contains(comparison, StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            var info = new FileInfo(file);
                            hits.Add(new FileHit
                            {
                                Name = info.Name,
                                Path = info.FullName,
                                SizeText = FormatSize(info.Length),
                                Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                        catch
                        {
                            continue;
                        }

                        if (hits.Count >= ResultLimit) return (IReadOnlyList<FileHit>)hits;
                    }
                }

                return hits;
            }, cancellationToken);
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:0.#} {units[unit]}";
        }
    }
}
