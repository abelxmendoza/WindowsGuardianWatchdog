using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.Views;
using System;
using System.IO;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace WindowsGuardianWatchdog
{
    public sealed partial class MainWindow : Window
    {
        private Frame _contentFrame;

        public MainWindow()
        {
            // Build UI in code instead of relying on XAML code-gen
            this.Title = "Windows Guardian Watchdog";

            var navView = new NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
                IsSettingsVisible = false
            };

            // Create Frame for page navigation
            _contentFrame = new Frame();
            navView.Content = _contentFrame;

            // Menu items
            void Add(string content, string tag)
            {
                navView.MenuItems.Add(new NavigationViewItem { Content = content, Tag = tag });
            }

            Add("Dashboard", "Dashboard");
            Add("Security Overview", "SecurityOverview");
            Add("Security Monitor", "SecurityMonitor");
            Add("File Integrity", "FileIntegrity");
            Add("Threat Detection", "ThreatDetection");
            Add("Security Audit", "SecurityAudit");
            Add("Remediation", "Remediation");
            Add("System Cleanup", "Cleanup");
            Add("Drivers", "Drivers");
            Add("Startup Manager", "Startup");
            Add("Security Hardening", "Hardening");
            Add("ML Insights", "MLInsights");
            Add("Alerts", "Alerts");

            navView.SelectionChanged += NavView_SelectionChanged;

            // Set initial page
            _contentFrame.Navigate(typeof(DashboardView));

            // Set window content
            this.Content = navView;

            ConfigureWindowsShell();
        }

        private void ConfigureWindowsShell()
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Logo.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 900;
                presenter.PreferredMinimumHeight = 600;
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();
                var pageType = tag switch
                {
                    "Dashboard" => typeof(DashboardView),
                    "SecurityOverview" => typeof(SecurityOverviewView),
                    "SecurityMonitor" => typeof(SecurityMonitorView),
                    "FileIntegrity" => typeof(FileIntegrityView),
                    "ThreatDetection" => typeof(ThreatDetectionView),
                    "SecurityAudit" => typeof(SecurityAuditView),
                    "Remediation" => typeof(RemediationView),
                    "Alerts" => typeof(AlertsView),
                    "Cleanup" => typeof(CleanupView),
                    "Drivers" => typeof(DriversView),
                    "Startup" => typeof(StartupView),
                    "Hardening" => typeof(HardeningView),
                    "MLInsights" => typeof(MLInsightsView),
                    _ => typeof(DashboardView)
                };

                _contentFrame.Navigate(pageType);
            }
        }
    }
}
