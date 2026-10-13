using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using WindowsGuardianWatchdog.Views;
using WinRT.Interop;

namespace WindowsGuardianWatchdog
{
    public sealed partial class MainWindow : Window
    {
        private readonly Frame _contentFrame = new();
        private readonly NavigationView _navigation = new();
        private readonly Dictionary<string, Type> _routes = new()
        {
            ["Dashboard"] = typeof(DashboardView), ["SecurityOverview"] = typeof(SecurityOverviewView),
            ["SecurityMonitor"] = typeof(SecurityMonitorView), ["FileIntegrity"] = typeof(FileIntegrityView),
            ["ThreatDetection"] = typeof(ThreatDetectionView), ["SecurityAudit"] = typeof(SecurityAuditView),
            ["Remediation"] = typeof(RemediationView), ["Hardening"] = typeof(HardeningView),
            ["Cleanup"] = typeof(CleanupView), ["Drivers"] = typeof(DriversView),
            ["Startup"] = typeof(StartupView), ["MLInsights"] = typeof(MLInsightsView),
            ["Alerts"] = typeof(AlertsView), ["Settings"] = typeof(SettingsView)
        };

        public MainWindow()
        {
            Title = "Windows Guardian Watchdog";
            BuildShell();
            ConfigureWindowsShell();
        }

        private void BuildShell()
        {
            _navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
            _navigation.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
            _navigation.IsSettingsVisible = true;
            _navigation.IsPaneToggleButtonVisible = true;
            _navigation.OpenPaneLength = 280;
            _navigation.CompactPaneLength = 52;
            _navigation.PaneTitle = "WINDOWS GUARDIAN";
            _navigation.Content = _contentFrame;
            _navigation.SelectionChanged += Navigation_SelectionChanged;

            var dashboard = AddItem("Dashboard", "Dashboard", Symbol.Home);
            AddHeader("MONITOR");
            AddItem("Security overview", "SecurityOverview", Symbol.View);
            AddItem("Live monitor", "SecurityMonitor", Symbol.Play);
            AddItem("File integrity", "FileIntegrity", Symbol.Document);
            AddItem("Alerts & incidents", "Alerts", Symbol.Important);
            AddHeader("INVESTIGATE");
            AddItem("Threat detection", "ThreatDetection", Symbol.Find);
            AddItem("Security audit", "SecurityAudit", Symbol.AllApps);
            AddItem("ML insights", "MLInsights", Symbol.OutlineStar);
            AddHeader("PROTECT");
            AddItem("Remediation center", "Remediation", Symbol.Repair);
            AddItem("Security hardening", "Hardening", Symbol.Permissions);
            AddHeader("MAINTAIN");
            AddItem("System cleanup", "Cleanup", Symbol.Delete);
            AddItem("Driver manager", "Drivers", Symbol.Download);
            AddItem("Startup manager", "Startup", Symbol.Setting);

            _navigation.SelectedItem = dashboard;
            Content = _navigation;
            Navigate("Dashboard");
        }

        private NavigationViewItem AddItem(string label, string tag, Symbol symbol)
        {
            var item = new NavigationViewItem { Content = label, Tag = tag, Icon = new SymbolIcon(symbol) };
            _navigation.MenuItems.Add(item);
            return item;
        }

        private void AddHeader(string label) => _navigation.MenuItems.Add(new NavigationViewItemHeader { Content = label });

        private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected) { Navigate("Settings"); return; }
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string route) Navigate(route);
        }

        private void Navigate(string route)
        {
            if (_routes.TryGetValue(route, out var pageType) && _contentFrame.CurrentSourcePageType != pageType)
                _contentFrame.Navigate(pageType);
        }

        private void ConfigureWindowsShell()
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Logo.ico");
            if (File.Exists(iconPath)) appWindow.SetIcon(iconPath);
            appWindow.Resize(new Windows.Graphics.SizeInt32(1440, 900));
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 960;
                presenter.PreferredMinimumHeight = 640;
            }
        }
    }
}
