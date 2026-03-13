using EconToolbox.Desktop.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests;

[TestClass]
public class TrafficDelayAnalysisViewModelTests
{
    [TestMethod]
    public void Recalculate_ComputesAnnualizedAndDiscountedTrafficImpacts()
    {
        var viewModel = new TrafficDelayAnalysisViewModel
        {
            AepThatCausesDelay = 0.5,
            DiscountRatePercent = 2.5,
            AnalysisPeriodYears = 30
        };

        var expectedAnnualized = viewModel.TotalTrafficImpacts * 0.5;
        var rate = 0.025;
        var expectedDiscounted = expectedAnnualized * ((1 - System.Math.Pow(1 + rate, -30)) / rate);

        Assert.AreEqual(expectedAnnualized, viewModel.AnnualizedTrafficImpacts, 1e-6);
        Assert.AreEqual(expectedDiscounted, viewModel.DiscountedTrafficImpactsOverAnalysisPeriod, 1e-6);
    }

    [TestMethod]
    public void Recalculate_UsesUndiscountedSeriesWhenDiscountRateIsZero()
    {
        var viewModel = new TrafficDelayAnalysisViewModel
        {
            AepThatCausesDelay = 0.25,
            DiscountRatePercent = 0,
            AnalysisPeriodYears = 10
        };

        Assert.AreEqual(
            viewModel.AnnualizedTrafficImpacts * 10,
            viewModel.DiscountedTrafficImpactsOverAnalysisPeriod,
            1e-6);
    }

    [TestMethod]
    public void Recalculate_ReturnsZeroDiscountedImpactWhenOptionalAnnualizationInputsAreNotProvided()
    {
        var viewModel = new TrafficDelayAnalysisViewModel();

        Assert.AreEqual(0, viewModel.AnnualizedTrafficImpacts, 1e-6);
        Assert.AreEqual(0, viewModel.DiscountedTrafficImpactsOverAnalysisPeriod, 1e-6);
    }
}
