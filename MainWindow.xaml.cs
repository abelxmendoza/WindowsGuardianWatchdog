using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WindowsGuardianWatchdog.Views;
using WinRT.Interop;

namespace WindowsGuardianWatchdog
{
    public sealed partial class MainWindow : Window
    {
        private readonly Frame _contentFrame = new();
        private readonly NavigationView _navigation = new();
        private IntPtr _largeIconHandle;
        private IntPtr _smallIconHandle;
        private readonly Dictionary<string, Type> _routes = new()
        {
            ["Dashboard"] = typeof(DashboardView), ["SecurityOverview"] = typeof(SecurityOverviewView),
            ["SecurityMonitor"] = typeof(SecurityMonitorView), ["FileIntegrity"] = typeof(FileIntegrityView),
            ["ThreatDetection"] = typeof(ThreatDetectionView), ["SecurityAudit"] = typeof(SecurityAuditView),
            ["Remediation"] = typeof(RemediationView), ["Hardening"] = typeof(HardeningView),
            ["Cleanup"] = typeof(CleanupView), ["Drivers"] = typeof(DriversView),
            ["Startup"] = typeof(StartupView), ["MLInsights"] = typeof(MLInsightsView),
            ["Alerts"] = typeof(AlertsView), ["Settings"] = typeof(SettingsView),
            ["AppUpdates"] = typeof(AppUpdatesView), ["FileSearch"] = typeof(FileSearchView)
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
            _navigation.PaneHeader = CreatePaneHeader();
            _navigation.PaneFooter = new TextBlock
            {
                Text = "WATCHDOG  •  WINDOWS PROTECTION",
                FontSize = 10,
                CharacterSpacing = 70,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 71, 191, 255)),
                Margin = new Thickness(12, 8, 8, 14)
            };
            _navigation.Content = _contentFrame;
            _navigation.SelectionChanged += Navigation_SelectionChanged;

            var dashboard = AddItem("Dashboard", "Dashboard", Symbol.Home);
            AddHeader("MONITOR");
            AddItem("Security overview", "SecurityOverview", Symbol.View);
            AddItem("Live monitor", "SecurityMonitor", Symbol.Play);
            AddItem("File integrity", "FileIntegrity", Symbol.Document);
            AddItem("File search", "FileSearch", Symbol.Find);
            AddItem("Alerts & incidents", "Alerts", Symbol.Important);
            AddHeader("INVESTIGATE");
            AddItem("Threat detection", "ThreatDetection", Symbol.Find);
            AddItem("Security audit", "SecurityAudit", Symbol.AllApps);
            AddItem("ML insights", "MLInsights", Symbol.OutlineStar);
            AddHeader("PROTECT");
            AddItem("Remediation center", "Remediation", Symbol.Repair);
            AddItem("Security hardening", "Hardening", Symbol.Permissions);
            AddHeader("MAINTAIN");
            AddItem("App updates", "AppUpdates", Symbol.Sync);
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

        private static UIElement CreatePaneHeader()
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(4, 8, 4, 12) };
            header.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Resources/LogoMark.png")),
                Width = 46,
                Height = 46,
                Stretch = Stretch.UniformToFill
            });
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new TextBlock { Text = "WINDOWS GUARDIAN", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 14 });
            labels.Children.Add(new TextBlock
            {
                Text = "WATCHDOG",
                FontSize = 10,
                CharacterSpacing = 210,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 226, 255))
            });
            header.Children.Add(labels);
            return header;
        }

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
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
                ApplyNativeWindowIcons(windowHandle, iconPath);
            }
            appWindow.Resize(new Windows.Graphics.SizeInt32(1440, 900));
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 960;
                presenter.PreferredMinimumHeight = 640;
            }
        }

        private void ApplyNativeWindowIcons(IntPtr windowHandle, string iconPath)
        {
            _largeIconHandle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 64, 64, LoadFromFile);
            _smallIconHandle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 32, 32, LoadFromFile);

            if (_largeIconHandle != IntPtr.Zero) SendMessage(windowHandle, SetIconMessage, IconBig, _largeIconHandle);
            if (_smallIconHandle != IntPtr.Zero) SendMessage(windowHandle, SetIconMessage, IconSmall, _smallIconHandle);
        }

        private const uint SetIconMessage = 0x0080;
        private const int IconSmall = 0;
        private const int IconBig = 1;
        private const uint ImageIcon = 1;
        private const uint LoadFromFile = 0x0010;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint load);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, int parameter, IntPtr value);
    }
}
