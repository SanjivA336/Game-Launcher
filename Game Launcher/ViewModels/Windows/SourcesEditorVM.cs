using Game_Launcher.Helpers;
using Game_Launcher.Models;
using Game_Launcher.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    /// <summary> One folder in the scan-folder or excluded-folder list. </summary>
    internal class PathRowVM : BaseVM {
        public string Path { get; }
        public string Detail { get; }
        public bool IsMissing { get; }
        public ICommand RemoveCommand { get; }

        public PathRowVM(string path, string detail, bool isMissing, Action<PathRowVM> remove) {
            Path = path;
            Detail = detail;
            IsMissing = isMissing;
            RemoveCommand = new RelayCommand(_ => remove(this));
        }
    }

    /// <summary> One removable ignore word, drawn as a chip. </summary>
    internal class KeywordChipVM {
        public string Word { get; }
        public ICommand RemoveCommand { get; }

        public KeywordChipVM(string word, Action<KeywordChipVM> remove) {
            Word = word;
            RemoveCommand = new RelayCommand(_ => remove(this));
        }
    }

    /// <summary>
    /// The editable "Library sources" part of Settings: scan folders, excluded folders and ignore words.
    /// Everything here is STAGED: edits live in this object only, and reach preferences.json when Save is pressed.
    /// </summary>
    internal class SourcesEditorVM : BaseVM {

        private readonly Func<string, string?> _pickFolder;
        private readonly List<string> _roots;
        private readonly List<string> _excludes;
        private readonly List<string> _ignores;
        private readonly string _originalSnapshot;
        private readonly List<string> _knownGameFolders;

        public ObservableCollection<PathRowVM> Roots { get; } = new();
        public ObservableCollection<PathRowVM> Excludes { get; } = new();
        public ObservableCollection<KeywordChipVM> Keywords { get; } = new();

        private string _notice = string.Empty;
        /// <summary> A short message about the last thing the user did ("Added 2 Steam libraries", "Already in the list"...). </summary>
        public string Notice {
            get => _notice;
            private set { _notice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNotice)); }
        }
        public bool HasNotice => _notice.Length > 0;

        private string _newKeyword = string.Empty;
        public string NewKeyword {
            get => _newKeyword;
            set { _newKeyword = value; OnPropertyChanged(); }
        }

        public bool HasNoRoots => Roots.Count == 0;

        /// <summary> True when scan folders, excluded folders or ignore words differ from what was saved. </summary>
        public bool HasChanges => Snapshot() != _originalSnapshot;

        #region "Why isn't my game found?"
        public ObservableCollection<string> DiagnosticLines { get; } = new();

        private string _diagnosticTitle = string.Empty;
        public string DiagnosticTitle {
            get => _diagnosticTitle;
            private set { _diagnosticTitle = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDiagnostic)); }
        }
        public bool HasDiagnostic => _diagnosticTitle.Length > 0;

        private bool _diagnosticFound;
        public bool DiagnosticFound {
            get => _diagnosticFound;
            private set { _diagnosticFound = value; OnPropertyChanged(); }
        }
        #endregion

        public ICommand AddRootCommand { get; }
        public ICommand AddExcludeCommand { get; }
        public ICommand AddKeywordCommand { get; }
        public ICommand ResetKeywordsCommand { get; }
        public ICommand AddAppLibrariesCommand { get; }
        public ICommand AddSingleGameCommand { get; }
        public ICommand ConfigureAppsCommand { get; }
        public ICommand WhyNotFoundCommand { get; }

        private readonly Func<IReadOnlyList<LauncherLibrary>> _findLibraries;
        private readonly Func<string, string?> _pickExecutable;

        /// <param name="findLibraries"> Finds the folders your launchers keep games in. Replaceable in tests.</param>
        /// <param name="openApps"> Takes the user to the Apps tab (the "Configure apps" link).</param>
        /// <param name="pickExecutable"> Shows a file picker filtered to .exe, for "Add a single game...". Replaceable in tests.</param>
        public SourcesEditorVM(Preferences prefs, Func<string, string?>? pickFolder = null, Func<IReadOnlyList<LauncherLibrary>>? findLibraries = null, Action? openApps = null, Func<string, string?>? pickExecutable = null) {
            _pickFolder = pickFolder ?? FolderPicker.Pick;
            _findLibraries = findLibraries ?? LauncherLibraries.FindOnThisPc;
            _pickExecutable = pickExecutable ?? ExecutablePicker.Pick;
            _roots = prefs._roots.ToList();
            _excludes = prefs._excludes.ToList();
            _ignores = prefs.Ignores.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            _originalSnapshot = Snapshot();

            // Games the library already knows, used for the "N games" figure on each scan folder
            _knownGameFolders = GameMappingManager.LoadMappings().Select(m => m.DirPathRaw).ToList();

            AddRootCommand = new RelayCommand(_ => {
                string? picked = _pickFolder("Choose a folder that contains your games");
                if (picked is not null) {
                    AddRootPath(picked);
                }
            });
            AddExcludeCommand = new RelayCommand(_ => {
                string? picked = _pickFolder("Choose a folder for Nexus to skip");
                if (picked is not null) {
                    AddExcludePath(picked);
                }
            });
            AddKeywordCommand = new RelayCommand(_ => AddKeyword());
            ResetKeywordsCommand = new RelayCommand(_ => ResetKeywords());
            AddAppLibrariesCommand = new RelayCommand(_ => AddAppLibraries());
            AddSingleGameCommand = new RelayCommand(_ => AddSingleGame());
            ConfigureAppsCommand = new RelayCommand(_ => openApps?.Invoke());
            WhyNotFoundCommand = new RelayCommand(_ => {
                string? picked = _pickFolder("Choose the folder of the game that isn't showing up");
                if (picked is not null) {
                    Diagnose(picked);
                }
            });

            RebuildAll();
        }

        #region Editing
        /// <summary> Adds a scan folder (after the Steam-folder check). Used by the button, drag-and-drop and Steam detection. </summary>
        public void AddRootPath(string path) {
            if (!Directory.Exists(path)) {
                Notice = "That isn't a folder. Drop or choose a folder, not a file.";
                return;
            }

            var normalized = SourceRules.NormalizeNewRoot(path);
            if (_roots.Contains(normalized.Path, StringComparer.OrdinalIgnoreCase)) {
                Notice = "That folder is already in the list.";
                return;
            }

            _roots.Add(normalized.Path);
            Notice = normalized.Notice ?? $"Added {normalized.Path}.";
            RebuildRoots();
        }

        public void AddExcludePath(string path) {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (_excludes.Contains(full, StringComparer.OrdinalIgnoreCase)) {
                Notice = "That folder is already excluded.";
                return;
            }

            _excludes.Add(full);
            Notice = $"Nexus will skip {full} and everything inside it.";
            RebuildExcludes();
        }

        private void RemoveRoot(PathRowVM row) {
            _roots.RemoveAll(r => r.Equals(row.Path, StringComparison.OrdinalIgnoreCase));
            Notice = string.Empty;
            RebuildRoots();
        }

        private void RemoveExclude(PathRowVM row) {
            _excludes.RemoveAll(r => r.Equals(row.Path, StringComparison.OrdinalIgnoreCase));
            Notice = string.Empty;
            RebuildExcludes();
        }

        private void AddKeyword() {
            string word = NewKeyword.Trim().ToLowerInvariant();
            if (word.Length == 0) {
                return;
            }

            if (_ignores.Contains(word, StringComparer.OrdinalIgnoreCase)) {
                Notice = $"\"{word}\" is already in the list.";
            }
            else {
                _ignores.Add(word);
                _ignores.Sort(StringComparer.OrdinalIgnoreCase);
                Notice = string.Empty;
                RebuildKeywords();
            }
            NewKeyword = string.Empty;
        }

        private void RemoveKeyword(KeywordChipVM chip) {
            _ignores.RemoveAll(k => k.Equals(chip.Word, StringComparison.OrdinalIgnoreCase));
            Notice = string.Empty;
            RebuildKeywords();
        }

        private void ResetKeywords() {
            _ignores.Clear();
            _ignores.AddRange(new Preferences().Ignores.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
            Notice = "Ignore words reset to the defaults.";
            RebuildKeywords();
        }

        // Adds the folders your launchers keep games in (Steam, Epic, GOG, Ubisoft Connect, EA app, Rockstar, Riot).
        // It doesn't matter whether the launcher is linked on the Apps tab: the folders are read from the launcher's own records.
        private void AddAppLibraries() {
            var found = _findLibraries();
            if (found.Count == 0) {
                Notice = "Couldn't find any launcher libraries on this PC. You can still add folders by hand.";
                return;
            }

            var added = found.Where(f => !_roots.Contains(f.Path, StringComparer.OrdinalIgnoreCase)).ToList();
            if (added.Count == 0) {
                Notice = "All the folders your launchers use are already in the list.";
                return;
            }

            _roots.AddRange(added.Select(a => a.Path));
            Notice = (added.Count == 1 ? "Added 1 folder from your launchers:" : $"Added {added.Count} folders from your launchers:")
                     + string.Concat(added.Select(a => $"{Environment.NewLine}{a.Launcher}: {a.Path}"));
            RebuildRoots();
        }

        // Adds one game straight from its .exe, for something that isn't inside (or shouldn't need) a whole scan folder. Unlike
        // everything else here, this is saved immediately: there is no separate "Save" step, the same as picking a cover.
        private void AddSingleGame() {
            string? picked = _pickExecutable("Choose the game's .exe");
            if (picked is null) {
                return;
            }

            if (!GameMappingManager.AddSingleGame(picked, Preferences.Load(), out var added, out string? error)) {
                Notice = error ?? "Couldn't add that game.";
                return;
            }

            _knownGameFolders.Add(added!.DirPathRaw);
            Notice = $"Added \"{added.Name}\". It doesn't need to be in a scan folder, and won't be removed if you change one.";
            RebuildRoots(); // updates a root's game count, in case the exe happened to already be inside one
        }

        private void Diagnose(string folder) {
            var report = ScanDiagnostics.Explain(folder, BuildPreferences(new Preferences()));
            DiagnosticLines.Clear();
            foreach (string line in report.Lines) {
                DiagnosticLines.Add(line);
            }
            DiagnosticTitle = folder;
            DiagnosticFound = report.Found;
        }
        #endregion

        /// <summary> Copies the staged lists into the given preferences (used when saving, and for the diagnostic). </summary>
        public Preferences BuildPreferences(Preferences target) {
            target.SetRoots(_roots);
            target.SetExcludes(_excludes);
            target.SetIgnores(_ignores);
            return target;
        }

        #region Rebuilding the lists
        private void RebuildAll() {
            RebuildRoots();
            RebuildExcludes();
            RebuildKeywords();
        }

        private void RebuildRoots() {
            var counts = SourceRules.CountGamesPerRoot(_roots, _knownGameFolders);
            Roots.Clear();
            foreach (string root in _roots) {
                bool missing = !Directory.Exists(root);
                int count = counts.GetValueOrDefault(root);
                string detail = missing ? "Folder not found (is the drive connected?)" : count == 1 ? "1 game" : $"{count} games";
                Roots.Add(new PathRowVM(root, detail, missing, RemoveRoot));
            }
            OnPropertyChanged(nameof(HasNoRoots));
            OnPropertyChanged(nameof(HasChanges));
        }

        private void RebuildExcludes() {
            Excludes.Clear();
            foreach (string exclude in _excludes) {
                bool missing = !Directory.Exists(exclude);
                Excludes.Add(new PathRowVM(exclude, missing ? "Folder not found" : string.Empty, missing, RemoveExclude));
            }
            OnPropertyChanged(nameof(HasChanges));
        }

        private void RebuildKeywords() {
            Keywords.Clear();
            foreach (string word in _ignores) {
                Keywords.Add(new KeywordChipVM(word, RemoveKeyword));
            }
            OnPropertyChanged(nameof(HasChanges));
        }

        // A comparable string of everything editable here, so "has anything changed?" is a single comparison
        private string Snapshot() {
            static string Join(IEnumerable<string> items) => string.Join("|", items.Select(i => i.ToLowerInvariant()).OrderBy(i => i, StringComparer.Ordinal));
            return Join(_roots) + "##" + Join(_excludes) + "##" + Join(_ignores);
        }
        #endregion
    }
}
