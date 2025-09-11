namespace EconToolbox.Desktop.Models
{
    public class FutureCostEntry : ObservableObject
    {
        private double _cost;
        private int _year;
        private double _pvFactor;
        private string _timing = "end";

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

        /// <summary>
        /// Present value factor calculated from rate, year and payment timing.
        /// </summary>
        public double PvFactor
        {
            get => _pvFactor;
            set { _pvFactor = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Payment timing within the period: beginning, midpoint or end.
        /// </summary>
        public string Timing
        {
            get => _timing;
            set { _timing = value; OnPropertyChanged(); }
        }
    }
}
