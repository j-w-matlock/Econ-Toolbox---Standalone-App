using System;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using EconToolbox.Desktop.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests;

[TestClass]
public class MainViewModelPaneToggleTests
{
    [TestMethod]
    public void ToggleLeftPaneCommand_CollapsesAndRestoresExplorerPane()
    {
        var layoutService = new InMemoryLayoutSettingsService();
        var viewModel = CreateMainViewModel(layoutService);

        viewModel.ExplorerPaneWidth = 312;

        viewModel.ToggleLeftPaneCommand.Execute(null);

        Assert.IsFalse(viewModel.IsExplorerPaneVisible);
        Assert.AreEqual(0, viewModel.ExplorerPaneWidth, 0.1);
        Assert.AreEqual(312, layoutService.LastSaved?.ExplorerPaneWidth, 0.1);

        viewModel.ToggleLeftPaneCommand.Execute(null);

        Assert.IsTrue(viewModel.IsExplorerPaneVisible);
        Assert.AreEqual(312, viewModel.ExplorerPaneWidth, 0.1);
    }

    [TestMethod]
    public void ToggleRightPaneCommand_CollapsesAndRestoresDetailsPane()
    {
        var layoutService = new InMemoryLayoutSettingsService();
        var viewModel = CreateMainViewModel(layoutService);

        viewModel.DetailsPaneWidth = 376;

        viewModel.ToggleRightPaneCommand.Execute(null);

        Assert.IsFalse(viewModel.IsDetailsPaneVisible);
        Assert.AreEqual(0, viewModel.DetailsPaneWidth, 0.1);
        Assert.AreEqual(376, layoutService.LastSaved?.DetailsPaneWidth, 0.1);

        viewModel.ToggleRightPaneCommand.Execute(null);

        Assert.IsTrue(viewModel.IsDetailsPaneVisible);
        Assert.AreEqual(376, viewModel.DetailsPaneWidth, 0.1);
    }

    private static MainViewModel CreateMainViewModel(InMemoryLayoutSettingsService layoutService)
    {
        return new MainViewModel(new ViewModelFactory(), new StubExcelExportService(), layoutService);
    }

    private sealed class InMemoryLayoutSettingsService : ILayoutSettingsService
    {
        private LayoutSettings _settings = new();

        public LayoutSettings? LastSaved { get; private set; }

        public LayoutSettings Load() => new()
        {
            ExplorerPaneWidth = _settings.ExplorerPaneWidth,
            DetailsPaneWidth = _settings.DetailsPaneWidth,
            IsExplorerPaneVisible = _settings.IsExplorerPaneVisible,
            IsDetailsPaneVisible = _settings.IsDetailsPaneVisible
        };

        public void Save(LayoutSettings settings)
        {
            LastSaved = new LayoutSettings
            {
                ExplorerPaneWidth = settings.ExplorerPaneWidth,
                DetailsPaneWidth = settings.DetailsPaneWidth,
                IsExplorerPaneVisible = settings.IsExplorerPaneVisible,
                IsDetailsPaneVisible = settings.IsDetailsPaneVisible
            };

            _settings = LastSaved;
        }
    }

    private sealed class StubExcelExportService : IExcelExportService
    {
        public void ExportAll(EadViewModel ead, AgricultureDepthDamageViewModel agriculture, UpdatedCostViewModel updated, AnnualizerViewModel annualizer, WaterDemandViewModel waterDemand, UdvViewModel udv, RecreationCapacityViewModel recreationCapacity, GanttViewModel gantt, string filePath)
        {
        }

        public void ExportAnnualizer(double firstCost, double rate, double annualOm, double annualBenefits, System.Collections.Generic.IEnumerable<FutureCostEntry> future, double futureCostPv, double idc, double totalInvestment, double crf, double annualCost, double bcr, string filePath)
        {
        }

        public void ExportCapitalRecovery(double rate, int periods, double factor, string filePath)
        {
        }

        public void ExportEad(System.Collections.Generic.IEnumerable<EadViewModel.EadRow> rows, System.Collections.Generic.IEnumerable<string> damageColumns, bool useStage, bool calculateEqad, int analysisPeriod, double discountRate, string result, string filePath)
        {
        }

        public void ExportWaterDemand(System.Collections.Generic.IEnumerable<Scenario> scenarios, string filePath)
        {
        }
    }
}
