using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class FutureCostEntry : ObservableObject
    {
        [ObservableProperty]
        private double _cost;

        [ObservableProperty]
        private int _year;

        [ObservableProperty]
        private int _month = 1;

        /// <summary>
        /// Present value factor calculated from rate, year and payment timing.
        /// </summary>
        [ObservableProperty]
        private double _pvFactor;

        /// <summary>
        /// Payment timing within the period: beginning, midpoint or end.
        /// </summary>
        [ObservableProperty]
        private string _timing = "end";
    }
}
