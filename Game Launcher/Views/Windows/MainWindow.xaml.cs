using Game_Launcher.Helpers;
using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.ViewModels.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace Game_Launcher {
    public partial class MainWindow : Window {

        private const string HomeId = "home";
        private const string LibraryId = "library";

        // One shared Library page for the whole session, so returning to it doesn't rebuild it (and rescan your drives)
        private readonly Views.Pages.Library _library = new();

        // Every page of the app: the built-in ones plus any added with INexusPage. The sidebar is generated from this list.
        private readonly List<NavPage> _pages;

        // Pages are built the first time they're opened and then reused
        private readonly Dictionary<string, Page> _instances = new();

        private MainWindowVM VM => (MainWindowVM)DataContext;

        public MainWindow() {
            InitializeComponent();

            var builtIn = new List<NavPage> {
                new(HomeId, "Home", "\uE80F", () => new Views.Pages.Home(() => NavigateTo(LibraryId)), ShowInSidebar: false),
                new(LibraryId, "Library", "\uE7FC", () => _library),
                // Can be switched off in Settings; then it isn't shown and never goes online
                new("deals", "Deals", "\uE719", () => new Views.Pages.Deals(), IsEnabled: prefs => prefs.DealsPageEnabled),
            };

            // Pages someone added by writing a class that implements INexusPage (see the README) come after the built-in ones
            _pages = [.. builtIn, .. PageDiscovery.Find(typeof(MainWindow).Assembly, builtIn.Select(p => p.Id))];

            SourceInitialized += (_, _) => WindowStyler.Apply(this);
            StateChanged += MainWindow_StateChanged;
            TitleBar.MouseDown += TitleBar_MouseDown;
            PageHost.Navigated += PageHost_Navigated;

            DataContext = new MainWindowVM(
                minimize: () => WindowState = WindowState.Minimized,
                maximize: ToggleMaximize,
                close: () => Close(),
                pages: _pages,
                navigate: NavigateTo,
                goHome: () => NavigateTo(HomeId),
                preferencesClosed: OnPreferencesClosed
            );
            VM.ApplyPreferences(_pages, Preferences.Load());

            NavigateTo(HomeId); // Home is the start page
        }

        private Page GetPage(string id) {
            if (!_instances.TryGetValue(id, out var page)) {
                page = _pages.First(p => p.Id == id).Create();
                _instances[id] = page;
            }
            return page;
        }

        private void NavigateTo(string id) {
            var page = GetPage(id);

            // Clicking Library while already in it puts it back to a clean start (search cleared, scrolled up)
            if (id == LibraryId) {
                _library.ShowHome();
            }

            // Navigating to the page that's already showing would do nothing, so only navigate when we're elsewhere
            if (PageHost.Content != page) {
                PageHost.Navigate(page);
            }
        }

        // Runs after every navigation (including the back button on a game's options page), so the sidebar highlight always matches
        private void PageHost_Navigated(object sender, NavigationEventArgs e) {
            string? id = _instances.FirstOrDefault(kv => kv.Value == e.Content).Key;
            if (id is not null) {
                VM.Select(id == HomeId ? null : id);
            }
            // A page that isn't in the list (a game's options) keeps the previous highlight: it belongs to the page you came from
        }

        private void OnPreferencesClosed() {
            var prefs = Preferences.Load();
            VM.ApplyPreferences(_pages, prefs);

            // If the page you were on was just switched off in Settings, fall back to Home
            string? currentId = _instances.FirstOrDefault(kv => kv.Value == PageHost.Content).Key;
            var current = _pages.FirstOrDefault(p => p.Id == currentId);
            if (current?.IsEnabled is not null && !current.IsEnabled(prefs)) {
                NavigateTo(HomeId);
            }

            _library.RefreshAfterSettings();
        }

        private void ToggleMaximize() {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e) {
            bool maximized = WindowState == WindowState.Maximized;

            // Swap the icon between "maximize" and "restore"
            MaximizeButton.Content = maximized ? "" : "";

            // A maximized borderless window extends past the screen by the width of its invisible resize border,
            // which would push the title bar buttons off-screen. Pull the content back in by that amount.
            RootGrid.Margin = maximized ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left) : new Thickness(0);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) {
                return;
            }

            if (e.ClickCount == 2) {
                ToggleMaximize();
            }
            else {
                DragMove();
            }
        }
    }
}
