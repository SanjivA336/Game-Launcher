# Nexus Launcher

**One fast, offline library for all the PC games on your computer, no matter which launcher installed them.**

Nexus scans the folders you point it at, works out which folders are games and which `.exe` starts each one,
and shows everything as a single cover-art library. There is no account, no sign-in and no store client:
it opens in about a second and works with no internet connection.

> **Version 1.0.** It is usable day to day, but it is a young project with rough edges (see
> [Limitations](#limitations)). Windows only.

**Jump to:** [What is Nexus](#what-is-nexus) | [For contributors](#for-contributors) | [Full guide](docs/GUIDE.md)

---

## Get started

**[Download Nexus for Windows](https://github.com/SanjivA336/Game-Launcher/releases/latest/download/Nexus-Launcher.exe)**
(one file, about 70 MB). Save it anywhere and double-click it — nothing to install, no .NET to set up.

Because the file isn't code-signed, Windows may say "Windows protected your PC". Choose **More info**, then **Run
anyway**. If you'd rather not trust a downloaded exe, you can [build it yourself](#for-contributors) from source.
Needs **Windows 11**, 64-bit (Windows 10 should run it, with plainer window effects, but hasn't been tested).

**First run:** the library starts empty. Open **Settings** (bottom of the sidebar) — it opens on **Library
sources** — and press **Add app libraries** to pull in the game folders of your launchers (Steam, Epic, GOG and so
on), and/or **Add folder...** for anything else, such as `D:\Games`. Press **Save and rescan**, and Nexus tells you
how many games it found.

See **[the full guide](docs/GUIDE.md)** for how every page and setting works, and its
[Troubleshooting](docs/GUIDE.md#troubleshooting) section if something's not doing what you expect.

---

# What is Nexus

## The idea

If you play games from several places (Steam, Epic, GOG, Amazon, itch.io, standalone installs), your library is
scattered across launchers that each want to be opened first, most of them slowly, and some of them online.
Nexus is a small alternative front door: point it at the folders your games live in, and it gives you one
library, launching straight into the game's own `.exe`.

At its core Nexus is a nicer way to store shortcuts: all your games in one place, without putting them on your
desktop. A few extras are layered on top (a start page, a launcher list, a deals page), but it is deliberately
**not** a replacement for Steam or Epic: no store, no downloads, no achievements or news feeds.

It was started for one person's mixed collection of games, and it is also a learning project for WPF and the MVVM
pattern, so the code is written to be read.

## What it can do

- **Home** — your most recently played games in one row, plus **Pick something for me** for when you can't decide
- **Library** — finds games by scanning the folders you point it at, with search, sorting, and tags you can filter
  by (grouped by status, source launcher, players, genre and more; Steam can suggest most of them for you)
- **Each game** — rename it, choose its executable, edit its tags, open its folder, or hide it (there's no delete)
- **Apps** — link your other game launchers so they're one click away, Discord-connections style
- **Deals** — free games and discounts from Steam, Epic, GamerPower and CheapShark, on by default and one switch to turn off
- **Covers** — fetched automatically from Steam; a free SteamGridDB key adds a picker and fills in the rest
- **Names** — cleans up folder-style names automatically (`Core_Keeper` becomes `Core Keeper`)
- **The app itself** — a Windows 11 style dark interface; a fork can add its own page with one class file

The [full guide](docs/GUIDE.md) covers all of this in the detail you'd actually need to use it.

## Design principles

- **It only reads your games.** Nexus never writes, renames, moves or deletes anything inside your game folders.
  The only files it creates are its own, in its data folder (`%LOCALAPPDATA%\Nexus Launcher`).
- **Fast and offline first.** Opening the library needs no network. The internet is used for two optional things
  only: looking up covers and tag suggestions on Steam, and the Deals page (only while it is open, and you can switch
  it off). Nothing is fetched for a game you have already done.
- **Your data stays yours and readable.** Everything is plain JSON you can open, edit or back up. What leaves your
  PC: a game's name (and, for Steam games, its Steam app number) to Steam, to find its cover and tags; a game's name to
  SteamGridDB if you added a key; and ordinary requests to the deal sites while the Deals page is open (no account,
  no personal data).
- **Predictable rules over guessing.** Name clean-up, ignore keywords and sorting follow fixed, documented rules.

## Limitations

Nexus is early, so please know what you're getting:

- **Windows only**, built with WPF. The look is designed for Windows 11; Windows 10 hasn't been tested.
- **Games can't be deleted from the library**, only hidden. If a game's folder disappears (uninstalled, drive
  unplugged) it stays listed as "Not installed" until you hide it or point it at its new folder.
- Playtime isn't tracked, only launches made through Nexus, and the default executable is a guess (the first one
  alphabetically), so some games need their main `.exe` chosen by hand.
- Tags come from a built-in list (they can't be created or renamed yet), and your sort and filter choices aren't
  remembered between sessions.
- **Steam suggestions are a best effort.** They use Steam's public store pages, which are not an official interface and
  can change. Games Steam doesn't sell (or names it can't match exactly) are reported as "not found", and a few Steam
  games have no portrait cover. Nexus never guesses: it only accepts a close name match.
- **Deals depend on other websites.** They use public feeds that can change or go down without notice (Nexus then
  shows the deals from the sources that still answer), prices are US dollars only, and Nexus doesn't check that a
  deal is available in your country.
- The startup scan runs on the main thread, so a huge library may make the window pause for a moment.
- There is no installer: Nexus is one portable `.exe`. It isn't code-signed, so Windows may show a warning the first time.
- There is no test project in the repository yet.

## Roadmap

- A page to manage the library (unhide, fix names in bulk)
- Custom tags, and a place to browse and manage mods
- Playtime tracking

---

# For contributors

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
command above, then create a GitHub release and attach the exe as `Nexus-Launcher.exe`. The Download link at the top
always points at the newest release's file of that name.

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
    public string Glyph => "\uE70B";              // a Segoe Fluent Icons character
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

## Contributing and rebuilding the icon

Issues and pull requests are welcome. The app icon is generated from `Game Launcher/Resources/Branding/*.svg`; after
editing them, run `powershell -ExecutionPolicy Bypass -File Tools\Generate-Icons.ps1` (it uses Microsoft Edge to
render the SVGs) and rebuild. When testing, set `NEXUS_DATA_DIR` to a scratch folder so you don't touch your own
library.

---

## Disclaimer and privacy

- **Use it at your own risk.** Nexus is a hobby project provided as-is, with no warranty (see the [MIT license](LICENSE)).
  There is no installer and no support promise. Builds from source, or a copy someone else built, are not code-signed,
  so Windows may show a "SmartScreen" warning.
- **Deals are just links.** Nexus isn't affiliated with Steam, Epic, GamerPower, CheapShark or any store, and it never
  handles purchases, payments or accounts. It lists deals published by those sites and opens their web pages; prices and
  availability can be wrong or out of date, and whether to trust a store or a deal is your decision.
- **What leaves your PC:** a game's name (and, for Steam games, its Steam app number) sent to Steam when Nexus looks up a
  cover or tags; a game's name and your own SteamGridDB API key sent to SteamGridDB, only if you added a key; and
  ordinary web requests to the deal sites while the Deals page is open (they can see your IP address, as any website
  you visit can). Nexus has no analytics, no accounts and no telemetry, and keeps everything else on your PC.
- **Steam is used through its public store pages,** which have no official licence for this. They can change or stop
  working, and Nexus is not affiliated with or endorsed by Valve. Covers are publishers' artwork, kept only in your own
  data folder.

## Credits and notes

- Covers come from Steam and from [SteamGridDB](https://www.steamgriddb.com) (uploaded by its community). Nexus is not
  affiliated with SteamGridDB, Valve, Epic Games or any other launcher or publisher, and the names of games and
  launchers belong to their owners.
- **License:** [MIT](LICENSE). You are free to use, change and share Nexus, including in your own projects.
