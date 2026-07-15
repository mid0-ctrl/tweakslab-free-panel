using System.IO;
using System.Windows;
using System.Windows.Threading;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        HookGlobalExceptions();
        StartupLog.Write("Demarrage");

        try
        {
            base.OnStartup(e);

            AppSettingsStore.Instance.Load();
            UserProfileStore.Instance.Load();
            EngagementStore.Instance.Load();
            EngagementStore.Instance.RecordAppOpen();
            GpuVendorStore.Instance.Load();
            PerformanceScoreService.Initialize();
            _ = PerformanceScoreService.EnsureScannedAsync();
            UpdateService.QueueStartupCheck();

            StartupLog.Write("Creation MainWindow");
            var main = new MainWindow();
            MainWindow = main;
            main.ContentRendered += (_, _) =>
            {
                main.WindowState = WindowState.Normal;
                main.Activate();
                main.Focus();
                main.Topmost = true;
                main.Topmost = false;
                StartupLog.Write("Fenetre affichee");
            };
            main.Show();
            StartupLog.Write("MainWindow.Show OK");
        }
        catch (Exception ex)
        {
            StartupLog.Write("ERREUR: " + ex);
            MessageBox.Show(
                "Le panel n'a pas pu demarrer.\n\n" + ex.Message,
                "TweaksLab Panel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void HookGlobalExceptions()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            StartupLog.Write("Dispatcher: " + args.Exception);
            MessageBox.Show(
                "Erreur inattendue :\n\n" + args.Exception.Message,
                "TweaksLab Panel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                StartupLog.Write("AppDomain: " + ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLog.Write("Task: " + args.Exception);
            args.SetObserved();
        };
    }
}
