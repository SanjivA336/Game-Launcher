using Game_Launcher.Helpers;
using Game_Launcher.Models;
using Game_Launcher.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;

namespace Game_Launcher.ViewModels.Pages {

    /// <summary> One of Nexus's known launchers on the Apps tab. It can be linked, found (but not linked yet), or not found. </summary>
    internal class KnownAppRowVM : BaseVM {
        private readonly AppsVM _owner;

        public KnownAppInfo Info { get; }

        /// <summary> The saved link, when this launcher is linked. </summary>
        public AppEntry? Entry { get; set; }

        /// <summary> Where Nexus found the launcher on this PC, when it did. </summary>
        public string? DetectedPath { get; set; }

        public string Name => Info.Name;

        public bool IsLinked => Entry is not null;
        public bool IsFound => Entry is null && DetectedPath is not null;
        public bool IsNotFound => Entry is null && DetectedPath is null;

        /// <summary> Linked, but the program isn't at the linked path any more (uninstalled, or its drive is unplugged). </summary>
        public bool IsMissing => Entry is not null && !Entry.Exists;

        private string? ShownPath => Entry?.ExePath ?? DetectedPath;

        public ImageSource? Icon => ShownPath is null ? null : IconExtractor.For(ShownPath);
        public bool HasIcon => Icon is not null;

        /// <summary> The line under the name. </summary>
        public string Detail => IsLinked ? (IsMissing ? $"Not found: {Entry!.ExePath}" : Entry!.ExePath)
                                : IsFound ? $"Found: {DetectedPath}"
                                : _owner.IsDetecting ? "Looking on this PC…" : "Not found on this PC";

        public ICommand LinkCommand { get; }
        public ICommand BrowseCommand { get; }
        public ICommand UnlinkCommand { get; }
        public ICommand LaunchCommand { get; }

        public KnownAppRowVM(KnownAppInfo info, AppsVM owner) {
            Info = info;
            _owner = owner;
            LinkCommand = new RelayCommand(_ => _owner.Link(this));
            BrowseCommand = new RelayCommand(_ => _owner.Browse(this));
            UnlinkCommand = new RelayCommand(_ => _owner.Unlink(this));
            LaunchCommand = new RelayCommand(_ => _owner.Launch(Entry!), _ => Entry is not null);
        }

        /// <summary> Re-reads everything shown, after the link or the detection changed. </summary>
        public void Refresh() {
            foreach (string property in new[] { nameof(IsLinked), nameof(IsFound), nameof(IsNotFound), nameof(IsMissing), nameof(Icon), nameof(HasIcon), nameof(Detail) }) {
                OnPropertyChanged(property);
            }
        }
    }

    /// <summary> A custom app (one that isn't a known launcher): its icon, name and the buttons to launch, rename, re-point or unlink it. </summary>
    internal class AppRowVM : BaseVM {
        private readonly AppsVM _owner;

        public AppEntry App { get; }

        public string Name => App.Name;
        public ImageSource? Icon => IconExtractor.For(App.ExePath);
        public bool HasIcon => Icon is not null;
        public bool IsMissing => !App.Exists;

        /// <summary> The path, or a "not found" note in front of it when the program has gone. </summary>
        public string Detail => IsMissing ? $"Not found: {App.ExePath}" : App.ExePath;

        private bool _isEditing;
        public bool IsEditing {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged(); }
        }

        private string _editName = string.Empty;
        public string EditName {
            get => _editName;
            set { _editName = value; OnPropertyChanged(); }
        }

        public ICommand LaunchCommand { get; }
        public ICommand StartRenameCommand { get; }
        public ICommand ConfirmRenameCommand { get; }
        public ICommand CancelRenameCommand { get; }
        public ICommand ChangePathCommand { get; }
        public ICommand RemoveCommand { get; }

        public AppRowVM(AppEntry app, AppsVM owner) {
            App = app;
            _owner = owner;

            LaunchCommand = new RelayCommand(_ => _owner.Launch(App));
            StartRenameCommand = new RelayCommand(_ => { EditName = App.Name; IsEditing = true; });
            ConfirmRenameCommand = new RelayCommand(_ => { _owner.Rename(this, EditName); IsEditing = false; });
            CancelRenameCommand = new RelayCommand(_ => IsEditing = false);
            ChangePathCommand = new RelayCommand(_ => _owner.ChangePath(this));
            RemoveCommand = new RelayCommand(_ => _owner.Remove(this));
        }

