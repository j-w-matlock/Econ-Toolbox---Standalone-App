using System.Windows.Input;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{ 
    public class MainViewModel : BaseViewModel
    {
        public EadViewModel Ead { get; } = new();
        public UpdatedCostViewModel UpdatedCost { get; } = new();
        public AnnualizerViewModel Annualizer { get; } = new();
        public UdvViewModel Udv { get; } = new();
        public WaterDemandViewModel WaterDemand { get; } = new();
        public MindMapViewModel MindMap { get; } = new();

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCalculateVisible));
            }
        }

        public ICommand CalculateCommand { get; }
        public ICommand ExportCommand { get; }

        public bool IsCalculateVisible => SelectedComputeCommand?.CanExecute(null) == true;

        public MainViewModel()
        {
            CalculateCommand = new RelayCommand(Calculate);
            ExportCommand = new RelayCommand(Export);

            SubscribeToComputeCommand(Ead.ComputeCommand);
            SubscribeToComputeCommand(UpdatedCost.ComputeCommand);
            SubscribeToComputeCommand(Annualizer.ComputeCommand);
            SubscribeToComputeCommand(WaterDemand.ComputeCommand);
            SubscribeToComputeCommand(Udv.ComputeCommand);
        }

        private void Calculate()
        {
            var command = SelectedComputeCommand;
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }

        private ICommand? SelectedComputeCommand => SelectedIndex switch
        {
            0 => Ead.ComputeCommand,
            1 => UpdatedCost.ComputeCommand,
            2 => Annualizer.ComputeCommand,
            3 => WaterDemand.ComputeCommand,
            4 => Udv.ComputeCommand,
            _ => null
        };

        private void SubscribeToComputeCommand(ICommand command)
        {
            command.CanExecuteChanged += (_, _) => OnPropertyChanged(nameof(IsCalculateVisible));
        }

        private void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "econ_toolbox.xlsx"
            };
            if (dlg.ShowDialog() == true)
            {
                ExcelExporter.ExportAll(Ead, UpdatedCost, Annualizer, WaterDemand, Udv, MindMap, dlg.FileName);
            }
        }
    }
}
