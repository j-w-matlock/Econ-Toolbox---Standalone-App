namespace EconToolbox.Desktop.Models
{
    public class EstimatorEventData
    {
        public string Name { get; set; } = string.Empty;
        public double DepthFeet { get; set; }
        public int FloodMonth { get; set; }
        public double ReturnPeriodYears { get; set; }
    }

    public class EstimatorCropData
    {
        public int CropCode { get; set; }
        public string CropName { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public double Acres { get; set; }
        public double ValuePerAcre { get; set; }
        public string GrowingMonthsCsv { get; set; } = string.Empty;
        public string SpecificCurve { get; set; } = string.Empty;
    }
}
