using System.Collections.ObjectModel;
using System.Windows.Media;

namespace EconToolbox.Desktop.Models
{
    /// <summary>
    /// Represents a set of water demand forecast parameters and results.
    /// </summary>
    public class Scenario : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int BaseYear { get; set; }
        public double BasePopulation { get; set; }
        public double BasePerCapitaDemand { get; set; }
        public double PopulationGrowthRate { get; set; }
        public double PerCapitaDemandChangeRate { get; set; }

        /// <summary>
        /// Percentage shares for each demand sector. The last entry is treated
        /// as the residual category that is auto-calculated to total 100%.
        /// </summary>
        public ObservableCollection<SectorShare> Sectors { get; } = new()
        {
            new SectorShare { Name = "Industrial" },
            new SectorShare { Name = "Residential" },
            new SectorShare { Name = "Commercial" },
            new SectorShare { Name = "Agricultural", IsResidual = true }
        };
        public double SystemImprovementsPercent { get; set; }
        public double SystemLossesPercent { get; set; }

        public ObservableCollection<DemandEntry> Results { get; } = new();

        private PointCollection _chartPoints = new();
        public PointCollection ChartPoints
        {
            get => _chartPoints;
            set { _chartPoints = value; OnPropertyChanged(); }
        }

        public Brush LineBrush { get; set; } = Brushes.Blue;
    }
}
