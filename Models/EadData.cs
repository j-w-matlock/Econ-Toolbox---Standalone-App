using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public class EadData
    {
        public bool UseStage { get; set; }
        public bool CalculateEqad { get; set; }
        public int AnalysisPeriod { get; set; }
        public double DiscountRate { get; set; }
        public string? ChartTitle { get; set; }
        public List<EadDamageColumnData> DamageColumns { get; set; } = new();
        public List<EadRowData> Rows { get; set; } = new();
    }
}
