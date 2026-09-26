using System.Windows;
using System.Windows.Threading;

namespace YouLoader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);

        // YouLoader.exe --uninstall removes the app without opening the main window.
        if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            UninstallFlow.Run(null, Core.Services.AppSettings.Load().OutputDir);
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    // Keep the app open after an unexpected error: running downloads shouldn't die with it.
    static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}\n\nYouLoader will keep running. If this keeps happening, please report it on GitHub.",
            "YouLoader",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
