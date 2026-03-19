using System;
using System.ComponentModel;
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
        Assert.AreEqual(0d, viewModel.ExplorerPaneWidth, 0.1);
        Assert.AreEqual(312d, layoutService.LastSaved?.ExplorerPaneWidth ?? double.NaN, 0.1);

        viewModel.ToggleLeftPaneCommand.Execute(null);

        Assert.IsTrue(viewModel.IsExplorerPaneVisible);
        Assert.AreEqual(312d, viewModel.ExplorerPaneWidth, 0.1);
    }

    [TestMethod]
    public void ToggleRightPaneCommand_CollapsesAndRestoresDetailsPane()
    {
        var layoutService = new InMemoryLayoutSettingsService();
        var viewModel = CreateMainViewModel(layoutService);

        viewModel.DetailsPaneWidth = 376;

        viewModel.ToggleRightPaneCommand.Execute(null);

        Assert.IsFalse(viewModel.IsDetailsPaneVisible);
        Assert.AreEqual(0d, viewModel.DetailsPaneWidth, 0.1);
        Assert.AreEqual(376d, layoutService.LastSaved?.DetailsPaneWidth ?? double.NaN, 0.1);

        viewModel.ToggleRightPaneCommand.Execute(null);

        Assert.IsTrue(viewModel.IsDetailsPaneVisible);
        Assert.AreEqual(376d, viewModel.DetailsPaneWidth, 0.1);
    }

    private static MainViewModel CreateMainViewModel(InMemoryLayoutSettingsService layoutService)
    {
        var serviceProvider = new StubServiceProvider();
        return new MainViewModel(new ViewModelFactory(serviceProvider), new StubExcelExportService(), layoutService, new StubAppProgressService());
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    private sealed class StubAppProgressService : IAppProgressService
    {
        public bool IsActive => false;
        public double ProgressPercent => 0;
        public string Message => string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public void Start(string message, double percent = 0) { }
        public void Report(string message, double percent) { }
        public void Complete(string? message = null) { }
        public void Fail(string message) { }
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
