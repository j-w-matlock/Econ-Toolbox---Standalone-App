namespace EconToolbox.Desktop.Models
{
    public class DemandEntry : ObservableObject
    {
        private int _year;
        private double _demand;
        private double _industrialDemand;
        private double _adjustedDemand;

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
    }
}
