using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.Models
{
    public class RrrCostEntry : ObservableObject
    {
        private string _item = string.Empty;
        private double _futureCost;
        private int _year;
        private double _pvFactor;
        private double _presentValue;

        public string Item
        {
            get => _item;
            set { _item = value; OnPropertyChanged(); }
        }

        public double FutureCost
        {
            get => _futureCost;
            set { _futureCost = value; OnPropertyChanged(); }
        }

        public int Year
        {
            get => _year;
            set { _year = value; OnPropertyChanged(); }
        }

        public double PvFactor
        {
            get => _pvFactor;
            set { _pvFactor = value; OnPropertyChanged(); }
        }

        public double PresentValue
        {
            get => _presentValue;
            set { _presentValue = value; OnPropertyChanged(); }
        }
    }
}
