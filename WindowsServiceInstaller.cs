using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace WindowsGuardianWatchdog
{
    [RunInstaller(true)]
    public class WindowsServiceInstaller : Installer
    {
        private ServiceInstaller? _serviceInstaller;
        private ServiceProcessInstaller? _processInstaller;

        public WindowsServiceInstaller()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _processInstaller = new ServiceProcessInstaller();
            _serviceInstaller = new ServiceInstaller();

            // Service account information
            _processInstaller.Account = ServiceAccount.LocalSystem;
            _processInstaller.Username = null;
            _processInstaller.Password = null;

            // Service information
            _serviceInstaller.ServiceName = "WindowsGuardianWatchdog";
            _serviceInstaller.DisplayName = "Windows Guardian Watchdog Service";
            _serviceInstaller.Description = "Monitors system health, file integrity, and provides threat detection";
            _serviceInstaller.StartType = ServiceStartMode.Automatic;

            Installers.Add(_processInstaller);
            Installers.Add(_serviceInstaller);
        }

        public static void InstallService(string servicePath)
        {
            try
            {
                string[] args = { servicePath };
                ManagedInstallerClass.InstallHelper(args);
                Console.WriteLine("Service installed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to install service: {ex.Message}");
                throw;
            }
        }

        public static void UninstallService(string servicePath)
        {
            try
            {
                string[] args = { "/u", servicePath };
                ManagedInstallerClass.InstallHelper(args);
                Console.WriteLine("Service uninstalled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to uninstall service: {ex.Message}");
                throw;
            }
        }
    }
}

