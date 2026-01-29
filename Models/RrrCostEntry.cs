using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class RrrCostEntry : ObservableObject
    {
        [ObservableProperty]
        private string _item = string.Empty;

        [ObservableProperty]
        private double _futureCost;

        [ObservableProperty]
        private int _year;

        [ObservableProperty]
        private double _pvFactor;

        [ObservableProperty]
        private double _presentValue;
    }
}
