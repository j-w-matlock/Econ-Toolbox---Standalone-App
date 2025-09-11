namespace EconToolbox.Desktop.Models
{
    public class FutureCostEntry : ObservableObject
    {
        private double _cost;
        private int _year;

        public double Cost
        {
            get => _cost;
            set { _cost = value; OnPropertyChanged(); }
        }

        public int Year
        {
            get => _year;
            set { _year = value; OnPropertyChanged(); }
        }
    }
}
