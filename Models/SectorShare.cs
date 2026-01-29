using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    /// <summary>
    /// Represents the percentage share of demand for a sector.
    /// </summary>
    public partial class SectorShare : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private double _currentPercent;

        [ObservableProperty]
        private double _futurePercent;

        /// <summary>
        /// Indicates this sector's percentages are calculated as the residual to total 100%.
        /// </summary>
        [ObservableProperty]
        private bool _isResidual;
    }
}
