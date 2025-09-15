namespace EconToolbox.Desktop.Models
{
    public class DemandEntry : ObservableObject
    {
        private const double GallonsPerAcreFoot = 325851.0;
        private const double DaysPerYear = 365.0;
        private int _year;
        private double _demand;
        private double _residentialDemand;
        private double _commercialDemand;
        private double _industrialDemand;
        private double _agriculturalDemand;
        private double _adjustedDemand;
        private double _growthRate;

        public int Year
        {
            get => _year;
            set { _year = value; OnPropertyChanged(); }
        }

        public double Demand
        {
            get => _demand;
            set
            {
                if (_demand == value)
                    return;
                _demand = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DemandAcreFeet));
            }
        }

        public double ResidentialDemand
        {
            get => _residentialDemand;
            set { _residentialDemand = value; OnPropertyChanged(); }
        }

        public double CommercialDemand
        {
            get => _commercialDemand;
            set { _commercialDemand = value; OnPropertyChanged(); }
        }

        public double IndustrialDemand
        {
            get => _industrialDemand;
            set { _industrialDemand = value; OnPropertyChanged(); }
        }

        public double AgriculturalDemand
        {
            get => _agriculturalDemand;
            set { _agriculturalDemand = value; OnPropertyChanged(); }
        }

        public double AdjustedDemand
        {
            get => _adjustedDemand;
            set
            {
                if (_adjustedDemand == value)
                    return;
                _adjustedDemand = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdjustedDemandAcreFeet));
            }
        }

        public double GrowthRate
        {
            get => _growthRate;
            set { _growthRate = value; OnPropertyChanged(); }
        }

        public double DemandAcreFeet => _demand * DaysPerYear / GallonsPerAcreFoot;

        public double AdjustedDemandAcreFeet => _adjustedDemand * DaysPerYear / GallonsPerAcreFoot;
    }
}
