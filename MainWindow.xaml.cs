using System.Windows;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
