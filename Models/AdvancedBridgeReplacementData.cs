namespace EconToolbox.Desktop.Models;

public sealed class AdvancedBridgeReplacementData
{
    public double CostOfNewBridge { get; set; }
    public double LifeOfNewBridgeYears { get; set; }
    public double RemainingLifeOfExistingBridgeYears { get; set; }
    public double DiscountRate { get; set; }
    public double AnnualOmAndRehabExistingBridge { get; set; }
    public double AnnualOmNewBridge { get; set; }
}
