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
            TryDeleteLegacyChatHistory();
            EngagementStore.Instance.Load();
            EngagementStore.Instance.RecordAppOpen();
            GpuVendorStore.Instance.Load();
            PerformanceScoreService.Initialize();
            // Nouveau scan à chaque lancement : le dernier résultat reste affiché le temps qu'il se termine.
            _ = PerformanceScoreService.ScanAsync();
            _ = GpuDetector.DetectAsync();
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

#if DEBUG
            // Outil de test (version de développement uniquement) : TweaksLabPanel.exe --page Gpu
            var startPage = e.Args.SkipWhile(a => a != "--page").Skip(1).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(startPage))
                main.NavigateTo(startPage);
#endif
        }
        catch (Exception ex)
        {
            StartupLog.Write("ERREUR: " + ex);
            MessageBox.Show(
                LocalizationService.T("Le panel n'a pas pu démarrer.\n\n", "The panel couldn't start.\n\n") + ex.Message,
                "TweaksLab Panel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void TryDeleteLegacyChatHistory()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TweaksLabPanel",
                "chat-history.json");
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Nettoyage optionnel de l'ancienne persistance chat.
        }
    }

    private void HookGlobalExceptions()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            StartupLog.Write("Dispatcher: " + args.Exception);
            MessageBox.Show(
                LocalizationService.T("Erreur inattendue :\n\n", "Unexpected error:\n\n") + args.Exception.Message,
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
