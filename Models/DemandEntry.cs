namespace EconToolbox.Desktop.Models
{
    public class DemandEntry : ObservableObject
    {
        private int _year;
        private double _demand;

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
    }
}
