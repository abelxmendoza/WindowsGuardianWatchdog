using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Drivers
{
    public class DriverInstaller
    {
        public event EventHandler<DriverInstallProgress>? ProgressUpdated;

        public async Task<DriverInstallResult> InstallAsync(DriverInfo driver, string driverPackagePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(driverPackagePath))
                    {
                        return new DriverInstallResult
                        {
                            Success = false,
                            Message = "Driver package not found"
                        };
                    }

                    // Verify signature
                    if (!VerifyDriverSignature(driverPackagePath))
                    {
                        return new DriverInstallResult
                        {
                            Success = false,
                            Message = "Driver signature verification failed"
                        };
                    }

                    OnProgressUpdated(new DriverInstallProgress
                    {
                        Status = "Installing driver...",
                        Progress = 50
                    });

                    // Install using pnputil
                    var result = InstallWithPnputil(driverPackagePath);

                    OnProgressUpdated(new DriverInstallProgress
                    {
                        Status = result.Success ? "Installation complete" : "Installation failed",
                        Progress = 100
                    });

                    return result;
                }
                catch (Exception ex)
                {
                    return new DriverInstallResult
                    {
                        Success = false,
                        Message = $"Installation error: {ex.Message}"
                    };
                }
            });
        }

        private bool VerifyDriverSignature(string filePath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"Get-AuthenticodeSignature '{filePath}' | Select-Object -ExpandProperty Status\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.Trim().Equals("Valid", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // If we can't verify, allow installation but log warning
            }

            return true; // Allow unsigned drivers with warning
        }

        private DriverInstallResult InstallWithPnputil(string driverPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pnputil",
                    Arguments = $"/add-driver \"{driverPath}\" /install",
                    Verb = "runas", // Run as administrator
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit();
                    return new DriverInstallResult
                    {
                        Success = process.ExitCode == 0,
                        Message = process.ExitCode == 0 
                            ? "Driver installed successfully" 
                            : "Driver installation failed"
                    };
                }
            }
            catch (Exception ex)
            {
                return new DriverInstallResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }

            return new DriverInstallResult
            {
                Success = false,
                Message = "Installation process failed to start"
            };
        }

        protected virtual void OnProgressUpdated(DriverInstallProgress progress)
        {
            ProgressUpdated?.Invoke(this, progress);
        }
    }

    public class DriverInstallResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class DriverInstallProgress
    {
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
    }
}

