using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public class AgricultureDepthDamageData
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
        public string? EstimatorDefaultCurve { get; set; }
        public double EstimatorDefaultCropValue { get; set; }
        public double EstimatorDamageStdDev { get; set; }
        public double EstimatorDepthStdDev { get; set; }
        public double EstimatorValueStdDev { get; set; }
        public int EstimatorMonteCarloRuns { get; set; }
        public int EstimatorAnalysisYears { get; set; }
        public int EstimatorRandomSeed { get; set; }
        public bool EstimatorRandomizeMonth { get; set; }
        public List<EstimatorEventData> EstimatorEvents { get; set; } = new();
        public List<EstimatorCropData> EstimatorCropRows { get; set; } = new();
        public string? EstimatorCdlRasterPath { get; set; }
        public string? EstimatorDepthRasterPath { get; set; }
        public string? EstimatorPolygonShapefilePath { get; set; }
        public double EstimatorUniformPolygonDepth { get; set; }
        public bool EstimatorUsePolygonUniformDepth { get; set; }
        public List<EstimatorSpatialCropData> EstimatorSpatialCropRows { get; set; } = new();
    }
}