using System.Windows;
using EconToolbox.Desktop.Services;
using EconToolbox.Desktop.ViewModels;
using EconToolbox.Desktop.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests;

[TestClass]
public class ViewSmokeTests
{
    [STATestMethod]
    public void EadView_Renders_WithStubbedViewModel()
    {
        var view = new EadView
        {
            DataContext = new EadViewModel(new StubExcelExportService())
        };

        view.Measure(new Size(100, 100));
        Assert.IsNotNull(view.Content);
    }

    [STATestMethod]
    public void UpdatedCostView_Renders()
    {
        var view = new UpdatedCostView
        {
            DataContext = new UpdatedCostViewModel()
        };

        view.Measure(new Size(100, 100));
        Assert.IsNotNull(view.Content);
    }

    [STATestMethod]
    public void WaterDemandView_Renders_WithStubbedViewModel()
    {
        var view = new WaterDemandView
        {
            DataContext = new WaterDemandViewModel(new StubExcelExportService())
        };

        view.Measure(new Size(100, 100));
        Assert.IsNotNull(view.Content);
    }

    [STATestMethod]
    public void GanttView_Renders()
    {
        var view = new GanttView
        {
            DataContext = new GanttViewModel()
        };

        view.Measure(new Size(100, 100));
        Assert.IsNotNull(view.Content);
    }

    [STATestMethod]
    public void StageDamageOrganizerView_Renders()
    {
        var view = new StageDamageOrganizerView
        {
            DataContext = new StageDamageOrganizerViewModel()
        };

        view.Measure(new Size(100, 100));
        Assert.IsNotNull(view.Content);
    }

    private sealed class StubExcelExportService : IExcelExportService
    {
        public void ExportAll(EadViewModel ead, AgricultureDepthDamageViewModel agriculture, UpdatedCostViewModel updated, AnnualizerViewModel annualizer, WaterDemandViewModel waterDemand, UdvViewModel udv, RecreationCapacityViewModel recreationCapacity, GanttViewModel gantt, string filePath)
        {
        }

        public void ExportAnnualizer(double firstCost, double rate, double annualOm, double annualBenefits, System.Collections.Generic.IEnumerable<Models.FutureCostEntry> future, double futureCostPv, double idc, double totalInvestment, double crf, double annualCost, double bcr, string filePath)
        {
        }

        public void ExportCapitalRecovery(double rate, int periods, double factor, string filePath)
        {
        }

        public void ExportEad(System.Collections.Generic.IEnumerable<EadViewModel.EadRow> rows, System.Collections.Generic.IEnumerable<string> damageColumns, bool useStage, bool calculateEqad, int analysisPeriod, double futureDamages, string result, string filePath)
        {
        }

        public void ExportWaterDemand(System.Collections.Generic.IEnumerable<Models.Scenario> scenarios, string filePath)
        {
        }
    }
}
