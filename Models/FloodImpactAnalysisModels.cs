using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public record FloodEventInput(string Name, double DepthFeet, int FloodMonth, double ReturnPeriodYears);

    public record CropImpactInput(
        int CropCode,
        string CropName,
        string EventName,
        double Acres,
        double ValuePerAcre,
        string GrowingMonthsCsv,
        string SpecificCurve,
        double SpatialAverageDepthFeet);

    public record FloodImpactUncertaintySettings(
        string DefaultCurve,
        double DefaultCropValue,
        double DamageStdDev,
        double DepthStdDev,
        double ValueStdDev,
        int MonteCarloRuns,
        int AnalysisYears,
        int RandomSeed,
        bool RandomizeFloodMonth);

    public record FloodImpactAnalysisRequest(
        IReadOnlyList<FloodEventInput> Events,
        IReadOnlyList<CropImpactInput> Crops,
        FloodImpactUncertaintySettings Settings);

    public record FloodImpactEventResult(
        string EventName,
        double MeanDamage,
        double StdDamage,
        double P5Damage,
        double P95Damage,
        double DiscreteEadContribution,
        int Samples);

    public record FloodImpactSummaryResult(
        double TotalDiscreteEad,
        double TotalMeanDamage,
        int EventCount,
        int CropCount,
        int Samples,
        double MeanCoefficientOfVariation);

    public record FloodImpactAnalysisResult(
        IReadOnlyList<FloodImpactEventResult> Events,
        FloodImpactSummaryResult Summary);
}
