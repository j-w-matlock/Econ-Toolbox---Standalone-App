using System.Windows.Input;

namespace EconToolbox.Desktop.ViewModels
{ 
    public class MainViewModel : BaseViewModel
    {
        public EadViewModel Ead { get; } = new();
        public UpdatedCostViewModel UpdatedCost { get; } = new();
        public AnnualizerViewModel Annualizer { get; } = new();
        public UdvViewModel Udv { get; } = new();
        public WaterDemandViewModel WaterDemand { get; } = new();

        private object? _selectedViewModel;
        public object? SelectedViewModel
        {
            get => _selectedViewModel;
            set { _selectedViewModel = value; OnPropertyChanged(); }
        }

        public ICommand CalculateCommand { get; }
        public ICommand ExportCommand { get; }

        public MainViewModel()
        {
            CalculateCommand = new RelayCommand(Calculate);
            ExportCommand = new RelayCommand(Export);
        }

        private void Calculate()
        {
            if (SelectedViewModel == null) return;
            var prop = SelectedViewModel.GetType().GetProperty("ComputeCommand");
            if (prop?.GetValue(SelectedViewModel) is ICommand cmd && cmd.CanExecute(null))
                cmd.Execute(null);
        }

        private void Export()
        {
            if (SelectedViewModel == null) return;
            var prop = SelectedViewModel.GetType().GetProperty("ExportCommand");
            if (prop?.GetValue(SelectedViewModel) is ICommand cmd && cmd.CanExecute(null))
                cmd.Execute(null);
        }
    }
}
