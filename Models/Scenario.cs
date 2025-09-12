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

        public int BaseYear { get; set; }
        public double BasePopulation { get; set; }
        public double BasePerCapitaDemand { get; set; }
        public double PopulationGrowthRate { get; set; }
        public double PerCapitaDemandChangeRate { get; set; }
        public double StandardGrowthRate { get; set; }

        public double CurrentIndustrialPercent { get; set; }
        public double FutureIndustrialPercent { get; set; }
        public double CurrentResidentialPercent { get; set; }
        public double FutureResidentialPercent { get; set; }
        public double CurrentCommercialPercent { get; set; }
        public double FutureCommercialPercent { get; set; }
        public double CurrentAgriculturalPercent { get; set; }
        public double FutureAgriculturalPercent { get; set; }
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
