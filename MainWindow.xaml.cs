using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WindowsGuardianWatchdog.Core.Incidents;
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
            ["AppUpdates"] = typeof(AppUpdatesView), ["FileSearch"] = typeof(FileSearchView),
            ["Quarantine"] = typeof(QuarantineView), ["Exposure"] = typeof(ExposureView),
            ["AppHealth"] = typeof(AppHealthView), ["Reports"] = typeof(ReportsView)
        };
        private NavigationViewItem? _noticesItem;

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
            _navigation.OpenPaneLength = 340;
            _navigation.CompactPaneLength = 52;
            _navigation.PaneHeader = CreatePaneHeader();
            _navigation.PaneFooter = new TextBlock
            {
                Text = "Stays on this PC. Nothing is sent away.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 145, 169, 204)),
                Margin = new Thickness(16, 8, 16, 16)
            };
            _navigation.Content = _contentFrame;
            _navigation.SelectionChanged += Navigation_SelectionChanged;

            var dashboard = AddItem("Home", "What to do next", "Dashboard", Symbol.Home);
            AddHeader("KEEP THIS PC HEALTHY");
            AddItem("Update apps", "Install newer versions", "AppUpdates", Symbol.Sync);
            AddItem("Free up space", "Clear leftover files", "Cleanup", Symbol.Delete);
            AddItem("Fix hardware", "Devices that need help", "Drivers", Symbol.Download);
            AddItem("Turn on protection", "Windows safety switches", "Hardening", Symbol.Permissions);
            AddItem("Startup programs", "What opens with Windows", "Startup", Symbol.Setting);
            AddHeader("LOOK AROUND");
            AddItem("What is running", "Programs using this PC", "SecurityMonitor", Symbol.Play);
            AddItem("Find a file", "Search by the file name", "FileSearch", Symbol.Find);
            _noticesItem = AddItem("Notices", "Things this app noticed", "Alerts", Symbol.Important);
            AddItem("Who can connect", "Doors open to the network", "Exposure", Symbol.World);
            AddHeader("A CLOSER LOOK");
            AddItem("Protection details", "A fuller status picture", "SecurityOverview", Symbol.View);
            AddItem("Check for risks", "Known public issues", "ThreatDetection", Symbol.Find);
            AddItem("Watch folders", "Notice if files change", "FileIntegrity", Symbol.Document);
            AddItem("Protection checkup", "Score the safety switches", "SecurityAudit", Symbol.AllApps);
            AddItem("Unusual activity", "Spot something out of the ordinary", "MLInsights", Symbol.OutlineStar);
            AddItem("Fix a problem", "Common repairs, with a preview", "Remediation", Symbol.Repair);
            AddItem("Quarantine", "Files put away, can go back", "Quarantine", Symbol.ProtectedDocument);
            AddItem("Reports", "A written summary to keep", "Reports", Symbol.Page2);
            AddItem("App health", "This app checking itself", "AppHealth", Symbol.Admin);

            _navigation.SelectedItem = dashboard;
            Content = _navigation;
            Navigate("Dashboard");

            EventTimeline.Changed += (_, _) => DispatcherQueue.TryEnqueue(UpdateNoticesBadge);
            UpdateNoticesBadge();
        }

        private void UpdateNoticesBadge()
        {
            if (_noticesItem == null) return;
            var unread = EventTimeline.UnreadCount();
            _noticesItem.InfoBadge = unread == 0 ? null : new InfoBadge { Value = unread };
        }

        private NavigationViewItem AddItem(string label, string hint, string tag, Symbol symbol)
        {
            var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
            text.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 133, 155, 192))
            });
            var item = new NavigationViewItem { Content = text, Tag = tag, Icon = new SymbolIcon(symbol), MinHeight = 56 };
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
            labels.Children.Add(new TextBlock { Text = "Windows Guardian", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 15 });
            labels.Children.Add(new TextBlock
            {
                Text = "Simple care for this PC",
                FontSize = 11,
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
