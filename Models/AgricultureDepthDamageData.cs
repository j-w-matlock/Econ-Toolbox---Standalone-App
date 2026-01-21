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
    }
}
