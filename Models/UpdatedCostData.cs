using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public class UpdatedCostData
    {
        public double TotalStorage { get; set; }
        public double StorageRecommendation { get; set; }
        public double JointOperationsCost { get; set; }
        public double JointMaintenanceCost { get; set; }
        public List<UpdatedCostEntryData> UpdatedCostItems { get; set; } = new();
        public int PreEnrYear { get; set; }
        public int TransitionEnrYear { get; set; }
        public int Enr1967Year { get; set; }
        public double PreEnrIndexValue { get; set; }
        public double TransitionEnrIndexValue { get; set; }
        public double Enr1967IndexValue { get; set; }
        public double CwccisBaseIndexValue { get; set; }
        public int CwccisIndexYear { get; set; }
        public double RrrRate { get; set; }
        public int RrrPeriods { get; set; }
        public double RrrCwcci { get; set; }
        public int RrrBaseYear { get; set; }
        public List<RrrCostEntryData> RrrCostItems { get; set; } = new();
        public double DiscountRate1 { get; set; }
        public int AnalysisPeriod1 { get; set; }
        public double DiscountRate2 { get; set; }
        public int AnalysisPeriod2 { get; set; }
    }
}
