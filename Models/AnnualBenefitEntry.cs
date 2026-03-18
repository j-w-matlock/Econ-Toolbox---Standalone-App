using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class AnnualBenefitEntry : ObservableObject
    {
        [ObservableProperty]
        private string _key = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private double _amount;

        [ObservableProperty]
        private double _indexFactor = 1d;

        [ObservableProperty]
        private bool _includeInTotal = true;

        [ObservableProperty]
        private bool _isModuleLinked;

        public double IndexedAmount => Amount * IndexFactor;

        partial void OnAmountChanged(double value) => OnPropertyChanged(nameof(IndexedAmount));

        partial void OnIndexFactorChanged(double value) => OnPropertyChanged(nameof(IndexedAmount));
    }
}
