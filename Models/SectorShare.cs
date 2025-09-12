using System.ComponentModel;

namespace EconToolbox.Desktop.Models
{
    /// <summary>
    /// Represents the percentage share of demand for a sector.
    /// </summary>
    public class SectorShare : ObservableObject
    {
        private string _name = string.Empty;
        private double _currentPercent;
        private double _futurePercent;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public double CurrentPercent
        {
            get => _currentPercent;
            set { _currentPercent = value; OnPropertyChanged(); }
        }
        public double FuturePercent
        {
            get => _futurePercent;
            set { _futurePercent = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// Indicates this sector's percentages are calculated as the residual to total 100%.
        /// </summary>
        public bool IsResidual { get; set; }
    }
}
