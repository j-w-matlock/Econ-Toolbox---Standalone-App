namespace EconToolbox.Desktop.Models
{
    public class DemandEntry : ObservableObject
    {
        private int _year;
        private double _demand;
        private double _industrialDemand;
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
            set { _demand = value; OnPropertyChanged(); }
        }

        public double IndustrialDemand
        {
            get => _industrialDemand;
            set { _industrialDemand = value; OnPropertyChanged(); }
        }

        public double AdjustedDemand
        {
            get => _adjustedDemand;
            set { _adjustedDemand = value; OnPropertyChanged(); }
        }

        public double GrowthRate
        {
            get => _growthRate;
            set { _growthRate = value; OnPropertyChanged(); }
        }
    }
}
