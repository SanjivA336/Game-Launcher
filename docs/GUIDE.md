# Nexus Launcher: full guide

Everything each page and setting does. For the short version, see the [README](../README.md).

**Jump to:** [Library sources](#library-sources) | [Home](#home) | [Your library](#your-library) | [Apps](#apps) |
[Deals](#deals) | [Tags](#tags) | [Cover art](#cover-art) | [Cleaning up game names](#cleaning-up-game-names) |
[Where your data lives](#where-your-data-lives) | [Troubleshooting](#troubleshooting)

## Library sources

Open **Settings** (bottom of the sidebar); it opens on **Library sources**. Nothing you change here takes effect
until you press **Save**, and **Cancel** throws the changes away.

1. Press **Add app libraries** to add the game folders of your launchers (Steam, Epic, GOG and so on), and/or **Add
   folder...** (or drag folders in from Explorer) for everything else: the folders your games live in, such as
   `D:\Games`. Each game should be somewhere below one of them.
2. Press **Save and rescan**. Nexus tells you how many games it found and how many are new.

- **Excluded folders** are skipped completely, together with everything inside them. A scan folder that sits
  *inside* an excluded folder is still scanned.
- **Ignore words** mark a file or folder as "not a game" when its name contains the word. Remove any chip you don't
  want, add your own, or **Reset to defaults**. The defaults are: `redist, anti-cheat, crashreport, crashsender,
  helper, update, unins, setup, bench, anticheat, worker, agent, service, dotnet, handler, x86, 32, downloader,
  pbsvc, readme, prelaunch, cracktro`. They're kept specific on purpose, so they don't end up matching a real game's name.
- **Add app libraries** finds where your launchers keep their games and adds those folders (staged until you Save).
  Steam, Epic, GOG, Ubisoft Connect, EA app, Rockstar and Riot are supported; Battle.net, itch and Amazon Games
  don't expose an install list, so add their folders by hand. **Configure apps** jumps to the Apps tab.
- **Individually added games** is a second, separate list for a game that isn't (or shouldn't need to be) inside a
  scan folder. **Add a single game...** picks its `.exe` and adds it right away, with its own remove button, and a
  scan-folder change never touches it.
- **Rescan now** saves and scans again without changing anything, for when you've installed something new.
- **Why isn't my game found?** Press **Check a game folder...** and pick its folder; Nexus explains what stops it
  (not inside a scan folder, excluded, an ignore word, or already part of another game's folder).
- **Removing a scan folder**, or excluding one that already has games in it, can leave games behind whose folder
  Nexus no longer looks at. Saving then asks first, and names them, before removing them — games from
  **Individually added games** are never affected, since scanning never found them in the first place.

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
- **Clear filters** resets the tags. To get a different game, just press **Pick something for me** again.

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

Each card shows the game's picture, name, old and new price, store, and when the offer ends. **In your library**
marks games you already have (matched by name; a sequel isn't treated as the same game). Click a card to open its
page in your browser — Nexus only ever opens the four sites above, over https. When a game is on offer at several
stores, one card shows the best deal and notes the others (`Also on GOG`).

**The colours**

- The **percent off** and the **new price** are coloured by how good the deal is: under 30% off, under 70% off, and
  70% or more (free games included).
- The **end date** (shown as `Ends 09/24`, month/day) is grey normally, yellow when it's within a week, and red when
  it's within a day. If the source doesn't say when it ends, the card says **No Date Found**.

**Search, sort and filter** (the bar above the deals)

- **Search** finds part of a game's name.
- **Sort** by best deal (the default), name, initial or discounted price, expiry, or reduction (% or $); the arrow
  flips the direction. Deals with nothing to sort on (no end date, say) always go last.
- **All / Only free / Only paid** narrows the list.
- **Filters** opens a side panel: pick one or more **stores** (**All selected** means a game must be at every one
  you picked, good for comparing prices; **Any selected** means at least one), **hide games I already own**, and
  **clear all** to reset.
- **Pages** of 12, 24 or 48 deals, with page numbers and next/previous.

Nexus contacts these sites **only while the Deals page is open**, and keeps the answer in memory for about 30
minutes so switching pages is instant. Offline, or if a site can't be reached, you get a friendly message (or the
deals from whichever sites still answered). Prices are US dollars only.

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
([Library sources](#library-sources) above), add one and press **Save and rescan**. A folder with a
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
