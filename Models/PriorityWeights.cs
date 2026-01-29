using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class PriorityWeights : ObservableObject
    {
        [ObservableProperty]
        private double _urgencyWeight = 1.0;

        [ObservableProperty]
        private double _importanceWeight = 1.0;

        [ObservableProperty]
        private double _complexityWeight = 1.0;
    }
}
