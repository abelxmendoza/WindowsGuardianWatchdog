using System.Text.Json;

namespace WindowsGuardianWatchdog.Core.Hardening
{
    public static class HardeningJournal
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsGuardianWatchdog",
            "hardening-journal.json");

        public static IReadOnlyList<string> Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return Array.Empty<string>();
                return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) ?? new List<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static void Remember(string id)
        {
            var items = Read().ToList();
            if (!items.Contains(id, StringComparer.OrdinalIgnoreCase)) items.Add(id);
            Save(items);
        }

        public static void Forget(string id)
        {
            Save(Read().Where(item => !item.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private static void Save(List<string> items)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(items));
        }
    }
}
