using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsGuardianWatchdog.Core.Storage;

namespace WindowsGuardianWatchdog.Core.Incidents
{
    /// <summary>
    /// Same three kinds the Linux app uses: an incident (something may have happened), a finding (a standing
    /// weakness) or information. Only incidents and findings count toward the unread badge.
    /// </summary>
    public enum NoticeKind { Incident, Finding, Information }

    public enum NoticeSeverity { Info, Warning, Critical }

    public sealed class Notice
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public DateTime FirstSeen { get; set; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public int Count { get; set; } = 1;
        public NoticeKind Kind { get; set; }
        public NoticeSeverity Severity { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Advice { get; set; } = string.Empty;
        public bool Seen { get; set; }

        [JsonIgnore] public string SeverityText => Severity switch
        {
            NoticeSeverity.Critical => "Act now",
            NoticeSeverity.Warning => "Worth a look",
            _ => "For your information"
        };

        [JsonIgnore] public string WhenText => Count > 1
            ? $"{LastSeen:g} (seen {Count} times since {FirstSeen:g})"
            : $"{LastSeen:g}";

        [JsonIgnore] public bool NeedsAttention => !Seen && Kind != NoticeKind.Information;
    }

    /// <summary>
    /// The app's memory of what it noticed, kept as one small JSON file. Repeats of an unread notice are folded into
    /// it instead of piling up, as in the Linux timeline.
    /// </summary>
    public static class EventTimeline
    {
        private const int MaxNotices = 500;
        private static readonly object Gate = new();
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

        private static string FilePath => GuardianPaths.File("notices.json");

        public static event EventHandler? Changed;

        public static Notice Record(NoticeKind kind, NoticeSeverity severity, string source, string title, string detail, string advice = "")
        {
            Notice notice;
            lock (Gate)
            {
                var notices = Load();
                var repeat = notices.FirstOrDefault(n => !n.Seen && n.Source == source && n.Title == title && n.Detail == detail
                                                         && DateTime.Now - n.LastSeen < TimeSpan.FromDays(1));
                if (repeat != null)
                {
                    repeat.Count++;
                    repeat.LastSeen = DateTime.Now;
                    notice = repeat;
                }
                else
                {
                    notice = new Notice { Kind = kind, Severity = severity, Source = source, Title = title, Detail = detail, Advice = advice };
                    notices.Insert(0, notice);
                }
                Save(notices);
            }
            Changed?.Invoke(null, EventArgs.Empty);
            return notice;
        }

        public static IReadOnlyList<Notice> ReadAll()
        {
            lock (Gate) return Load();
        }

        public static int UnreadCount()
        {
            lock (Gate) return Load().Count(n => n.NeedsAttention);
        }

        public static void MarkSeen(string id) => Update(n => { if (n.Id == id) n.Seen = true; });

        public static void MarkAllSeen() => Update(n => n.Seen = true);

        public static void ClearRead()
        {
            lock (Gate) Save(Load().Where(n => !n.Seen).ToList());
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void Update(Action<Notice> change)
        {
            lock (Gate)
            {
                var notices = Load();
                notices.ForEach(change);
                Save(notices);
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static List<Notice> Load()
        {
            try
            {
                if (!System.IO.File.Exists(FilePath)) return new List<Notice>();
                return JsonSerializer.Deserialize<List<Notice>>(System.IO.File.ReadAllText(FilePath), Json) ?? new List<Notice>();
            }
            catch (Exception ex)
            {
                // A damaged file must not take the app down; the App health page reports it and can restore a backup.
                Debug.WriteLine($"Notices could not be read: {ex.Message}");
                return new List<Notice>();
            }
        }

        private static void Save(List<Notice> notices)
        {
            try
            {
                GuardianPaths.WriteAtomically(FilePath, JsonSerializer.Serialize(notices.Take(MaxNotices).ToList(), Json));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Notices could not be saved: {ex.Message}");
            }
        }
    }
}
