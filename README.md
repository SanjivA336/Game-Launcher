# Nexus Launcher

Nexus is a personal, unified game launcher for Windows. If your games are scattered across
Steam, Epic, GOG, itch.io, and random standalone installs, Nexus scans a set of folders you
point it at, figures out which subfolders are games and which executable actually launches
each one, and shows them all in a single fast library — no store client, no sign-in, no
internet connection required to open it.

It's a **WPF / MVVM** app built with **.NET 8**, and also serves as a learning project for
WPF and the MVVM pattern.

***

## How it works

1. **Scan**: starting from a list of root folders you configure, Nexus walks the directory
   tree breadth-first looking for `.exe` files, skipping directories/files that match a
   keyword ignore-list (`redist`, `unins`, `setup`, `helper`, etc.) so it doesn't get lost in
   installer or crash-reporter clutter.
2. **Map**: each folder that contains executables becomes a `GameMapping` — a name, the
   folder path, the list of executables found there, and which one is the "primary" (the one
   Launch actually runs).
3. **Cache**: the results are written to `UserData/mappings.json` next to the executable, so
   subsequent launches read the cache instead of rescanning your whole drive. A manual
   refresh re-scans and merges in anything new without discarding tags/edits on existing
   entries.
4. **Browse & launch**: the library page shows every game as a tile in a responsive grid;
   clicking a tile launches its primary executable, and an options page lets you rename a
   game, change which executable is primary, or hide it from the library.

***

## Current status

This is an early-stage personal project, not a finished product. Here's an honest breakdown
of what works today versus what's stubbed out or missing:

**Implemented**
* Recursive game scanning with configurable ignore keywords
* JSON-backed caching of scan results (`mappings.json`) and settings (`preferences.json`)
* Library page with a responsive, resizable tile grid and live search
* Sorting: name (A to Z / Z to A), recently played, most played, recently added, each with a
  direction toggle. Games with no value (e.g. never played) always sort last.
* Tags describing how a game works (not its genre), shown as chips on game cards and set per
  game on its options page: Single-player, Multiplayer, Co-op, Online, Offline, Local,
  Controller, VR. Installed / Not installed / Never played / Hidden are worked out by Nexus.
* A filter panel to select one or several tags, with an Any (OR) / All (AND) switch
* Play history: last played and launch count, recorded when you launch a game through Nexus
* Per-game options page: rename, add/remove executables (via file picker), pick the primary
  executable, edit tags, and hide the game (hidden games only appear while the "Hidden"
  filter is on)
* A sidebar with the Nexus logo and name that collapses to a slim icon strip (and expands again)
  with the chevron button next to the name
* Launching a game's executable and opening its folder in Explorer
* Cover art (see "Cover art" below): found automatically on [SteamGridDB](https://www.steamgriddb.com)
  when a game is added, renamed or reset; or chosen by you (search SteamGridDB in a popup, or upload
  an image). Games without a cover keep a generated placeholder.
* Name clean-up (Settings > Game names): a switch that cleans the names of newly found games, and
  a button that cleans all existing names after showing you what will change

**Not yet working / incomplete**
* **Preferences window** — Save/Cancel work for what's there (the cover API key and the name
  options), but scan folders are only listed, not editable, and there's no Reset. Until then, scan
  roots have to be set by hand (see "Configuring scan folders" below).
* Play history only counts launches made through Nexus (not games started from other launchers),
  and there's no playtime tracking yet.
* Tags can't be created or renamed yet (the list is built in), and the sort/filter choices
  aren't remembered between sessions.

## Planned / ideas

* Finish the Preferences window (editable roots and excludes)
* Custom tags, and auto-tagging Steam games from Steam's local metadata
* Playtime tracking
* Packaging as a single self-contained `.exe` (no installer, no .NET runtime install required
  on the target machine)

***

## Getting started

**Prerequisites**: Windows, and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### Visual Studio 2022
Open `Game Launcher.sln` and press Run (F5/the green arrow). This runs a Debug build of the
WPF app directly.

### VS Code
The repo includes a `.vscode/launch.json` and `.vscode/tasks.json`, so with the
**C# Dev Kit** extension installed you can just press **F5** and it will build and launch the
app the same way VS 2022's Run button does.

You can also skip the IDE entirely and use the CLI from the repo root:
```
dotnet build "Game Launcher.sln"
dotnet run --project "Game Launcher/Game Launcher.csproj"
```

There's no visual XAML designer in VS Code (unlike VS 2022), but the XAML in this project is
all hand-written anyway, so IntelliSense from the C# extension is enough to work with it.

### Cover art

Nexus downloads game covers from SteamGridDB using their official API, which needs a free
personal API key:

