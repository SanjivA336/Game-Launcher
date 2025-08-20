# Nexus Launcher

Nexus is a **WPF-based game launcher** designed to manage and launch games organized in a folder structure. It provides **metadata editing**, **executable selection**, and **tag-based organization**, with a clean, extensible architecture built on **MVVM**.

***

## Core Features

### Library Management
* **Games Library**: Each game is represented by a `GameMapping` with name, folder path, executables, tags, and visibility.
* **Game Tiles**: Clean UI tiles for each game, showing cover image, name, and launch/options buttons.
* **Path Handling**: View a game’s folder directly or open it in Explorer for manual adjustments.

### Game Options Editing
* **Editable Metadata**: Name, folder path, executables, and tags.
* **Executable Selection**: Expandable list of detected executables with a **single-select radio style**, allowing the user to pick the primary launcher.
* **Tagging**: Built-in support for “Installed” and “Hidden” tags; extensible for custom categorization.
* **Image Support**: Load and show splash/cover images with fallback to placeholders.

### Preferences
* **Preferences Window**: Narrow, modal dialog for editing scan preferences.
* **Root Directories**: Configure library scan locations.
* **Excludes**: Exclude certain directories from scanning.
* **Ignores**: Maintain a list of keywords to skip (e.g., “redist”, “update”, “helper”).
* **Persistent Storage**: Preferences saved to JSON (`UserData/preferences.json`).

### Navigation & Workflow
* **Page-based Navigation**: `MainWindow` hosts a `Frame` for page transitions (Library, Options).
* **Save/Cancel/Reset**: Save mapping changes, cancel edits, or rescan game folders when needed.
* **Modal Settings**: Preferences opened via title bar gear icon; blocks main window until closed.
* **Data Refresh**: Saving preferences triggers a full rescan of games.

***

## Future Extensions

* **Add/Remove Games**: Manual addition/removal of games to/from the library.
* **Executable Management**: UI for **adding/removing executables** via file picker.
* **Advanced Tag Management**: Define custom tags, filtering, and search in the library view.
* **Statistics Tracking**: Play counts, last played date, playtime tracking.
* **Recommendations (Future)**: Suggested games based on history and metadata.
* **Cover / Image Integration**: Pull covers from `.exe` icons or external APIs (SteamGridDB, etc.).
* **UI Enhancements**: Improved styles, themes, drag-and-drop support, and richer interactions.

***

## Getting Started

This project is currently in **early development**.  
Game scanning, metadata editing, and preferences configuration are implemented, but features like statistics, recommendations, and advanced tagging are planned for future versions.  

At this stage, the repository is primarily focused on:
* **solidifying the core launcher flow** (scan → list → edit → save),  
* **building reusable UI components** under MVVM, and  
* **maintaining JSON-based persistence** for both games and preferences.
