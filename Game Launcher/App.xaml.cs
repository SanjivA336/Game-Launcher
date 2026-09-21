using Game_Launcher.Helpers;
using System.Windows;

namespace Game_Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e) {
            // Must happen before anything reads the library: earlier versions kept their data next to the exe, and it
            // is copied to the new per-user folder once.
            AppPaths.MigrateLegacyDataIfNeeded();

            base.OnStartup(e);
        }
    }
}
