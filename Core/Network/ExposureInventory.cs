using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WindowsGuardianWatchdog.Core.Network
{
    public enum ExposureReach { ThisPcOnly, OneNetwork, AnyNetwork }

    public sealed class ExposedService
    {
        public string Protocol { get; init; } = "TCP";
        public string Address { get; init; } = string.Empty;
        public int Port { get; init; }
        public int ProcessId { get; init; }
        public string ProcessName { get; init; } = string.Empty;
        public ExposureReach Reach { get; init; }
        public string KnownAs { get; init; } = string.Empty;
        public string Advice { get; init; } = string.Empty;
        public bool IsRisky { get; init; }

        public string Title => string.IsNullOrEmpty(KnownAs) ? $"{ProcessName} on {Protocol} port {Port}" : $"{KnownAs} ({ProcessName}, {Protocol} port {Port})";
        public string ReachText => Reach switch
        {
            ExposureReach.ThisPcOnly => "Only programs on this PC can connect",
            ExposureReach.OneNetwork => $"Devices on the network at {Address} can try to connect",
            _ => "Any device on any network this PC joins can try to connect"
        };
    }

    public sealed class ExposureReport
    {
        public List<ExposedService> Services { get; init; } = new();
        public Dictionary<string, bool?> FirewallProfiles { get; init; } = new();
        public DateTime TakenAt { get; init; } = DateTime.Now;
        public int ReachableCount => Services.Count(s => s.Reach != ExposureReach.ThisPcOnly);
        public int RiskyCount => Services.Count(s => s.IsRisky);
    }

    /// <summary>
    /// What is listening on this PC, who can reach it, and how worried to be. Ported from the Linux app's
    /// exposure_inventory.py. DETECTION ONLY: nothing is closed, stopped or changed.
    ///
    /// Honest limit: this shows what is willing to answer. Windows Firewall may still block a port; per-port firewall
    /// rules aren't judged here, only whether the firewall is on for each network profile.
    /// </summary>
    public static class ExposureInventory
    {
        private static readonly Dictionary<int, (string Name, string Advice)> RiskyPorts = new()
        {
            [21] = ("FTP file sharing", "FTP sends passwords unencrypted. Turn it off unless you run a file server on purpose."),
            [22] = ("SSH remote shell", "Fine if you set up OpenSSH Server on purpose; otherwise remove it in Settings > System > Optional features."),
            [23] = ("Telnet", "Telnet is unencrypted remote control. Turn it off."),
            [135] = ("Windows RPC", "Normal on Windows. Make sure the firewall is on for public networks."),
            [139] = ("Windows file sharing (NetBIOS)", "Only needed for very old devices. Keep the firewall on for public networks."),
            [445] = ("Windows file sharing (SMB)", "If you don't share folders, turn off File and printer sharing in Advanced sharing settings."),
            [1433] = ("SQL Server database", "Databases should normally listen only on this PC unless something else needs them."),
            [3306] = ("MySQL database", "Databases should normally listen only on this PC unless something else needs them."),
            [3389] = ("Remote Desktop", "Remote Desktop is a common break-in route. Turn it off in Settings > System > Remote Desktop if you don't use it."),
            [5432] = ("PostgreSQL database", "Databases should normally listen only on this PC unless something else needs them."),
            [5900] = ("VNC remote control", "VNC is remote control of this PC. Remove it if you didn't install it on purpose."),
            [5985] = ("Windows Remote Management", "Lets other PCs run commands here. Turn off WinRM if you don't manage this PC remotely."),
            [5986] = ("Windows Remote Management (HTTPS)", "Lets other PCs run commands here. Turn off WinRM if you don't manage this PC remotely."),
            [6379] = ("Redis database", "Redis has no password by default. Bind it to this PC only."),
            [27017] = ("MongoDB database", "Databases should normally listen only on this PC unless something else needs them."),
        };

        private static readonly Dictionary<int, string> FamiliarPorts = new()
        {
            [80] = "Web server", [443] = "Secure web server", [5353] = "Local device discovery (mDNS)",
            [5355] = "Name lookup on the local network (LLMNR)", [1900] = "Device discovery (UPnP)",
            [137] = "Windows name lookup (NetBIOS)", [138] = "Windows network browsing (NetBIOS)",
            [7680] = "Delivery Optimization (Windows Update sharing)", [5040] = "Windows Connected Devices"
        };

        public static Task<ExposureReport> TakeAsync() => Task.Run(() => Take());

        public static ExposureReport Take()
        {
            var names = new Dictionary<int, string>();
            string NameOf(int pid)
            {
                if (names.TryGetValue(pid, out var known)) return known;
                string name;
                try { name = pid == 0 ? "System Idle" : pid == 4 ? "Windows (System)" : Process.GetProcessById(pid).ProcessName; }
                catch { name = "a program that has ended"; }
                return names[pid] = name;
            }

            var services = new List<ExposedService>();
            foreach (var (protocol, address, port, pid) in ReadTcpListeners().Concat(ReadUdpListeners()))
            {
                services.Add(Describe(protocol, address, port, pid, NameOf(pid)));
            }

            return new ExposureReport
            {
                Services = services
                    .GroupBy(s => (s.Protocol, s.Port, s.ProcessId, s.Reach))
                    .Select(g => g.First())
                    .OrderByDescending(s => s.IsRisky)
                    .ThenByDescending(s => s.Reach)
                    .ThenBy(s => s.Port)
                    .ToList(),
                FirewallProfiles = ReadFirewallProfiles()
            };
        }

        private static ExposedService Describe(string protocol, IPAddress address, int port, int pid, string processName)
        {
            var reach = IPAddress.IsLoopback(address) ? ExposureReach.ThisPcOnly
                : address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ? ExposureReach.AnyNetwork
                : ExposureReach.OneNetwork;

            var knownAs = string.Empty;
            var advice = reach == ExposureReach.ThisPcOnly
                ? "Nothing to do: other devices can't reach it."
                : "If you don't know why this program accepts connections, look it up before allowing it through the firewall.";
            var risky = false;
            if (RiskyPorts.TryGetValue(port, out var info))
            {
                knownAs = info.Name;
                if (reach != ExposureReach.ThisPcOnly) { advice = info.Advice; risky = true; }
            }
            else if (FamiliarPorts.TryGetValue(port, out var familiar))
            {
                knownAs = familiar;
                if (reach != ExposureReach.ThisPcOnly) advice = "A normal Windows or network feature.";
            }
            else if (protocol == "UDP" && port >= 49152 && reach != ExposureReach.ThisPcOnly)
            {
                // Usually a program's temporary reply channel (browsers, calls, games), but a service can sit here too.
                knownAs = "Temporary port";
                advice = "Usually a program's short-lived reply channel. Look it up only if you don't recognize the program.";
            }

            return new ExposedService
            {
                Protocol = protocol, Address = address.ToString(), Port = port, ProcessId = pid, ProcessName = processName,
                Reach = reach, KnownAs = knownAs, Advice = advice, IsRisky = risky
            };
        }

        public static Dictionary<string, bool?> ReadFirewallProfiles()
        {
            var result = new Dictionary<string, bool?>();
            const string root = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\";
            foreach (var (profile, label) in new[] { ("DomainProfile", "Work"), ("StandardProfile", "Private"), ("PublicProfile", "Public") })
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(root + profile);
                    result[label] = key?.GetValue("EnableFirewall") is int enabled ? enabled == 1 : null;
                }
                catch
                {
                    result[label] = null;
                }
            }
            return result;
        }

        // --- iphlpapi: the only way to learn which program owns a listening port without administrator rights ---

        private const int AfInet = 2;
        private const int AfInet6 = 23;
        private const int TcpTableOwnerPidListener = 3;
        private const int UdpTableOwnerPid = 1;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);

        private static IEnumerable<(string, IPAddress, int, int)> ReadTcpListeners()
        {
            // MIB_TCPROW_OWNER_PID: state, localAddr, localPort, remoteAddr, remotePort, pid
            foreach (var row in ReadTable((IntPtr t, ref int s) => GetExtendedTcpTable(t, ref s, true, AfInet, TcpTableOwnerPidListener, 0), 24))
                yield return ("TCP", new IPAddress(BitConverter.GetBytes(row.ReadInt(4))), Port(row.ReadInt(8)), row.ReadInt(20));
            // MIB_TCP6ROW_OWNER_PID: localAddr[16], scope, localPort, remoteAddr[16], scope, remotePort, state, pid
            foreach (var row in ReadTable((IntPtr t, ref int s) => GetExtendedTcpTable(t, ref s, true, AfInet6, TcpTableOwnerPidListener, 0), 56))
                yield return ("TCP", new IPAddress(row.ReadBytes(0, 16)), Port(row.ReadInt(20)), row.ReadInt(52));
        }

        private static IEnumerable<(string, IPAddress, int, int)> ReadUdpListeners()
        {
            // MIB_UDPROW_OWNER_PID: localAddr, localPort, pid
            foreach (var row in ReadTable((IntPtr t, ref int s) => GetExtendedUdpTable(t, ref s, true, AfInet, UdpTableOwnerPid, 0), 12))
                yield return ("UDP", new IPAddress(BitConverter.GetBytes(row.ReadInt(0))), Port(row.ReadInt(4)), row.ReadInt(8));
            // MIB_UDP6ROW_OWNER_PID: localAddr[16], scope, localPort, pid
            foreach (var row in ReadTable((IntPtr t, ref int s) => GetExtendedUdpTable(t, ref s, true, AfInet6, UdpTableOwnerPid, 0), 28))
                yield return ("UDP", new IPAddress(row.ReadBytes(0, 16)), Port(row.ReadInt(20)), row.ReadInt(24));
        }

        private static int Port(int raw) => ((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF);

        private delegate uint TableReader(IntPtr table, ref int size);

        private readonly struct Row
        {
            private readonly byte[] _bytes;
            public Row(byte[] bytes) => _bytes = bytes;
            public int ReadInt(int offset) => BitConverter.ToInt32(_bytes, offset);
            public byte[] ReadBytes(int offset, int count) => _bytes.AsSpan(offset, count).ToArray();
        }

        private static List<Row> ReadTable(TableReader read, int rowSize)
        {
            var rows = new List<Row>();
            var size = 0;
            read(IntPtr.Zero, ref size);
            for (var attempt = 0; attempt < 3 && size > 0; attempt++)
            {
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    var status = read(buffer, ref size);
                    if (status == 122) continue; // ERROR_INSUFFICIENT_BUFFER: the table grew, try again with the new size
                    if (status != 0) return rows;
                    var count = Marshal.ReadInt32(buffer);
                    for (var i = 0; i < count; i++)
                    {
                        var bytes = new byte[rowSize];
                        Marshal.Copy(buffer + 4 + i * rowSize, bytes, 0, rowSize);
                        rows.Add(new Row(bytes));
                    }
                    return rows;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            return rows;
        }
    }
}
