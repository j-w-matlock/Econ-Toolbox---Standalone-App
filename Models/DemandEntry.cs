using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class DemandEntry : ObservableObject
    {
        private const double GallonsPerAcreFoot = 325851.0;
        private const double DaysPerYear = 365.0;

        [ObservableProperty]
        private int _year;

        [ObservableProperty]
        private double _demand;

        [ObservableProperty]
        private double _residentialDemand;

        [ObservableProperty]
        private double _commercialDemand;

        [ObservableProperty]
        private double _industrialDemand;

        [ObservableProperty]
        private double _agriculturalDemand;

        [ObservableProperty]
        private double _adjustedDemand;

        [ObservableProperty]
        private double _growthRate;

        public double DemandAcreFeet => _demand * DaysPerYear / GallonsPerAcreFoot;

        public double AdjustedDemandAcreFeet => _adjustedDemand * DaysPerYear / GallonsPerAcreFoot;

        partial void OnDemandChanged(double value)
        {
            OnPropertyChanged(nameof(DemandAcreFeet));
        }

        partial void OnAdjustedDemandChanged(double value)
        {
            OnPropertyChanged(nameof(AdjustedDemandAcreFeet));
        }
    }
}
