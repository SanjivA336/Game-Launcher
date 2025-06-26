using Game_Launcher.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    internal class PreferencesWindowVM : BaseVM {

        Preferences Preferences { get; }


        public ObservableCollection<DirectoryInfo> Roots => new([.. Preferences.Roots]);

        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }

        public ICommand AddRootCommand { get; }
        public ICommand RemoveRootCommand { get; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }



        public PreferencesWindowVM(Action minimize, Action maximize, Action close) {
            Preferences = Preferences.Load();

            MinimizeCommand = new RelayCommand(_ => minimize());
            MaximizeCommand = new RelayCommand(_ => maximize());
            CloseCommand = new RelayCommand(_ => close());

            AddRootCommand = new RelayCommand(_ => AddRoot());
            RemoveRootCommand = new RelayCommand(_ => RemovePrimaryFolder());

            SaveCommand = new RelayCommand(_ => SaveChanges());
            CancelCommand = new RelayCommand(_ => CancelChanges());
            ResetCommand = new RelayCommand(_ => ResetChanges());
        }

        private void AddRoot() {
            var dlg = new OpenFolderDialog {
                Title = "Select a root folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                ValidateNames = true,
                Multiselect = false,
            };

            if (dlg.ShowDialog() == true) {
                var fullPath = new DirectoryInfo(dlg.FolderName);

                if (!Preferences.Roots.Contains(fullPath)) {
                    Preferences.Roots.Add(fullPath);
                    OnPropertyChanged(nameof(Roots));
                }
                else {
                    Debug.WriteLine("Executable already exists in list.");
                }
            }
        }

        private void RemovePrimaryFolder() {

        }

        private void SaveChanges() {

        }

        private void CancelChanges() {

        }

        private void ResetChanges() {

        }
    }
}
