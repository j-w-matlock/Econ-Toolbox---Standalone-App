using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class AnnualBenefitEntry : ObservableObject
    {
        [ObservableProperty]
        private string _key = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private double _amount;
    }
}
