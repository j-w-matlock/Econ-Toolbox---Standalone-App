using System.Collections.Generic;
using System.Windows.Media;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Services;

public interface IExcelExportService
{
    void ExportCapitalRecovery(double rate, int periods, double factor, string filePath);

    void ExportWaterDemand(IEnumerable<Scenario> scenarios, string filePath);

    void ExportAnnualizer(
        double firstCost,
        double rate,
        double annualOm,
        double annualBenefits,
        IEnumerable<FutureCostEntry> future,
        double idc,
        double totalInvestment,
        double crf,
        double annualCost,
        double bcr,
        string filePath);

    void ExportEad(
        IEnumerable<EadViewModel.EadRow> rows,
        IEnumerable<string> damageColumns,
        bool useStage,
        string result,
        IReadOnlyList<Point> stagePoints,
        IReadOnlyList<Point> frequencyPoints,
        string filePath);

    void ExportAll(
        EadViewModel ead,
        AgricultureDepthDamageViewModel agriculture,
        UpdatedCostViewModel updated,
        AnnualizerViewModel annualizer,
        WaterDemandViewModel waterDemand,
        UdvViewModel udv,
        RecreationCapacityViewModel recreationCapacity,
        MindMapViewModel mindMap,
        GanttViewModel gantt,
        DrawingViewModel drawing,
        IReadOnlyList<Point> eadStagePoints,
        IReadOnlyList<Point> eadFrequencyPoints,
        string filePath);
}
