using System;
using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public class ProjectData
    {
        public string Version { get; set; } = "1.0";
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
        public EadProjectData Ead { get; set; } = new();
        public AgricultureDepthDamageProjectData AgricultureDepthDamage { get; set; } = new();
        public UpdatedCostProjectData UpdatedCost { get; set; } = new();
        public AnnualizerProjectData Annualizer { get; set; } = new();
        public WaterDemandProjectData WaterDemand { get; set; } = new();
        public UdvProjectData Udv { get; set; } = new();
        public RecreationCapacityProjectData RecreationCapacity { get; set; } = new();
        public GanttProjectData Gantt { get; set; } = new();
        public StageDamageOrganizerProjectData StageDamageOrganizer { get; set; } = new();
    }

    public class EadProjectData
    {
        public bool UseStage { get; set; }
        public bool CalculateEqad { get; set; }
        public int AnalysisPeriod { get; set; }
        public double DiscountRate { get; set; }
        public string? ChartTitle { get; set; }
        public List<EadDamageColumnData> DamageColumns { get; set; } = new();
        public List<EadRowData> Rows { get; set; } = new();
    }

    public class EadDamageColumnData
    {
        public string Name { get; set; } = string.Empty;
    }

    public class EadRowData
    {
        public double Probability { get; set; }
        public double? Stage { get; set; }
        public double FutureDamages { get; set; }
        public List<double> Damages { get; set; } = new();
    }

    public class AgricultureDepthDamageProjectData
    {
        public string? SelectedRegionName { get; set; }
        public string? SelectedCropName { get; set; }
        public double AverageResponse { get; set; }
        public int SimulationYears { get; set; }
        public List<AgricultureRegionData> Regions { get; set; } = new();
        public List<AgricultureCropData> Crops { get; set; } = new();
        public List<AgricultureStageExposureData> StageExposures { get; set; } = new();
        public double CropScapeTotalAcreage { get; set; }
        public string? CropScapeImportStatus { get; set; }
        public List<CropScapeSummaryData> CropScapeSummaries { get; set; } = new();
    }

    public class AgricultureRegionData
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double ImpactModifier { get; set; }
        public int FloodWindowStartDay { get; set; }
        public int FloodWindowEndDay { get; set; }
        public double AnnualExceedanceProbability { get; set; }
        public int FloodSeasonPeakDay { get; set; }
        public int SeasonShiftDays { get; set; }
        public bool IsCustom { get; set; }
        public List<AgricultureDepthDurationPointData> DepthDuration { get; set; } = new();
    }

    public class AgricultureDepthDurationPointData
    {
        public double DepthFeet { get; set; }
        public double DurationDays { get; set; }
        public double BaseDamage { get; set; }
    }

    public class AgricultureCropData
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double DamageFactor { get; set; }
        public double ImpactModifier { get; set; }
        public bool IsCustom { get; set; }
    }

    public class AgricultureStageExposureData
    {
        public string StageName { get; set; } = string.Empty;
        public double ExposureDays { get; set; }
        public double FloodToleranceDays { get; set; }
    }

    public class CropScapeSummaryData
    {
        public int Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public long PixelCount { get; set; }
        public double Acres { get; set; }
        public double PercentOfTotal { get; set; }
    }

    public class AnnualizerProjectData
    {
        public double FirstCost { get; set; }
        public double Rate { get; set; }
        public int AnalysisPeriod { get; set; }
        public int BaseYear { get; set; }
        public int ConstructionMonths { get; set; }
        public double AnnualOm { get; set; }
        public double AnnualBenefits { get; set; }
        public List<AnnualizerFutureCostData> FutureCosts { get; set; } = new();
        public List<AnnualizerFutureCostData> IdcEntries { get; set; } = new();
        public string? IdcTimingBasis { get; set; }
        public bool CalculateInterestAtPeriod { get; set; }
        public string? IdcFirstPaymentTiming { get; set; }
        public string? IdcLastPaymentTiming { get; set; }
        public List<AnnualizerScenarioData> Scenarios { get; set; } = new();
        public string? SelectedScenarioName { get; set; }
    }

    public class AnnualizerFutureCostData
    {
        public double Cost { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string? Timing { get; set; }
    }

    public class AnnualizerScenarioData
    {
        public string Name { get; set; } = string.Empty;
        public double FirstCost { get; set; }
        public double AnnualOm { get; set; }
        public double AnnualBenefits { get; set; }
        public double Rate { get; set; }
    }

    public class UpdatedCostProjectData
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

    public class UpdatedCostEntryData
    {
        public string Category { get; set; } = string.Empty;
        public double JointUsePre1967 { get; set; }
        public double Pre1967EnrIndex { get; set; }
        public double TransitionEnrIndex { get; set; }
        public double EnrRatioPreToTransition { get; set; }
        public double JointUseTransition { get; set; }
        public double Enr1967Index { get; set; }
        public double EnrRatioTransitionTo1967 { get; set; }
        public double CwccisBase { get; set; }
        public double JointUse1967 { get; set; }
        public double CwccisIndex { get; set; }
        public double CwccisUpdateFactor { get; set; }
        public double UpdatedJointCost { get; set; }
    }

    public class RrrCostEntryData
    {
        public string Item { get; set; } = string.Empty;
        public double FutureCost { get; set; }
        public int Year { get; set; }
        public double PvFactor { get; set; }
        public double PresentValue { get; set; }
    }

    public class WaterDemandProjectData
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

    public class WaterDemandEntryData
    {
        public int Year { get; set; }
        public double Demand { get; set; }
    }

    public class WaterDemandScenarioData
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int BaseYear { get; set; }
        public double BasePopulation { get; set; }
        public double BasePerCapitaDemand { get; set; }
        public double PopulationGrowthRate { get; set; }
        public double PerCapitaDemandChangeRate { get; set; }
        public double SystemImprovementsPercent { get; set; }
        public double SystemLossesPercent { get; set; }
        public List<WaterDemandSectorShareData> Sectors { get; set; } = new();
    }

    public class WaterDemandSectorShareData
    {
        public string Name { get; set; } = string.Empty;
        public double CurrentPercent { get; set; }
        public double FuturePercent { get; set; }
        public bool IsResidual { get; set; }
    }

    public class UdvProjectData
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

    public class UdvPointValueData
    {
        public int Points { get; set; }
        public double GeneralRecreation { get; set; }
        public double GeneralFishingHunting { get; set; }
        public double SpecializedFishingHunting { get; set; }
        public double SpecializedRecreation { get; set; }
    }

    public class UdvHistoricalVisitationData
    {
        public string? Label { get; set; }
        public string? VisitationText { get; set; }
    }

    public class RecreationCapacityProjectData
    {
        public double CampingCampsites { get; set; }
        public double CampingAverageGroupSize { get; set; }
        public double CampingDailyTurnover { get; set; }
        public double CampingSeasonLengthDays { get; set; }
        public double FishingAccessibleShorelineFeet { get; set; }
        public double FishingSpacingFeet { get; set; }
        public double FishingAverageGroupSize { get; set; }
        public double FishingDailyTurnover { get; set; }
        public double FishingSeasonLengthDays { get; set; }
        public double BoatingWaterSurfaceAcres { get; set; }
        public double BoatingAcresPerVessel { get; set; }
        public double BoatingPersonsPerVessel { get; set; }
        public double BoatingDailyTurnover { get; set; }
        public double BoatingSeasonLengthDays { get; set; }
    }

    public class GanttProjectData
    {
        public List<GanttTaskData> Tasks { get; set; } = new();
    }

    public class GanttTaskData
    {
        public string Name { get; set; } = string.Empty;
        public string Workstream { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public int DurationDays { get; set; }
        public DateTime EndDate { get; set; }
        public string Dependencies { get; set; } = string.Empty;
        public double PercentComplete { get; set; }
        public bool IsMilestone { get; set; }
        public double LaborCostPerDay { get; set; }
        public uint ColorArgb { get; set; }
    }

    public class StageDamageOrganizerProjectData
    {
        public string? StatusMessage { get; set; }
        public List<string> AepHeaders { get; set; } = new();
        public List<StageDamageSummaryInfoData> Summaries { get; set; } = new();
        public List<StageDamageRecordData> Records { get; set; } = new();
    }

    public class StageDamageSummaryInfoData
    {
        public string SourceKey { get; set; } = string.Empty;
        public string SourceLabel { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class StageDamageRecordData
    {
        public string StructureFid { get; set; } = string.Empty;
        public string DamageCategory { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImpactArea { get; set; } = string.Empty;
        public string OccTypeName { get; set; } = string.Empty;
        public string SummaryName { get; set; } = string.Empty;
        public string SourceKey { get; set; } = string.Empty;
        public List<StageDamageAepValueData> AepDamages { get; set; } = new();
    }

    public class StageDamageAepValueData
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
