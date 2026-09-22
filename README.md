# Nexus Launcher

**One fast, offline library for all the PC games on your computer, no matter which launcher installed them.**

Nexus scans the folders you point it at, works out which folders are games and which `.exe` starts each one,
and shows everything as a single cover-art library. There is no account, no sign-in and no store client:
it opens in about a second and works with no internet connection.

> **Version 1.0.** It is usable day to day, but it is a young project with rough edges (see
> [Limitations](#limitations)). Windows only.

## Download

**[Download Nexus for Windows](https://github.com/SanjivA336/Game-Launcher/releases/latest/download/Nexus-Launcher.exe)**
(one file, about 70 MB). Save it anywhere and double-click it. There is nothing to install and no .NET to set up.

Because the file isn't code-signed, Windows may say "Windows protected your PC". Choose **More info**, then **Run
anyway**. If you'd rather not trust a downloaded exe, you can [build it yourself](#get-it-running) from the source.

**Jump to:** [Part 1: What is Nexus](#part-1-what-is-nexus) | [Part 2: Using Nexus](#part-2-using-nexus)

---

# Part 1: What is Nexus

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

**Home**
- The start page. **Continue playing** shows your most recently launched games in one row (as many as fit the
  window), ending with a **See all your games** card that opens the Library
- **Pick something for me** chooses an installed game at random, with its own filters and an optional lean towards
  games you play more (or less) often
- Click the Nexus logo in the sidebar to get back to it from anywhere

**Library**
- Finds games automatically by scanning folders you choose (edited right in Settings), ignoring installers,
  updaters, crash reporters and similar clutter
- A cover-art grid that resizes with the window, with live search
- **Sorting:** name (A to Z or Z to A), recently played, most played, recently added. Games with no value (for
  example never played) always sort last
- **Tags that describe how a game works and where it came from**, in groups: Status, Source (which launcher),
  Completion, Connection, Players, Structure, Genre and Features. Nexus works out Status and Source by itself, and
  can suggest the rest from Steam (see Tags below)
- **A filter panel:** pick one or several tags and choose whether a game needs **any** of them (OR) or **all**
  of them (AND). Each tag shows how many games have it
- Hide games you don't want to see (they reappear under the Hidden filter). There is no "delete a game": hiding
  does the same job and is undone with one click

**Each game**
- Launch with one click on its cover. It starts the game's own executable in the game's own folder
- Rename it (or press **Clean name**), choose which executable is the main one (or add one Nexus missed), edit its tags
  (or press **Add suggested tags**), open its folder
- **Moved a game to another drive?** Point Nexus at the new folder and it keeps the name, tags, play history and cover
- Play history: when you last launched it and how many times (only launches made through Nexus are counted)

**Library sources (Settings)**
- Add scan folders with a folder picker or by dragging them in; each shows how many games it holds and warns if
  it can't be found (for example an unplugged drive)
- **Add app libraries** adds the game folders of your launchers in one click (Steam, Epic, GOG, Ubisoft Connect, EA app,
  Rockstar, Riot), and picking Steam's own folder sends you to the right one
- **Individually added games** is a second, separate list, for something that isn't (or shouldn't need to be) inside
  a scan folder — say a launcher that shares a folder with unrelated programs. **Add a single game...** picks its
  `.exe` and adds it immediately; each one gets its own row here, with its own remove button, and a scan-folder
  change never touches it
- Excluded folders, and ignore words shown as chips you can remove (or reset to the defaults)
- **Save and rescan** tells you how many games were found, how many are new and which folders couldn't be read. If a
  scan folder was removed, or a folder with games in it was excluded, it asks before removing those games too
- **Why isn't my game found?** Pick a game's folder and Nexus explains which rule stops it

**Apps (a tab in Settings)**
- Lists every launcher Nexus knows (Steam, Epic, GOG Galaxy, Ubisoft Connect, EA app, Battle.net, Amazon Games,
  itch, Riot Client, Rockstar, Playnite, Heroic, Minecraft Launcher) and you choose which to **link**, a bit like
  the connections list in Discord (though nothing is actually connected to the launcher)
- A launcher Nexus finds links in one click; one it can't find (or finds wrongly) you point at yourself with **Browse...**
- **Add custom app...** (or drag a program or shortcut onto the tab) links anything else
- Linked apps show their real icon and have a Launch button; unlinking only forgets the shortcut

**Deals**
- Free games and discounts from Steam, the Epic Games Store, GamerPower (giveaways on GOG, itch.io, IndieGala and
  others) and CheapShark, with prices, the discount, the end date and the store it's on offer at (US dollars)
- Sort (initial price, discounted price, when it expires, reduction in percent or dollars, name), filter (store,
  free or paid, hide games you own), search, and page through the results
- The percent and the new price are coloured by how good the deal is, and the end date turns yellow within a week
  and red within a day
- Games you already own are marked. Clicking a deal opens its web page in your browser
- On by default and switchable off in Settings. It only goes online while the page is open; pictures are shown
  live and never saved

**Cover art**
- Covers come from Steam automatically, with no key needed. A free [SteamGridDB](https://www.steamgriddb.com) key lets
  you pick other covers yourself (search SteamGridDB in a popup) and fills in games Steam has no cover for. You can
  also upload your own image
- Covers are saved once in Nexus's own folder, so the library stays fast and works offline afterwards
- Games without a cover get a generated colored placeholder

**Names**
- Turns folder-style names into readable ones (`Core_Keeper` becomes `Core Keeper`, `Titanfall2` becomes
  `Titanfall 2`) using simple fixed rules, either for new games only or for everything (after a preview)

**The app itself**
- A Windows 11 style dark interface: rounded corners, the translucent Mica backdrop, Segoe UI Variable text
- A sidebar that collapses to a slim icon strip. Its entries (Library, Deals) come from a single list of
  pages in the code, and a fork can add a page of its own with one small class file (see "Adding your own page" below)

## How it works

1. **Scan.** Starting from your scan folders, Nexus walks the folder tree one level at a time looking for `.exe`
   files. As soon as a folder contains a usable executable it treats that folder as a game and doesn't look
   deeper. Files and folders that match an ignore list (`setup`, `unins`, `redist`, `crash`...) are skipped.
2. **Map.** Each game folder becomes an entry: a name, the folder, the executables found, and which one is the
   main one.
3. **Remember.** Entries are saved to a small JSON file in Nexus's own data folder, together with everything you
   add: tags, renames, play history and cover choices. Nexus scans again every time it starts (about a second for
   a few dozen games), or when you press **Save and rescan** / **Rescan now** in Settings, and merges in anything
   new without touching what you've already set.
4. **Show and launch.** The library is drawn from that saved data, and launching starts the chosen `.exe`.

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

---

# Part 2: Using Nexus

## What you need

- **Windows 11**, 64-bit (Windows 10 should run it, with plainer window effects, but hasn't been tested)
- To run the download: nothing else. It already contains everything it needs.
- To build it yourself: the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Get it running

**The easy way:** use the [download](#download) at the top, or the newest file on the
[Releases page](https://github.com/SanjivA336/Game-Launcher/releases). To update, download the new file and replace the
old one; your library isn't stored next to the exe, so nothing is lost.

**Building it yourself:** clone the repository, then use whichever you like.

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
[Where your data lives](#where-your-data-lives)).

**Making a release** (for the maintainer): raise `Version` in `Game Launcher/Game Launcher.csproj`, run the publish
command above, then create a GitHub release and attach the exe as `Nexus-Launcher.exe`. The Download link at the top
always points at the newest release's file of that name.

## First run: tell Nexus where your games are

The first time you start Nexus the library is empty, because it doesn't know where your games are. Open
**Settings** (bottom of the sidebar). It opens on **Library sources**:

1. Press **Add app libraries** to add the game folders of your launchers (Steam, Epic, GOG and so on), and/or **Add folder...** (or drag folders in
   from Explorer) for everything else: the folders your games live in, such as `D:\Games`. Each game should be
   somewhere below one of them.
2. Press **Save and rescan**. Nexus tells you how many games it found and how many are new.

Nothing you change in Settings takes effect until you press Save, and **Cancel** throws the changes away.

- **Excluded folders** are skipped completely, together with everything inside them. A scan folder that sits
  *inside* an excluded folder is still scanned.
- **Ignore words** mark a file or folder as "not a game" when its name contains the word. Remove any chip you don't
  want, add your own, or **Reset to defaults**. The defaults are: `redist, anti-cheat, crashreport, crashsender,
  helper, update, unins, setup, bench, anticheat, worker, agent, service, dotnet, handler, x86, 32, downloader,
  pbsvc, readme, prelaunch, cracktro`. They're chosen to be specific: a bare `crash` or `trial` would also skip
  real games (Crash Bandicoot, the Trials series), so those are narrowed or left out.
- **Add app libraries** reads where your launchers keep their games and adds those folders to the list (staged until
  you Save). It works whether or not the launcher is linked on the Apps tab. Steam, Epic Games, GOG, Ubisoft Connect,
  EA app, Rockstar and Riot are supported; Battle.net, itch and Amazon Games keep their install list in private
  databases, so add their folders by hand. Folders that are a whole drive, Program Files or similar are never added.
  **Configure apps** next to it jumps to the Apps tab.
- **Individually added games** is its own card, below Scan folders, listing every game you added this way. **Add a
  single game...** picks one `.exe` and adds it right away — no Save needed, the same as changing a cover. The game
  doesn't need to be inside a scan folder at all, and a scan-folder change never removes it (see below). Picking a
  folder already in your library shows an error instead of adding it again. Each row has its own remove button:
  unlike everything else in Settings, that deletes the game immediately too, since nothing would ever add it back.
- **Rescan now** saves and scans again without changing anything, for when you've installed something new.
- **Why isn't my game found?** Press **Check a game folder...**, pick the game's folder, and Nexus explains what
  stops it: not inside any scan folder, excluded, skipped by an ignore word, or already part of another game's
  folder. It uses the settings on screen, including ones you haven't saved yet.
- **Removing a scan folder** (or excluding one that already has games in it) can leave games behind whose folder
  Nexus no longer looks at. Saving then asks first, and names them, before removing them from your library — a game
  from **Individually added games** is never affected, since it was never found by scanning.

**Good to know about scanning**

- A game's name comes from the folder directly under its scan folder, so `D:\Games\Foo\bin\Foo.exe` is called
  `Foo`. Steam's layout (`...\steamapps\common\Foo`) is recognised whichever folder you used.
- A launcher's own folder usually has an `.exe` of its own (`steam.exe`), and Nexus stops looking deeper once a
  folder has one. So if you add Steam's own folder (or a Steam library folder) Nexus swaps in its
  `steamapps\common` folder for you and tells you it did.
- Nexus picks the first `.exe` it finds as the main one. If a game starts the wrong thing, open the game and
  choose a different executable.
- Folders that are on a drive that isn't connected are shown with a warning; the games in them stay in your
  library as "Not installed" until the drive is back.

## Home

Nexus opens on **Home**; click the Nexus logo and name in the sidebar to come back to it.

**Continue playing** lists the games you launched most recently (launches made through Nexus), newest first, in a
single row. How many show depends on the width of the window: it fits as many cards as it can and always ends with a
dashed **See all your games** card, which opens the Library. Make the window wider and more games appear; narrower
and fewer do.

**Pick something for me** picks a random installed game. It has its own settings on the right of the section, which
never change what the Library page shows:

- **Lean towards** decides how the dice are weighted. **Even** gives every game the same chance. **More played**
  makes games you launch often more likely (each launch adds to a game's weight). **Less played** does the opposite,
  so a game you've never launched is the most likely.
- **Only pick games with** works like the Library's tag filter: choose tags (Solo, Co-op, VR, Never played...)
  and whether a game needs **any** or **all** of them. A line next to the button shows how many installed games match.
- **Pick again** gives a different game whenever there is one; **Clear filters** resets the tags.

Hidden and not-installed games are never picked.
## Your library

- **Launch** a game by clicking its cover (a play button appears on hover). Click the name area to open the
  game's options.
- **Search** with the box at the top: it matches any part of a name.
- **Sort** with the dropdown, and flip the direction with the arrow button next to it.
- **Filter** with the **Filters** button, which opens a panel on the right. Select tags, and choose **Any tag**
  (a game needs at least one) or **All tags** (a game needs every one). The button shows how many tags are
  selected; "Clear all" resets them.
- **The sidebar** collapses with the chevron next to the Nexus name. **Settings** is at the bottom.

### A game's options

Open it by clicking the name area of a game's card.

| You can | How |
| --- | --- |
| Rename the game | Edit the Name box (or press **Clean name**), then **Save** |
| Choose which `.exe` launches it | Pick one in the list (it is marked **Default**), or **Add** one; **Remove** drops the selected one |
| Set tags | Click the chips, or press **Add suggested tags** to fill them in from Steam first (see Tags) |
| Hide it (there is no delete) | Use the **Hidden** switch. Hidden games only show while the **Hidden** filter is on |
| Open its folder | **Open folder** |
| Tell Nexus the game moved | **Change folder...**, then pick where it is now (see below) |
| Change its cover | **Change cover...** (see below) |
| Undo your edits and re-scan the folder | **Reset**, then **Save** |

**Save** keeps your changes and returns to the library; **Cancel** discards them. Cover changes and **Change
folder** are the exceptions: they are saved the moment you apply them.

**Change folder:** if you moved a game (to another drive, say), pick its new folder. Nexus keeps the game's name,
tags, play history, hidden state and cover, and re-reads the executables from the new folder (it keeps the main
`.exe` you chose when the new folder has one with the same name, and any extra ones you added). If you pick a folder
that holds one game in a sub-folder, it uses that sub-folder. It refuses when the folder has no launchable `.exe`,
holds several games, or is already used by another game in your library. Nexus never moves the files itself.

## Apps

Open **Settings > Apps** to link your game launchers, so they are one click from starting. Changes here are saved
straight away (there is nothing to Save), and Nexus never changes the programs themselves.

- **Launchers** lists every launcher Nexus knows: Steam, Epic Games Launcher, GOG Galaxy, Ubisoft Connect, EA app,
  Battle.net, Amazon Games, itch, Riot Client, Rockstar Games Launcher, Playnite, Heroic and the Minecraft Launcher.
  Each row is in one of three states:
  - **Found**: Nexus found it on this PC (usual install folders, Windows' installed-programs list, Start Menu
    shortcuts). Press **Link** to link it, or **Browse...** if that isn't the program you meant.
  - **Not found**: press **Browse...** and choose its program or a shortcut to it.
  - **Linked**: shows its icon and path, with buttons to **launch** it, **change the program** it points to, and
    **unlink** it. If the program has gone missing it is dimmed with a "Not found" note.
- **Other apps** holds anything else you want one click away. **Add custom app...** takes a program (`.exe`) or a
  shortcut (`.lnk`, followed to the program it starts, keeping its arguments), and you can drop them onto the tab
  too. Custom apps can be renamed, pointed somewhere else and unlinked.
- Unlinking only forgets the shortcut. A launcher that is installed simply goes back to **Found**.
## Deals

The **Deals** page shows what's free or discounted right now:

| Source | What it provides |
| --- | --- |
| Steam | Current specials |
| Epic Games Store | The weekly free games and other running promotions |
| GamerPower | Free giveaways on GOG, itch.io, IndieGala, Steam, Ubisoft and others |
| CheapShark | Top discounts across Steam, GOG, Epic, Humble, Ubisoft and Fanatical |

Each card shows the game's picture, its name, the normal price (struck through) and the new price, the store, and
when the offer ends. **In your library** marks games you already have (matched by name; a sequel isn't treated as the
same game). Click a card to open its page in your browser: Nexus only ever opens web addresses on those four sites,
over https. The **store** on a card is where the game is actually on offer (Steam, GOG, IndieGala...), with a note
like "via GamerPower" saying which list it came from. When the same game is on offer at several stores, one card
shows the best offer and adds a line such as "Also on GOG".

**The colours**

- The **percent off** and the **new price** are coloured by how good the deal is: under 30% off, under 70% off, and
  70% or more (free games included).
- The **end date** (shown as `Ends 09/24`, month/day) is grey normally, yellow when it's within a week, and red when
  it's within a day. If the source doesn't say when it ends, the card says **No Date Found**.

**Search, sort and filter** (the bar above the deals)

- **Search** finds part of a game's name.
- **Sort** by Best deal (the default: free first, then the biggest discount), Name, Initial price, Discounted price,
  Expires, Reduction % or Reduction $. The arrow button flips the direction, and each sort starts in its natural
  direction. Deals with nothing to sort on (for example no end date) always go last.
- **All / Only free / Only paid** switches between everything, free games and paid deals.
- **Filters** opens a side panel:
  - **Stores**: pick one or more. With **All selected** (the default) a game must be on offer at *every* store you
    picked, which is handy for comparing prices; with **Any selected** it can be at any of them. Each store shows how
    many games it has.
  - **Hide games I already own**.
  - **Clear all** resets the panel.
- **Pages:** deals are shown 12, 24 or 48 at a time (change it under the list), with page numbers and next/previous
  buttons. Changing a search, sort or filter goes back to page 1.

Nexus contacts these sites **only while the Deals page is open**, keeps the answer in memory for about 30 minutes
(so switching pages is instant) and shows pictures without saving them anywhere. If you're offline you'll see a
friendly message; if only some sites can't be reached, you get the rest plus a note saying which are missing.
The same game at the same store listed by two sources appears once. Prices are in US dollars.

Don't want it? **Settings > General > Show the Deals page** hides the page from the sidebar, and then nothing is
ever fetched.
## Tags

Tags are grouped so the filter panel stays readable:

| Group | Tags | Who sets it |
| --- | --- | --- |
| Status | Installed, Not installed, Never played, Recently added (last 14 days), Hidden | Nexus |
| Source | Steam, Epic Games, GOG, Ubisoft Connect, EA app, Riot Games, Rockstar Games, Amazon Games, Other | Nexus, from where the game is installed |
| Completion | In Progress, Finished (only shown for Campaign games) | You. Launching a Campaign game marks it In Progress. Pick one |
| Connection | Online, Offline (includes local play) | You or Steam. A game can have both |
| Players | Solo, Co-op, Competitive, MMO, Party, Local | You or Steam |
| Structure | Campaign, Roguelike, Open world, Sandbox | You |
| Genre | Action, Adventure, RPG, Strategy, Simulation, Survival, Racing, Sports, Shooter, Puzzle, Platformer, Horror | You or Steam |
| Features | Controller, VR | You or Steam |

Older tags are carried over (Single-player becomes Solo, Multiplayer becomes Online).

**Add suggested tags** (in a game's options) looks the game up on Steam and turns on the tags it finds. It only ever
adds tags, never removes yours, and nothing is kept until you press **Save**. **Settings > General > Game tags >
Add suggested tags to all games** does the same for the whole library in the background (about a game and a half per
second, with a **Cancel** button) and lists which games were updated and which weren't found on Steam.

Steam games are matched exactly using the app number in their local install record; other games by a close name match.
The game's name (and that number) is what gets sent to Steam.

## Cover art

Covers come from Steam's public image servers, so they work without any setup. Steam's portrait art belongs to the
game's publisher; Nexus only downloads it once for your own library and never redistributes it. Some games have no Steam
portrait, and then Nexus falls back to [SteamGridDB](https://www.steamgriddb.com) (a community database of game art) if
you have added a free key, or shows a placeholder.

**Optional SteamGridDB key:** it lets you pick other covers and fills in games Steam doesn't have.

1. Create a free account on steamgriddb.com and copy your API key from your profile preferences (the API page).
   **Get a free key** in Settings opens that page.
2. In Nexus open **Settings > General**, paste it into **Cover art**, and press **Test key** to check it, then **Save**.

**When Nexus looks for a cover:** only when a game has just been added, renamed (and saved), or reset (and saved).
Starting Nexus never triggers lookups, a game with no match isn't asked about again, and a cover you chose yourself
is never replaced. If you're offline when a lookup is due, it is kept for next time.

**Choosing a cover yourself:** in a game's options press **Change cover...**. Either search SteamGridDB (pick a game
from the results, then a cover) or upload an image (PNG, JPG, BMP, GIF or TIFF). Nexus saves a resized copy and
never touches your original. **Use automatic cover** gives it back to Nexus.

Covers are matched by name, and the options page shows where a cover came from (Steam or SteamGridDB) and which title
it matched, so a wrong match is easy to spot. Only the game's name (and a Steam app number) is sent, and your key stays
in your own `preferences.json`.

## Cleaning up game names

Folder names are often ugly (`Core_Keeper`, `BelowZero`, `Titanfall2`). In **Settings > General > Game names**:

- **Clean up names of newly found games** (on by default) cleans a name when a game is first found.
- **Clean up all existing names...** shows you what would change and asks first, because it overwrites names you
  may have typed and can't be undone. Afterwards it lists the games it renamed.
- In a single game's options, **Clean name** fills the Name box with the cleaned name, and you keep it with **Save**.

The rules are fixed: `-`, `_` and `.` become spaces; a space goes before a capital letter that follows a lowercase
letter, and before a number that follows a letter; a trailing website tag (`-SomeSite.com`) is removed. Names that
use capitals on purpose (`DiRT`, `iRacing`) get split, so rename those by hand.

## Where your data lives

Everything Nexus saves is in one folder:

```
%LOCALAPPDATA%\Nexus Launcher        (for example C:\Users\you\AppData\Local\Nexus Launcher)
```

| File | What it holds |
| --- | --- |
| `preferences.json` | Scan folders, excluded folders, ignore words, your API key, the name clean-up and Deals switches |
| `mappings.json` | Your games: names, executables, tags, play history, cover details |
| `apps.json` | The Apps tab: your linked launchers and custom apps |
| `Covers/` | Downloaded and uploaded cover images |
| `crash.log` | Only exists if something unexpected went wrong: the details, so a problem can be described or reported |

It is per-user (not next to the exe), so moving, rebuilding or deleting the exe never loses your library. It is
"Local" rather than "Roaming" because it contains folder paths that only make sense on this PC.

**You normally never need to open this folder.** The only times you would:

- **Backing up**: copy the folder. **Moving to another PC**: copy it to the same place there (and check the scan
  folders in Settings still exist).
- **Starting over**: delete `mappings.json` (the next start re-scans; you lose tags, renames and history, and the
  cover files stay), or the whole folder to reset everything. Close Nexus first.

Settings > General > **Your data** shows the exact path and has an **Open folder** button. If a JSON file is ever damaged,
Nexus sets it aside as `*.json.bad` instead of crashing.

**Coming from an earlier version?** Older builds kept a `UserData` folder next to the exe. The first time the new
version starts, it copies that folder to the location above (once, and never over data that's already there). The
old folder is left alone, so you can delete it once you're happy.

**Advanced:** set the environment variable `NEXUS_DATA_DIR` to a folder path before starting Nexus to use a
different data folder instead. This is handy for keeping a separate test library.

## Troubleshooting

**The library is empty.** Nexus needs at least one scan folder: open Settings > Library sources
([First run](#first-run-tell-nexus-where-your-games-are)), add one and press **Save and rescan**. A folder with a
yellow warning couldn't be found.

**A game is missing.** Use **Settings > Library sources > Why isn't my game found?** and pick its folder; it names
the rule that stops it. Usually the folder isn't below a scan folder, is excluded, has an ignore word in its name,
or sits inside another game's folder (a folder with an `.exe` of its own stops the search from going deeper).

**A game is called `bin` or the wrong thing.** That's the folder-name rule; rename it in the game's options, or add
the folder one level up as a scan folder.

**The wrong program launches.** Open the game and select a different executable.

**A game says "Not installed".** Its folder wasn't found: the drive is unplugged, it was uninstalled, or you moved
it. If you moved it, use **Change folder...** in its options. Otherwise it comes back automatically when the folder
exists again at the next scan.

**Deals shows "You're offline" or a note about missing sources.** Nexus couldn't reach those sites (no internet, or
the site is down or has changed). Press the refresh button later; your library is unaffected.

**A launcher shows as "Not found" on the Apps tab.** Press **Browse...** on its row and choose its `.exe` or its Start
Menu shortcut. A launcher that isn't in the list at all can be linked with **Add custom app...**.

**No covers appear.** Covers come from Steam and are looked up only when a game is added, renamed or reset, and only
while you are online. Steam doesn't have a portrait for every game: for those, add a free SteamGridDB key in Settings
(it fills in the gaps), or open the game and use **Change cover...** to pick or upload one yourself. A message under the
game count explains connection problems.

**Something went wrong and a message appeared.** Nexus keeps going and saved the details to `crash.log` in its data
folder (see [Where your data lives](#where-your-data-lives)). Your games and settings are safe: they are saved in a way
that a crash can't half-write.

**The `.exe` icon looks old.** Windows caches icons; restarting Explorer refreshes it.

---

## Contributing and rebuilding the icon

Issues and pull requests are welcome. The app icon is generated from `Game Launcher/Resources/Branding/*.svg`; after
editing them, run `powershell -ExecutionPolicy Bypass -File Tools\Generate-Icons.ps1` (it uses Microsoft Edge to
render the SVGs) and rebuild. When testing, set `NEXUS_DATA_DIR` to a scratch folder so you don't touch your own
library.

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
