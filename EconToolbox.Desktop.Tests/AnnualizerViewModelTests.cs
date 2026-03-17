using System.Collections.Generic;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using EconToolbox.Desktop.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests;

[TestClass]
public class AnnualizerViewModelTests
{
    [TestMethod]
    public void AnnualBenefitEntries_TracksLinkedModuleBenefits_AndTotal()
    {
        var viewModel = new AnnualizerViewModel(new StubExcelExportService());

        viewModel.UpdateLinkedAnnualBenefit("recreation", 120_000);
        viewModel.UpdateLinkedAnnualBenefit("traffic-delay", 80_000);
        viewModel.UpdateLinkedAnnualBenefit("abr", 30_000);
        viewModel.AnnualBenefits = 1_000_000;

        Assert.AreEqual(4, viewModel.AnnualBenefitEntries.Count);
        Assert.AreEqual(1_000_000, viewModel.TotalAnnualBenefits, 0.001);

        var frm = Find(viewModel.AnnualBenefitEntries, "frm");
        Assert.IsNotNull(frm);
        Assert.AreEqual(770_000, frm!.Amount, 0.001);
    }

    private static AnnualBenefitEntry? Find(IEnumerable<AnnualBenefitEntry> entries, string key)
    {
        foreach (var entry in entries)
        {
            if (entry.Key == key)
            {
                return entry;
            }
        }

        return null;
    }

    private sealed class StubExcelExportService : IExcelExportService
    {
        public void ExportAll(EadViewModel ead, AgricultureDepthDamageViewModel agriculture, UpdatedCostViewModel updated, AnnualizerViewModel annualizer, WaterDemandViewModel waterDemand, UdvViewModel udv, RecreationCapacityViewModel recreationCapacity, GanttViewModel gantt, string filePath)
        {
        }

        public void ExportAnnualizer(double firstCost, double rate, double annualOm, double annualBenefits, IEnumerable<FutureCostEntry> future, double futureCostPv, double idc, double totalInvestment, double crf, double annualCost, double bcr, string filePath)
        {
        }

        public void ExportCapitalRecovery(double rate, int periods, double factor, string filePath)
        {
        }

        public void ExportEad(IEnumerable<EadViewModel.EadRow> rows, IEnumerable<string> damageColumns, bool useStage, bool calculateEqad, int analysisPeriod, double discountRate, string result, string filePath)
        {
        }

        public void ExportWaterDemand(IEnumerable<Scenario> scenarios, string filePath)
        {
        }
    }
}
