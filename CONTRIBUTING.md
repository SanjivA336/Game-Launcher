# Contributing to Nexus Launcher

This started as a solo project, but issues and pull requests are welcome.

## How it's built

C#, **.NET 8**, **WPF**, using the **MVVM** pattern (Model-View-ViewModel: screens in XAML, the logic behind
them in separate classes that the screens bind to). No third-party UI libraries.

```
Game Launcher/
  Models/        GameMapping (one game), Preferences (settings), TagCatalog (the built-in tags),
                 AppEntry (a program on the Apps tab), Deal (one offer)
  Services/      GameMappingManager (scanning, saving, editing, re-pointing games), LibraryQuery (search + filter + sort),
                 CoverService / CoverStore / SteamGridDbClient (finding, saving and fetching covers),
                 SteamLocator / SourceRules / ScanDiagnostics (the scan-folder helpers behind Settings),
                 LauncherLibraries (where each launcher keeps games, and which launcher a game came from),
                 KnownApps / AppsStore (launcher detection and apps.json), NavPage (the list the sidebar is built from),
                 INexusPage / PageDiscovery (how a fork adds a page), GamePicker (rules for "pick something for me")
    Steam/       SteamStoreClient (store lookups), SteamTags (Steam's labels to Nexus tags), SteamTagSuggester and
                 BulkTagSuggestions (one game / every game), SteamCovers (portrait covers), SteamAppIds (local install records)
    Deals/       one adapter per website (SteamDeals, EpicDeals, GamerPowerDeals, CheapSharkDeals), DealsService
                 (runs them together and remembers the answer for a while), DealsQuery (search, filters, sorting, pages),
                 DealStores (one name per store), DealLinks (which links may be opened)
  ViewModels/    the logic behind each page, window and control
  Views/         the XAML screens: Pages (Home, Library, Deals, game options), Windows and reusable Controls
                 (including the Apps tab of Settings)
  Helpers/       NameCleaner, CoverArt, WindowStyler (Windows 11 window effects), RelativeTime, AppPaths (where data
                 lives), IconExtractor, ShortcutResolver, FolderPicker
  Resources/     Styles (Theme.xaml = colors/fonts, Controls.xaml = how each control looks) and Branding (the icon)
Tools/           Generate-Icons.ps1 (rebuilds the icon files from the SVG artwork)
```

To change how the whole app looks, start with `Resources/Styles/Theme.xaml`.

## Building it yourself

Clone the repository (needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)), then use whichever
you like.

**Visual Studio 2022:** open `Game Launcher.sln` and press F5.

**VS Code:** install the *C# Dev Kit* extension, open the folder, and press F5 (the repo includes the launch
and build settings).

**Command line**, from the repo root:

```
dotnet run --project "Game Launcher/Game Launcher.csproj"
```

**A standalone `.exe`** you can copy anywhere (the same kind of file as the download):

```
dotnet publish "Game Launcher/Game Launcher.csproj" -p:PublishProfile=win-x64
```

This produces `Game Launcher/bin/publish/Game Launcher.exe`: one file of about 70 MB that includes .NET itself, so the
PC running it needs nothing installed. (A smaller build that needs the .NET 8 Desktop Runtime on the PC is possible
by setting `SelfContained` to `false` in `Game Launcher/Properties/PublishProfiles/win-x64.pubxml`.) Your library is
**not** stored next to the exe, so you can move, rebuild or delete the exe without losing anything (see
[Where your data lives](docs/GUIDE.md#where-your-data-lives) in the guide).

**Making a release** (for the maintainer): raise `Version` in `Game Launcher/Game Launcher.csproj`, run the publish
command above, then create a GitHub release and attach the exe as `Nexus-Launcher.exe`. The Download link on the
README always points at the newest release's file of that name.

## Adding your own page

If you fork Nexus you can add a page of your own (a notes page, a screenshots page, anything) without touching the
sidebar code. Nexus looks for classes that implement `INexusPage` when it starts and adds a sidebar entry for each
one it finds. That is the whole extension mechanism: there are no plug-in files to download or load, so a "mod" is
simply a fork or a pull request with one more class.

1. In Visual Studio, add a WPF **Page** (for example `Views/Pages/Notes.xaml`) and build your screen in it. The
   shared styles (`Card`, `AccentButton`, `SectionTitleText`...) are available to it automatically, so it matches the
   rest of the app.
2. Add a class that implements `INexusPage` (anywhere in the project):

```csharp
using Game_Launcher.Services;
using System.Windows.Controls;

public class NotesPage : INexusPage {
    public string Id => "notes";                 // unique; must not be home, library or deals
    public string Title => "Notes";              // the sidebar label
    public string Glyph => "";              // a Segoe Fluent Icons character
    public int Order => 10;                      // lower numbers come first; built-in pages always come before yours
    public Page CreatePage() => new Views.Pages.Notes();   // built once, the first time it is opened
}
```

3. Run it. The page appears in the sidebar after Library and Deals, highlights when you're on it, and is reused when
   you come back. A page whose class fails to load is skipped (see the Visual Studio output window) and never stops
   Nexus from starting.

Things to know: pages can read the same data as the rest of the app (`GameMappingManager.LoadMappings()`, `Preferences.Load()`),
and should follow the same rule as Nexus itself: only write to your own files inside the data folder (`AppPaths.DataDirectory`),
never inside a game's folder. If you want your page to appear only when a setting is on, add a property to `Preferences`
and a switch in Settings, the way the Deals page does it.

## Rebuilding the icon

The app icon is generated from `Game Launcher/Resources/Branding/*.svg`; after editing them, run
`powershell -ExecutionPolicy Bypass -File Tools\Generate-Icons.ps1` (it uses Microsoft Edge to render the SVGs) and
rebuild.

## Testing

There's no test project in the repository yet. When trying things out, set the environment variable `NEXUS_DATA_DIR`
to a scratch folder first, so you don't touch your own library.
