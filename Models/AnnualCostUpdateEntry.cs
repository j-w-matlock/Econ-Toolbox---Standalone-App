using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class AnnualCostUpdateEntry : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private double _cost;

        [ObservableProperty]
        private int _originalFiscalYear;

        [ObservableProperty]
        private int _updatedFiscalYear;

        [ObservableProperty]
        private double _indexFactor = 1d;

        [ObservableProperty]
        private bool _isSelected;

        public double UpdatedCost => Cost * IndexFactor;

        partial void OnCostChanged(double value) => OnPropertyChanged(nameof(UpdatedCost));

        partial void OnIndexFactorChanged(double value) => OnPropertyChanged(nameof(UpdatedCost));
    }
}
