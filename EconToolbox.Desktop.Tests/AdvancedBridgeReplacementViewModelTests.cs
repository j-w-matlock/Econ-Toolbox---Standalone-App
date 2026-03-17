using EconToolbox.Desktop.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests;

[TestClass]
public class AdvancedBridgeReplacementViewModelTests
{
    [TestMethod]
    public void Recalculate_ComputesExpectedAnnualBenefit_FromWorkbookInputs()
    {
        var viewModel = new AdvancedBridgeReplacementViewModel
        {
            CostOfNewBridge = 29_405_000,
            LifeOfNewBridgeYears = 50,
            RemainingLifeOfExistingBridgeYears = 18,
            DiscountRate = 0.0275,
            AnnualOmAndRehabExistingBridge = 459_000,
            AnnualOmNewBridge = 25_000
        };

        Assert.AreEqual(0.0370409195, viewModel.CapitalRecoveryFactor, 1e-9);
        Assert.AreEqual(14_103_248.6502, viewModel.PwOfAvoidedFutureReplacementCost, 1e-3);
        Assert.AreEqual(6_097_164.7090, viewModel.PwOfOmRehabSavings, 1e-3);
        Assert.AreEqual(748_241.8851, viewModel.AverageAnnualAdvancedBridgeReplacementBenefit, 1e-3);
    }

    [TestMethod]
    public void Recalculate_UsesZeroRateFallbackFactors_WhenDiscountRateIsZero()
    {
        var viewModel = new AdvancedBridgeReplacementViewModel
        {
            CostOfNewBridge = 1_000_000,
            LifeOfNewBridgeYears = 50,
            RemainingLifeOfExistingBridgeYears = 10,
            DiscountRate = 0,
            AnnualOmAndRehabExistingBridge = 100_000,
            AnnualOmNewBridge = 20_000
        };

        Assert.AreEqual(0.02, viewModel.CapitalRecoveryFactor, 1e-12);
        Assert.AreEqual(80_000 * 10, viewModel.PwOfOmRehabSavings, 1e-6);
    }
}