        /// <summary> Re-reads everything shown, after the name or path changed. </summary>
        public void Refresh() {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(IsMissing));
            OnPropertyChanged(nameof(Detail));
        }
    }

    /// <summary>
    /// The Apps tab: a list of the launchers Nexus knows about, where you choose which to link (found ones link in one click; ones it
    /// can't find, or finds wrongly, you point at yourself), plus any custom programs you add. Saved in apps.json.
    /// </summary>
    internal class AppsVM : BaseVM {

        private readonly Func<string?> _pickProgram;
        private readonly Func<IReadOnlyList<DetectedApp>> _detect;
        private AppsData _data = new();
        private IReadOnlyList<DetectedApp> _detected = [];

        /// <summary> Every known launcher, in a fixed order (Steam first). </summary>
        public ObservableCollection<KnownAppRowVM> Known { get; } = new();

        /// <summary> The apps you added yourself. </summary>
        public ObservableCollection<AppRowVM> Custom { get; } = new();

        public bool HasCustom => Custom.Count > 0;

        private bool _isDetecting;
        /// <summary> True while Nexus is still looking for the launchers on this PC. </summary>
        public bool IsDetecting {
            get => _isDetecting;
            private set { _isDetecting = value; OnPropertyChanged(); }
        }

        private string _message = string.Empty;
        /// <summary> The result of the last action, or the reason it didn't work. </summary>
        public string Message {
            get => _message;
            private set { _message = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMessage)); }
        }
        public bool HasMessage => _message.Length > 0;

        private bool _messageIsError;
        public bool MessageIsError {
            get => _messageIsError;
            private set { _messageIsError = value; OnPropertyChanged(); }
        }

        public ICommand AddAppCommand { get; }

        /// <param name="pickProgram"> Asks the user for a program or shortcut; null result = cancelled. Replaceable in tests.</param>
        /// <param name="detect"> Finds launchers on this PC. Replaceable in tests.</param>
        public AppsVM(Func<string?>? pickProgram = null, Func<IReadOnlyList<DetectedApp>>? detect = null) {
            _pickProgram = pickProgram ?? PickProgram;
            _detect = detect ?? KnownApps.DetectOnThisPc;

            foreach (var info in KnownApps.Catalog) {
                Known.Add(new KnownAppRowVM(info, this));
            }

            AddAppCommand = new RelayCommand(_ => {
                string? picked = _pickProgram();
                if (picked is not null) {
                    AddFromPath(picked);
                }
            });
        }

        private static string? PickProgram() {
            var dlg = new OpenFileDialog {
                Title = "Choose a program or shortcut",
                Filter = "Programs and shortcuts (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*",
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        /// <summary> Loads the saved links, then looks for the launchers on this PC. Runs every time the Apps tab is shown. </summary>
        public async Task LoadAsync() {
            _data = AppsStore.Load();
            IsDetecting = true;
            SyncAll();

            try {
                _detected = await Task.Run(_detect);
            }
            finally {
                IsDetecting = false;
            }

            // Apps saved by an earlier version have no launcher id: recognise the ones that are one of the known launchers
            bool changed = false;
            foreach (var entry in _data.Apps.Where(a => a.KnownId is null).ToList()) {
                var match = _detected.FirstOrDefault(d => d.ExePath.Equals(entry.ExePath, StringComparison.OrdinalIgnoreCase)
                                                          && !_data.Apps.Any(a => a.KnownId == d.KnownId));
                if (match is not null) {
                    entry.KnownId = match.KnownId;
                    changed = true;
                }
            }
            if (changed) {
                AppsStore.Save(_data);
            }

            SyncAll();
        }

        // Brings the known-launcher rows and the custom list in line with the saved links and what was detected
        private void SyncAll() {
            foreach (var row in Known) {
                row.Entry = _data.Apps.FirstOrDefault(a => a.KnownId == row.Info.Id);
                row.DetectedPath = _detected.FirstOrDefault(d => d.KnownId == row.Info.Id)?.ExePath;
                row.Refresh();
            }

            Custom.Clear();
            foreach (var app in _data.Apps.Where(a => a.KnownId is null)) {
                Custom.Add(new AppRowVM(app, this));
            }
            OnPropertyChanged(nameof(HasCustom));
        }

        private void Say(string message, bool isError) {
            MessageIsError = isError;
            Message = message;
        }

        private void Save() {
            AppsStore.Save(_data);
            SyncAll();
        }

        #region Known launchers
        /// <summary> Links a launcher that was found on this PC. </summary>
        public void Link(KnownAppRowVM row) {
            if (row.DetectedPath is null) return;

            _data.Apps.RemoveAll(a => a.KnownId == row.Info.Id);
            _data.Apps.Add(new AppEntry { Name = row.Info.Name, KnownId = row.Info.Id, ExePath = row.DetectedPath });
            Save();
            Say($"Linked {row.Info.Name}.", false);
        }

        /// <summary> Asks for a program and links (or re-points) the launcher to it: for one Nexus couldn't find, or found wrongly. </summary>
        public void Browse(KnownAppRowVM row) {
            string? picked = _pickProgram();
            if (picked is not null) {
                LinkTo(row, picked);
            }
        }

        public bool LinkTo(KnownAppRowVM row, string path) {
            if (!TryResolveProgram(path, out string exe, out string args, out _, out string? error)) {
                Say(error!, true);
                return false;
            }

            var existing = _data.Apps.FirstOrDefault(a => a.KnownId == row.Info.Id);
            if (existing is not null) {
                IconExtractor.Forget(existing.ExePath);
                existing.ExePath = exe;
                existing.Arguments = args;
            }
            else {
                _data.Apps.Add(new AppEntry { Name = row.Info.Name, KnownId = row.Info.Id, ExePath = exe, Arguments = args });
            }

            Save();
            Say($"{row.Info.Name} now points to {exe}.", false);
            return true;
        }

        public void Unlink(KnownAppRowVM row) {
            _data.Apps.RemoveAll(a => a.KnownId == row.Info.Id);
            Save();
            Say($"Unlinked {row.Info.Name}.", false);
        }
        #endregion

        #region Custom apps
        /// <summary>
        /// Works out what to launch for a file the user chose or dropped. A shortcut (.lnk) is followed to the program it starts
        /// (keeping its arguments); a plain .exe is used as is.
        /// </summary>
        public static bool TryResolveProgram(string path, out string exePath, out string arguments, out string name, out string? error) {
            exePath = path;
            arguments = string.Empty;
            name = string.Empty;
            error = null;

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".lnk") {
                var target = ShortcutResolver.Resolve(path);
                if (target is null || !target.TargetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                    error = "That shortcut doesn't point to a program Nexus can start (only .exe programs work).";
                    return false;
                }

                exePath = target.TargetPath;
                arguments = target.Arguments;
                name = Path.GetFileNameWithoutExtension(path);
            }
            else if (extension == ".exe") {
                name = FriendlyName(path);
            }
            else {
                error = "Choose an .exe program or a shortcut (.lnk).";
                return false;
            }

            if (!File.Exists(exePath)) {
                error = $"{exePath} doesn't exist.";
                return false;
            }
            return true;
        }

        // "steam" reads better as "Steam" when the exe carries a product name; otherwise the file name is used
        private static string FriendlyName(string exePath) {
            try {
                string? product = FileVersionInfo.GetVersionInfo(exePath).ProductName?.Trim();
                if (!string.IsNullOrEmpty(product) && product.Length <= 40 && !product.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)) {
                    return product;
                }
            }
            catch (Exception ex) when (ex is IOException or FileNotFoundException) {
                // fall through to the file name
            }
            return Path.GetFileNameWithoutExtension(exePath);
        }

        /// <summary> Adds a custom program or shortcut (from the button or a drag-and-drop). </summary>
        public bool AddFromPath(string path) {
            if (Directory.Exists(path)) {
                Say("Drop a program (.exe) or a shortcut (.lnk), not a folder.", true);
                return false;
            }

            if (!TryResolveProgram(path, out string exe, out string args, out string name, out string? error)) {
                Say(error!, true);
                return false;
            }

            if (_data.Apps.Any(a => a.ExePath.Equals(exe, StringComparison.OrdinalIgnoreCase) && a.Arguments == args)) {
                Say($"{name} is already linked.", true);
                return false;
            }

            _data.Apps.Add(new AppEntry { Name = name, ExePath = exe, Arguments = args });
            Save();
            Say($"Added {name}.", false);
            return true;
        }

        public void Rename(AppRowVM row, string newName) {
            newName = newName.Trim();
            if (newName.Length == 0 || newName == row.App.Name) {
                return;
            }

            row.App.Name = newName;
            AppsStore.Save(_data);
            row.Refresh();
        }

        public void ChangePath(AppRowVM row) {
            string? picked = _pickProgram();
            if (picked is not null) {
                ChangePathTo(row, picked);
            }
        }

        /// <summary> Points a custom app at a different program (e.g. after reinstalling on another drive). The name stays. </summary>
        public bool ChangePathTo(AppRowVM row, string path) {
            if (!TryResolveProgram(path, out string exe, out string args, out _, out string? error)) {
                Say(error!, true);
                return false;
            }

            IconExtractor.Forget(row.App.ExePath);
            row.App.ExePath = exe;
            row.App.Arguments = args;
            AppsStore.Save(_data);
            row.Refresh();
            Say($"{row.App.Name} now starts {exe}.", false);
            return true;
        }

        public void Remove(AppRowVM row) {
            _data.Apps.Remove(row.App);
            Save();
            Say($"Unlinked {row.App.Name}.", false);
        }
        #endregion

        public void Launch(AppEntry app) {
            if (app.Launch(out string? error)) {
                Message = string.Empty;
            }
            else {
                Say(error ?? "Couldn't start that app.", true);
            }
        }
    }
}
