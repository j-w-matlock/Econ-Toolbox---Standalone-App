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

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { _selectedIndex = value; OnPropertyChanged(); }
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
            switch (SelectedIndex)
            {
                case 0:
                    if (Ead.ComputeCommand.CanExecute(null)) Ead.ComputeCommand.Execute(null);
                    break;
                case 1:
                    if (UpdatedCost.ComputeCommand.CanExecute(null)) UpdatedCost.ComputeCommand.Execute(null);
                    break;
                case 2:
                    if (Annualizer.ComputeCommand.CanExecute(null)) Annualizer.ComputeCommand.Execute(null);
                    break;
                case 3:
                    if (WaterDemand.ComputeCommand.CanExecute(null)) WaterDemand.ComputeCommand.Execute(null);
                    break;
                case 4:
                    if (Udv.ComputeCommand.CanExecute(null)) Udv.ComputeCommand.Execute(null);
                    break;
            }
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
                ExcelExporter.ExportAll(Ead, UpdatedCost, Annualizer, WaterDemand, Udv, dlg.FileName);
            }
        }
    }
}
