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

            // A safety net: an unexpected error is written to crash.log and shown in plain words, instead of the app vanishing.
            // (Everything is saved with write-then-swap, so an error never leaves a half-written data file behind.)
            DispatcherUnhandledException += (_, args) => {
                CrashLog.Write(args.Exception);
                MessageBox.Show($"Something went wrong: {args.Exception.Message}\n\nDetails were saved to:\n{CrashLog.FilePath}",
                    "Nexus", MessageBoxButton.OK, MessageBoxImage.Warning);
                args.Handled = true; // keep going: most errors are one action failing, not the whole app
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) => CrashLog.Write(args.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, args) => { CrashLog.Write(args.Exception); args.SetObserved(); };

            base.OnStartup(e);
        }
    }
}