1. Create a free account at [steamgriddb.com](https://www.steamgriddb.com) and copy your API key
   from your profile preferences (the API page).
2. In Nexus, open **Settings**, paste the key into **Cover art**, and press **Save**.
   (Or add `"SteamGridDbApiKey": "your-key"` to `UserData/preferences.json`.)

**When Nexus looks for a cover.** Only when a game has just been **added**, **renamed** (and
saved), or **reset** (and saved). Starting Nexus never triggers lookups, and a game that simply has
no match is not asked about again. (If you're offline the lookup is kept for next time.) A cover
you chose yourself is never replaced.

**Changing a cover yourself.** On a game's options page press **Change cover…**. In the popup you
can either search SteamGridDB (results show the games found and a grid of covers; pick one) or
upload an image (PNG, JPG, BMP, GIF or TIFF; a resized PNG copy is saved and your original isn't
touched). A cover you choose is saved right away and marked as yours. **Use automatic cover**
gives it back to Nexus, which then looks one up again.

**Where covers live.** Each cover is saved once in `UserData/Covers/` (Nexus's own folder, never
inside your game folders), so afterwards it loads from disk and works offline. Files are named
after the game's exe plus a short fingerprint of its folder, and say who picked them, for example
`Trackmania-a1b2c3d4.cover.png` (automatic) or `Trackmania-a1b2c3d4.custom.jpg` (yours). Saving a
new cover deletes any older file with the same fingerprint, so replaced images never pile up.
Deleting a game keeps its cover, and if the game comes back it picks the cover up again.

Only game names are sent to SteamGridDB. Keep your key private: it lives only in your local
`preferences.json`, which is not part of the repository. Games are matched by name (Nexus already
handles things like `Titanfall2`, `Core_Keeper` and a trailing `-SomeSite.com` when searching),
and the options page shows which SteamGridDB title a cover came from, so a wrong match is easy to
spot and fix with **Change cover…**.

### Game names

Folder names often aren't readable (`Core_Keeper`, `BelowZero`, `Titanfall2`). In **Settings >
Game names** the rules are fixed and predictable: `-`, `_` and `.` become spaces; a space is added
before a capital letter that follows a lowercase letter, and before a number that follows a letter;
a website tag at the end (`-SomeSite.com`) is removed. One switch applies this to games found from
now on, and one button applies it to every existing game after showing you exactly what will
change (it can't be undone, since it overwrites names you may have typed). Known limit: names that
use capitals on purpose, like `DiRT` or `iRacing`, get split; rename those by hand.

### Configuring scan folders

Until the Preferences window is finished, create `UserData/preferences.json` next to the built
executable (for a debug build that's `Game Launcher/bin/Debug/net8.0-windows/UserData/`) with
the folders to scan (note the doubled backslashes, required by JSON):
```json
{
  "_roots": [ "D:\\Games", "D:\\Games\\Steam\\steamapps\\common" ],
  "_excludes": [ "D:\\Games\\Steam" ]
}
```
`Ignores` (filename/folder keywords to skip, like `setup` or `unins`) defaults to a built-in
list if you leave it out.

***

## Project structure

MVVM-organized under `Game Launcher/`:
* `Models/` — `GameMapping`, `Preferences` (plain data + persistence logic)
* `Services/` — `GameMappingManager` (scanning, caching, CRUD over mappings)
* `ViewModels/` — one VM per page/window/control, exposing bindable properties and commands
* `Views/` — the XAML pages, windows, and user controls, kept free of logic beyond wiring to
  their ViewModel
* `Resources/Styles/` — the visual theme: `Theme.xaml` holds colors, fonts and corner radii;
  `Controls.xaml` turns them into styled buttons, scrollbars, toggles, etc. To re-skin the app,
  start with `Theme.xaml`.
* `Helpers/` — small utilities, including `WindowStyler` (Windows 11 rounded corners + Mica
  backdrop via the DWM API) and `CoverArt` (generated placeholder covers)
* `Resources/Branding/` — the app icon. `nexus.svg` (with a background) and
  `nexusTransparent.svg` are the source artwork; `nexus.ico` (the `.exe`, taskbar and window icon)
  and `nexus-logo.png` (the logo drawn inside the app) are generated from them
* `Tools/` — helper scripts, currently `Generate-Icons.ps1` (rebuilds the icon files from the SVGs)

### App icon

Windows can't use an SVG as an `.exe` icon (it needs an `.ico` holding several sizes), and WPF
can't draw SVG directly, so the SVGs are converted. After editing either SVG, run this from the
repo root and rebuild:
```
powershell -ExecutionPolicy Bypass -File Tools\Generate-Icons.ps1
```
Windows caches `.exe` icons, so an old icon can linger in Explorer for a while after a rebuild
(restarting Explorer, or renaming the file, refreshes it).

The UI targets Windows 11 (Segoe UI Variable font, Segoe Fluent Icons, Mica). On Windows 10 it
still runs, but with square corners and a solid background.
