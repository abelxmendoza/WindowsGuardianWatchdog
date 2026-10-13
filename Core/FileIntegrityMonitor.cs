using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core
{
    public class FileIntegrityMonitor
    {
        private readonly string _baselinePath;
        private readonly Dictionary<string, FileHash> _baseline;
        private readonly List<string> _monitoredPaths;
        private readonly Timer _monitoringTimer;
        private bool _isMonitoring = false;

        public event EventHandler<FileIntegrityAlert>? IntegrityViolation;

        public FileIntegrityMonitor()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configDir = Path.Combine(appData, "WindowsGuardianWatchdog");
            Directory.CreateDirectory(configDir);
            
            _baselinePath = Path.Combine(configDir, "file_baseline.json");
            _baseline = new Dictionary<string, FileHash>();
            _monitoredPaths = new List<string>();
            _monitoringTimer = new Timer(MonitorTick, null, Timeout.Infinite, Timeout.Infinite);
            
            LoadBaseline();
        }

        public void AddMonitoredPath(string path)
        {
            if (Directory.Exists(path) && !_monitoredPaths.Contains(path))
            {
                _monitoredPaths.Add(path);
            }
        }

        public async Task CreateBaselineAsync()
        {
            _baseline.Clear();
            
            foreach (var path in _monitoredPaths)
            {
                await ScanDirectoryAsync(path);
            }
            
            SaveBaseline();
        }

        public void StartMonitoring(int intervalMs = 60000)
        {
            if (_isMonitoring) return;
            
            _isMonitoring = true;
            _monitoringTimer.Change(0, intervalMs);
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void MonitorTick(object? state)
        {
            if (!_isMonitoring) return;

            Task.Run(async () =>
            {
                foreach (var path in _monitoredPaths)
                {
                    await CheckDirectoryAsync(path);
                }
            });
        }

        private async Task ScanDirectoryAsync(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    try
                    {
                        var hash = await CalculateFileHashAsync(file);
                        var fileInfo = new FileInfo(file);
                        
                        _baseline[file] = new FileHash
                        {
                            Path = file,
                            Hash = hash,
                            Size = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime
                        };
                    }
                    catch
                    {
                        // Skip files that can't be accessed
                    }
                }
            }
            catch
            {
                // Handle directory access errors
            }
        }

        private async Task CheckDirectoryAsync(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    if (_baseline.TryGetValue(file, out var baselineHash))
                    {
                        var currentHash = await CalculateFileHashAsync(file);
                        var fileInfo = new FileInfo(file);
                        
                        if (currentHash != baselineHash.Hash)
                        {
                            OnIntegrityViolation(new FileIntegrityAlert
                            {
                                FilePath = file,
                                AlertType = FileAlertType.Modified,
                                Message = $"File modified: {file}",
                                Timestamp = DateTime.Now,
                                BaselineHash = baselineHash.Hash,
                                CurrentHash = currentHash
                            });
                        }
                        else if (fileInfo.LastWriteTime != baselineHash.LastModified)
                        {
                            // Timestamp changed but hash is same (metadata change)
                            OnIntegrityViolation(new FileIntegrityAlert
                            {
                                FilePath = file,
                                AlertType = FileAlertType.MetadataChanged,
                                Message = $"File metadata changed: {file}",
                                Timestamp = DateTime.Now
                            });
                        }
                    }
                    else
                    {
                        // New file detected
                        OnIntegrityViolation(new FileIntegrityAlert
                        {
                            FilePath = file,
                            AlertType = FileAlertType.NewFile,
                            Message = $"New file detected: {file}",
                            Timestamp = DateTime.Now
                        });
                    }
                }
                
                // Check for deleted files
                foreach (var baselineFile in _baseline.Keys)
                {
                    if (baselineFile.StartsWith(directory, StringComparison.OrdinalIgnoreCase) && 
                        !File.Exists(baselineFile))
                    {
                        OnIntegrityViolation(new FileIntegrityAlert
                        {
                            FilePath = baselineFile,
                            AlertType = FileAlertType.Deleted,
                            Message = $"File deleted: {baselineFile}",
                            Timestamp = DateTime.Now
                        });
                    }
                }
            }
            catch
            {
                // Handle errors
            }
        }

        private async Task<string> CalculateFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToBase64String(hash);
        }

        private void LoadBaseline()
        {
            if (File.Exists(_baselinePath))
            {
                try
                {
                    var json = File.ReadAllText(_baselinePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, FileHash>>(json);
                    if (data != null)
                    {
                        foreach (var item in data)
                        {
                            _baseline[item.Key] = item.Value;
                        }
                    }
                }
                catch
                {
                    // If baseline is corrupted, start fresh
                }
            }
        }

        private void SaveBaseline()
        {
            try
            {
                var json = JsonSerializer.Serialize(_baseline, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_baselinePath, json);
            }
            catch
            {
                // Handle save errors
            }
        }

        protected virtual void OnIntegrityViolation(FileIntegrityAlert alert)
        {
            IntegrityViolation?.Invoke(this, alert);
        }

        public void Dispose()
        {
            StopMonitoring();
            _monitoringTimer?.Dispose();
        }
    }

    public class FileHash
    {
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class FileIntegrityAlert
    {
        public string FilePath { get; set; } = string.Empty;
        public FileAlertType AlertType { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? BaselineHash { get; set; }
        public string? CurrentHash { get; set; }
    }

    public enum FileAlertType
    {
        Modified,
        Deleted,
        NewFile,
        MetadataChanged
    }
}

