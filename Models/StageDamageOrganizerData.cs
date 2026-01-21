using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public class StageDamageOrganizerData
    {
        public string? StatusMessage { get; set; }
        public List<string> AepHeaders { get; set; } = new();
        public List<StageDamageSummaryInfoData> Summaries { get; set; } = new();
        public List<StageDamageRecordData> Records { get; set; } = new();
    }
}
