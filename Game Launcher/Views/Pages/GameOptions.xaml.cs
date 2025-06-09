using Game_Launcher.Models;
using Game_Launcher.ViewModels.Pages;
using System.Windows.Controls;

namespace Game_Launcher.Views.Pages
{
    public partial class GameOptions : Page
    {
        public GameOptions(GameMapping mapping) {
            InitializeComponent();

            DataContext = new GameOptionsVM(mapping);
        }
    }
}
