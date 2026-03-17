using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public class AnnualizerData
    {
        public double FirstCost { get; set; }
        public double Rate { get; set; }
        public int AnalysisPeriod { get; set; }
        public int BaseYear { get; set; }
        public int ConstructionMonths { get; set; }
        public double AnnualOm { get; set; }
        public double AnnualBenefits { get; set; }
        public List<AnnualBenefitEntryData> AnnualBenefitEntries { get; set; } = new();
        public List<AnnualizerFutureCostData> FutureCosts { get; set; } = new();
        public List<AnnualizerFutureCostData> IdcEntries { get; set; } = new();
        public string? IdcTimingBasis { get; set; }
        public bool CalculateInterestAtPeriod { get; set; }
        public string? IdcFirstPaymentTiming { get; set; }
        public string? IdcLastPaymentTiming { get; set; }
        public List<AnnualizerScenarioData> Scenarios { get; set; } = new();
        public string? SelectedScenarioName { get; set; }
    }

    public class AnnualBenefitEntryData
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Amount { get; set; }
        public bool IncludeInTotal { get; set; } = true;
        public bool IsModuleLinked { get; set; }
    }
}
