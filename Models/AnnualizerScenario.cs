using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class AnnualizerScenario : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private double _firstCost;

        [ObservableProperty]
        private double _annualOm;

        [ObservableProperty]
        private double _annualBenefits;

        [ObservableProperty]
        private double _rate;

        [ObservableProperty]
        private double _idc;

        [ObservableProperty]
        private double _futureCostPv;

        [ObservableProperty]
        private double _totalInvestment;

        [ObservableProperty]
        private double _crf;

        [ObservableProperty]
        private double _annualCost;

        [ObservableProperty]
        private double _bcr;

        [ObservableProperty]
        private double? _unityBcrFirstCost;

        [ObservableProperty]
        private string? _notes;
    }
}
