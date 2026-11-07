using System.Runtime.InteropServices;

namespace WindowsGuardianWatchdog.Core.Cleanup
{
    /// <summary>
    /// The Recycle Bin, through the same Windows calls Explorer uses. The old version counted every account's bin
    /// (the app runs as administrator) and "emptied" it with a hidden PowerShell that could stop on a confirmation
    /// nobody saw.
    /// </summary>
    public class RecycleBinCleaner
    {
        public const string CategoryName = "Recycle Bin";

        [StructLayout(LayoutKind.Sequential)] // 64-bit layout; the app is x64 only.
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x1, SHERB_NOPROGRESSUI = 0x2, SHERB_NOSOUND = 0x4;

        public static (long Bytes, long Items) Query()
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            var hr = SHQueryRecycleBin(null, ref info);
            return hr == 0 ? (info.i64Size, info.i64NumItems) : (0, 0);
        }

        public CleanupResult Scan()
        {
            var result = new CleanupResult { Category = CategoryName };
            var (bytes, items) = Query();
            if (items > 0)
            {
                result.Items.Add(new CleanupItem
                {
                    Path = "Recycle Bin",
                    Category = CategoryName,
                    Size = bytes,
                    LastModified = DateTime.Now,
                    Type = CleanupItemType.Directory,
                    Description = $"{items} deleted items"
                });
            }
            result.TotalSize = bytes;
            return result;
        }

        public CleanupOutcome Execute(CleanupResult result)
        {
            var (bytesBefore, itemsBefore) = Query();
            var hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            var (bytesAfter, itemsAfter) = Query();
            // S_OK, or E_UNEXPECTED when it was already empty.
            return new CleanupOutcome
            {
                FreedBytes = Math.Max(0, bytesBefore - bytesAfter),
                Removed = (int)Math.Max(0, itemsBefore - itemsAfter),
                Skipped = hr == 0 || itemsAfter == 0 ? 0 : (int)itemsAfter
            };
        }
    }
}
