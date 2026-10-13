using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Browser
{
    public class BrowserControl
    {
        public class FirefoxSettings
        {
            public bool TotalCookieProtectionEnabled { get; set; }
            public bool TrackingProtectionEnabled { get; set; }
            public bool EnhancedTrackingProtectionEnabled { get; set; }
            public string ProfilePath { get; set; } = string.Empty;
            public bool IsInstalled { get; set; }
        }

        public async Task<FirefoxSettings> GetFirefoxSettingsAsync()
        {
            return await Task.Run(() =>
            {
                var settings = new FirefoxSettings();

                try
                {
                    // Find Firefox installation
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    var firefoxPath = Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe");
                    
                    if (!File.Exists(firefoxPath))
                    {
                        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                        firefoxPath = Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe");
                    }

                    settings.IsInstalled = File.Exists(firefoxPath);

                    if (settings.IsInstalled)
                    {
                        // Find Firefox profile directory
                        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        var profilesPath = Path.Combine(appData, "Mozilla", "Firefox", "Profiles");

                        if (Directory.Exists(profilesPath))
                        {
                            var profiles = Directory.GetDirectories(profilesPath);
                            if (profiles.Length > 0)
                            {
                                settings.ProfilePath = profiles[0];
                                
                                // Read prefs.js
                                var prefsPath = Path.Combine(settings.ProfilePath, "prefs.js");
                                if (File.Exists(prefsPath))
                                {
                                    var prefsContent = File.ReadAllText(prefsPath);
                                    
                                    // Check for Total Cookie Protection
                                    settings.TotalCookieProtectionEnabled = prefsContent.Contains("network.cookie.cookieBehavior", StringComparison.OrdinalIgnoreCase) &&
                                                                          prefsContent.Contains("5"); // 5 = Total Cookie Protection
                                    
                                    // Check for Tracking Protection
                                    settings.TrackingProtectionEnabled = prefsContent.Contains("privacy.trackingprotection.enabled", StringComparison.OrdinalIgnoreCase) &&
                                                                       prefsContent.Contains("true", StringComparison.OrdinalIgnoreCase);
                                    
                                    // Check for Enhanced Tracking Protection
                                    settings.EnhancedTrackingProtectionEnabled = prefsContent.Contains("browser.contentblocking.category", StringComparison.OrdinalIgnoreCase);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting Firefox settings: {ex.Message}");
                }

                return settings;
            });
        }

        public async Task<bool> SetFirefoxTrackingProtectionAsync(bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var profilesPath = Path.Combine(appData, "Mozilla", "Firefox", "Profiles");

                    if (!Directory.Exists(profilesPath))
                        return false;

                    var profiles = Directory.GetDirectories(profilesPath);
                    if (profiles.Length == 0)
                        return false;

                    var prefsPath = Path.Combine(profiles[0], "prefs.js");
                    if (!File.Exists(prefsPath))
                        return false;

                    var prefsContent = File.ReadAllText(prefsPath);
                    var value = enabled ? "true" : "false";
                    
                    // Update or add the preference
                    if (prefsContent.Contains("privacy.trackingprotection.enabled"))
                    {
                        // Update existing
                        prefsContent = System.Text.RegularExpressions.Regex.Replace(
                            prefsContent,
                            @"user_pref\(""privacy\.trackingprotection\.enabled"",\s*[^)]+\);",
                            $"user_pref(\"privacy.trackingprotection.enabled\", {value});");
                    }
                    else
                    {
                        // Add new
                        prefsContent += $"\nuser_pref(\"privacy.trackingprotection.enabled\", {value});";
                    }

                    File.WriteAllText(prefsPath, prefsContent);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public List<string> GetInstalledBrowsers()
        {
            var browsers = new List<string>();
            
            try
            {
                // Check for common browsers
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                var browserPaths = new[]
                {
                    Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe"),
                    Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe"),
                    Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                    Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                    Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe")
                };

                foreach (var path in browserPaths)
                {
                    if (File.Exists(path))
                    {
                        var name = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)));
                        if (name == "Application")
                            name = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path))));
                        browsers.Add(name);
                    }
                }
            }
            catch
            {
            }

            return browsers;
        }
    }
}

