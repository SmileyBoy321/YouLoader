using System.ComponentModel;
using System.Windows;
using YouLoader.Core.Services;

namespace YouLoader;

/// <summary>The "Uninstall…" link and <c>YouLoader.exe --uninstall</c>.</summary>
static class UninstallFlow
{
    /// <summary>Returns true when YouLoader was removed and should close right away.</summary>
    public static bool Run(Window? owner, string outputDir)
    {
        var window = new UninstallWindow(outputDir);
        if (owner is not null) window.Owner = owner;
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.ShowDialog();

        if (!window.Removed) return false;
        try
        {
            // The window is closed now; this deletes the exe a few seconds after YouLoader exits.
            Uninstaller.StartFinalCleanup(window.Plan);
        }
        catch (Win32Exception)
        {
            // Without cmd.exe the exe stays behind; everything else is already gone.
        }
        return true;
    }
}
