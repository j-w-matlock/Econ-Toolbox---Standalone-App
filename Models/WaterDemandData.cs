using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public class WaterDemandData
    {
        public int ForecastYears { get; set; }
        public string? ChartTitle { get; set; }
        public double Alternative1PopulationAdjustment { get; set; }
        public double Alternative1PerCapitaAdjustment { get; set; }
        public double Alternative1ImprovementsAdjustment { get; set; }
        public double Alternative1LossesAdjustment { get; set; }
        public double Alternative2PopulationAdjustment { get; set; }
        public double Alternative2PerCapitaAdjustment { get; set; }
        public double Alternative2ImprovementsAdjustment { get; set; }
        public double Alternative2LossesAdjustment { get; set; }
        public List<WaterDemandEntryData> HistoricalData { get; set; } = new();
        public List<WaterDemandScenarioData> Scenarios { get; set; } = new();
        public string? SelectedScenarioName { get; set; }
    }
}
