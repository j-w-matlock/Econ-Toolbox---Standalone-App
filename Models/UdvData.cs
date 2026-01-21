using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public class UdvData
    {
        public string? RecreationType { get; set; }
        public string? ActivityType { get; set; }
        public double Points { get; set; }
        public double SeasonDays { get; set; }
        public double VisitationInput { get; set; }
        public string? VisitationPeriod { get; set; }
        public string? ChartTitle { get; set; }
        public List<UdvPointValueData> Table { get; set; } = new();
        public List<UdvHistoricalVisitationData> HistoricalVisitationRows { get; set; } = new();
    }
}
