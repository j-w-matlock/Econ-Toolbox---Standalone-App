using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    public partial class UpdatedCostEntry : ObservableObject
    {
        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private double _jointUsePre1967;

        [ObservableProperty]
        private double _pre1967EnrIndex;

        [ObservableProperty]
        private double _transitionEnrIndex;

        [ObservableProperty]
        private double _enrRatioPreToTransition;

        [ObservableProperty]
        private double _jointUseTransition;

        [ObservableProperty]
        private double _enr1967Index;

        [ObservableProperty]
        private double _enrRatioTransitionTo1967;

        [ObservableProperty]
        private double _cwccisBase = 100.0;

        [ObservableProperty]
        private double _jointUse1967;

        [ObservableProperty]
        private double _cwccisIndex;

        [ObservableProperty]
        private double _cwccisUpdateFactor;

        [ObservableProperty]
        private double _updatedJointCost;
    }
}
