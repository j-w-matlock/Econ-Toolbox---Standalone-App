using System;
using System.Collections.Generic;
using System.Diagnostics;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using EconToolbox.Desktop.Models;
using System.Linq;
using EconToolbox.Desktop.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;
using System.Threading;
using static EconToolbox.Desktop.Services.ExcelNamingPolicy;
using EconToolbox.Desktop.Themes;

namespace EconToolbox.Desktop.Services
{
    public sealed class ExcelExportService : IExcelExportService
    {

        private static Color ChartBlue => ThemeResourceHelper.GetColor("App.Chart.Series1.Color", (Color)ColorConverter.ConvertFromString("#2D6A8E"));
        private static Color ChartTeal => ThemeResourceHelper.GetColor("App.Chart.Series2.Color", (Color)ColorConverter.ConvertFromString("#1ABC9C"));
        private static Color ChartOrange => ThemeResourceHelper.GetColor("App.Chart.Series4.Color", (Color)ColorConverter.ConvertFromString("#F39C12"));
        private static Color ChartPlum => ThemeResourceHelper.GetColor("App.Chart.Series3.Color", (Color)ColorConverter.ConvertFromString("#7F56D9"));
        private static Color ChartGray => ThemeResourceHelper.GetColor("App.TextSecondary.Color", (Color)ColorConverter.ConvertFromString("#6B7280"));

        private static XLColor DashboardHeaderFill => ToXLColor(ThemeResourceHelper.GetColor("App.Chart.Series1.Color", (Color)ColorConverter.ConvertFromString("#2D6A8E")));
        private static XLColor DashboardHeaderText => ToXLColor(ThemeResourceHelper.GetColor("App.OnAccent.Color", Colors.White));
        private static XLColor DashboardSubHeaderFill => ToXLColor(ThemeResourceHelper.GetColor("App.HighlightBackground.Color", (Color)ColorConverter.ConvertFromString("#EFF5FB")));
        private static XLColor DashboardRowLight => ToXLColor(ThemeResourceHelper.GetColor("App.Surface.Color", Colors.White));
        private static XLColor DashboardRowAlt => ToXLColor(ThemeResourceHelper.GetColor("App.SurfaceAlt.Color", (Color)ColorConverter.ConvertFromString("#F6F9FC")));
        private static XLColor DashboardBorder => ToXLColor(ThemeResourceHelper.GetColor("App.Border.Color", (Color)ColorConverter.ConvertFromString("#D0D7E5")));
        private static XLColor DashboardAccentText => ToXLColor(ThemeResourceHelper.GetColor("App.Accent.Color", (Color)ColorConverter.ConvertFromString("#2D6A8E")));
        private static XLColor DashboardPrimaryText => ToXLColor(ThemeResourceHelper.GetColor("App.TextPrimary.Color", (Color)ColorConverter.ConvertFromString("#1F2937")));

        private static XLColor ToXLColor(Color color)
        {
            return XLColor.FromArgb(color.A, color.R, color.G, color.B);
        }

        private static void LogWarning(string message)
        {
            Debug.WriteLine($"[ExcelExport] {message}");
        }

        private static void LogError(string message, Exception ex)
        {
            Debug.WriteLine($"[ExcelExport] ERROR: {message}\n{ex}");
        }

        private sealed class ExportContext : IDisposable
        {
            private readonly HashSet<string> _tableNames = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _pictureNames = new(StringComparer.OrdinalIgnoreCase);

            public ExportContext()
            {
                try
                {
                    Workbook = new XLWorkbook();
                }
                catch (Exception ex)
                {
                    LogError("Failed to create Excel workbook instance.", ex);
                    throw new InvalidOperationException("Unable to create an Excel workbook for export.", ex);
                }
            }

            public XLWorkbook Workbook { get; }

            public IXLWorksheet CreateWorksheet(string name)
            {
                var worksheet = Workbook.Worksheets.Add(ExcelNamingPolicy.CreateWorksheetName(Workbook, name, LogWarning));
                worksheet.Style.Font.SetFontName("Segoe UI");
                return worksheet;
            }

            public string GetTableName(string baseName)
            {
                return CreateUniqueName(baseName, _tableNames, ExcelMaxTableNameLength, "Tbl", true, LogWarning);
            }

            public string GetPictureName(string prefix)
            {
                return CreateUniqueName(prefix, _pictureNames, ExcelMaxPictureNameLength, "Picture", false, LogWarning);
            }

            public void Save(string filePath)
            {
                Workbook.SaveAs(filePath);
            }

            public void Dispose()
            {
                Workbook.Dispose();
            }
        }

        private static void WriteHeaderRow(IXLWorksheet ws, int row, int startColumn, IReadOnlyList<string> headers, bool center = true, bool includeBorder = true)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                var headerCell = ws.Cell(row, startColumn + i);
                headerCell.Value = headers[i];
                headerCell.Style.Font.SetBold();
                headerCell.Style.Fill.BackgroundColor = DashboardSubHeaderFill;
                headerCell.Style.Alignment.Horizontal = center ? XLAlignmentHorizontalValues.Center : XLAlignmentHorizontalValues.Left;
                if (includeBorder)
                {
                    headerCell.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                    headerCell.Style.Border.OutsideBorderColor = DashboardBorder;
                }
            }
        }

        private static IReadOnlyList<string> WriteSanitizedHeaders(IXLWorksheet ws, int row, int startColumn, IEnumerable<string> headers, string fallbackPrefix, bool center = true, bool includeBorder = true)
        {
            var sanitizedHeaders = ExcelNamingPolicy.SanitizeHeaders(headers, fallbackPrefix, LogWarning);
            WriteHeaderRow(ws, row, startColumn, sanitizedHeaders, center, includeBorder);
            return sanitizedHeaders;
        }

        private static string JoinOrEmpty(string separator, IEnumerable<string>? values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            return string.Join(separator, values.Where(v => v != null));
        }

        private static void RunOnSta(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception is not null)
            {
                throw exception;
            }
        }

        private static void ExecuteExport(string operation, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogError(operation, ex);
                throw new InvalidOperationException($"Failed to {operation}. {ex.Message}", ex);
            }
        }

        public void ExportCapitalRecovery(double rate, int periods, double factor, string filePath)
        {
            ExecuteExport("export the capital recovery worksheet", () =>
            {
                RunOnSta(() =>
                {
                    using var context = new ExportContext();
                    var ws = context.CreateWorksheet("CapitalRecovery");

                    var entries = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
                    {
                        ("Interest Rate", rate, "0.00%", "Input discount rate used to compute the capital recovery factor.", false),
                        ("Number of Periods", periods, "0", "Total compounding periods used in the schedule.", false),
                        ("Capital Recovery Factor", factor, "0.000000", "r(1+r)^n / ((1+r)^n - 1)", true)
                    };

                    int nextRow = WriteKeyValueTable(ws, 1, 1, "Capital Recovery Factor", entries, context);
                    var noteRange = ws.Range(nextRow, 1, nextRow, 2);
                    noteRange.Merge();
                    noteRange.Value = "Use this sheet to document standalone capital recovery calculations for audit packages.";
                    noteRange.Style.Alignment.WrapText = true;
                    noteRange.Style.Fill.BackgroundColor = DashboardRowLight;
                    noteRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                    noteRange.Style.Border.OutsideBorderColor = DashboardBorder;

                    ws.Columns(1, 2).AdjustToContents();
                    context.Save(filePath);
                });
            });
        }

        public void ExportWaterDemand(IEnumerable<Scenario> scenarios, string filePath)
        {
            ExecuteExport("export the water demand workbooks", () =>
            {
                RunOnSta(() =>
                {
                    using var context = new ExportContext();
                int scenarioIndex = 1;

                foreach (var scenario in scenarios)
                {
                    string baseName = string.IsNullOrWhiteSpace(scenario.Name) ? $"Scenario {scenarioIndex}" : scenario.Name;
                    var ws = context.CreateWorksheet(baseName);

                    var headers = new List<string>
                    {
                        "Year",
                        "Growth Rate",
                        "Demand (MGD)",
                        "Residential (MGD)",
                        "Commercial (MGD)",
                        "Industrial (MGD)",
                        "Agricultural (MGD)",
                        "Adjusted (MGD)",
                        "Adjusted (ac-ft/yr)"
                    };

                    WriteHeaderRow(ws, 1, 1, headers);

                    ws.Cell(1, 3).GetComment().AddText("Demand = Prior Demand × (1 + Growth Rate)");
                    ws.Cell(1, 4).GetComment().AddText("Residential = Demand × Residential %");
                    ws.Cell(1, 5).GetComment().AddText("Commercial = Demand × Commercial %");
                    ws.Cell(1, 6).GetComment().AddText("Industrial = Demand × Industrial %");
                    ws.Cell(1, 7).GetComment().AddText("Agricultural = Demand × Agricultural %");
                    ws.Cell(1, 8).GetComment().AddText("Adjusted = Demand ÷ (1 - Losses %) × (1 - Improvements %)");
                    ws.Cell(1, 9).GetComment().AddText("Adjusted Acre-Feet = Adjusted Demand × 365 ÷ 325,851");

                    int row = 2;
                    foreach (var result in scenario.Results)
                    {
                        ws.Cell(row, 1).Value = result.Year;
                        ws.Cell(row, 1).Style.NumberFormat.Format = "0";

                        ws.Cell(row, 2).Value = result.GrowthRate;
                        ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%";

                        ws.Cell(row, 3).Value = result.Demand;
                        ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";

                        ws.Cell(row, 4).Value = result.ResidentialDemand;
                        ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";

                        ws.Cell(row, 5).Value = result.CommercialDemand;
                        ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";

                        ws.Cell(row, 6).Value = result.IndustrialDemand;
                        ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";

                        ws.Cell(row, 7).Value = result.AgriculturalDemand;
                        ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";

                        ws.Cell(row, 8).Value = result.AdjustedDemand;
                        ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";

                        ws.Cell(row, 9).Value = result.AdjustedDemandAcreFeet;
                        ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                        row++;
                    }

                    if (!string.IsNullOrWhiteSpace(scenario.Description))
                    {
                    var descriptionRange = ws.Range(row, 1, row, headers.Count);
                        descriptionRange.Merge();
                        descriptionRange.Value = scenario.Description;
                        descriptionRange.Style.Alignment.WrapText = true;
                        descriptionRange.Style.Fill.BackgroundColor = DashboardRowLight;
                        descriptionRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                        descriptionRange.Style.Border.OutsideBorderColor = DashboardBorder;
                        row += 2;
                    }
                    else
                    {
                        row += 1;
                    }

                    int dataEndRow = Math.Max(2, scenario.Results.Count + 1);
                    var dataRange = ws.Range(1, 1, dataEndRow, headers.Count);
                    var table = dataRange.CreateTable(context.GetTableName($"WaterDemand_{baseName}"));
                    table.Theme = XLTableTheme.TableStyleMedium4;

                    ws.Columns(1, headers.Count).AdjustToContents();

                    if (scenario.Results.Count >= 2)
                    {
                        AddWaterDemandChart(ws, new[] { scenario }, row + 1, 1, context);
                    }

                    scenarioIndex++;
                }

                context.Save(filePath);
            });
            });
        }

        public void ExportAnnualizer(double firstCost, double rate, double annualOm, double annualBenefits, IEnumerable<FutureCostEntry> future, double futureCostPv, double idc, double totalInvestment, double crf, double annualCost, double bcr, string filePath)
        {
            ExecuteExport("export the annualizer workbook", () =>
            {
                RunOnSta(() =>
                {
                    using var context = new ExportContext();

                var summary = context.CreateWorksheet("Summary");

            var summaryRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("First Cost", firstCost, "$#,##0.00", "Initial investment entered in the calculator.", false),
                ("Discount Rate", rate / 100d, "0.00%", "Interest rate applied to the annualization.", false),
                ("Annual O&M", annualOm, "$#,##0.00", "Recurring operations and maintenance costs.", false),
                ("Annual Benefits", annualBenefits, "$#,##0.00", "Benefits used in the benefit-cost ratio.", false),
                ("PV of Future Costs", futureCostPv, "$#,##0.00", "Present value of scheduled future capital spending.", false),
                ("Interest During Construction", idc, "$#,##0.00", "Calculated from first cost, rate, and IDC schedule.", false),
                ("Total Investment", totalInvestment, "$#,##0.00", "First Cost + IDC + PV of Future Costs.", true),
                ("Capital Recovery Factor", crf, "0.000000", "r(1+r)^n / ((1+r)^n - 1)", false),
                ("Annual Cost", annualCost, "$#,##0.00", "Total Investment × CRF + Annual O&M.", true),
                ("Benefit-Cost Ratio", bcr, "0.00", "Annual Benefits ÷ Annual Cost.", true)
            };

            int nextRow = WriteKeyValueTable(summary, 1, 1, "Annualization Summary", summaryRows, context);
            AddAnnualizerComparisonChart(summary, annualBenefits, annualCost, bcr, 1, 5, context);

            var noteRange = summary.Range(nextRow, 1, nextRow, 2);
            noteRange.Merge();
            noteRange.Value = "Document assumptions for the IDC schedule and benefit streams alongside this sheet when routing for review.";
            noteRange.Style.Alignment.WrapText = true;
            noteRange.Style.Fill.BackgroundColor = DashboardRowAlt;
            noteRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            noteRange.Style.Border.OutsideBorderColor = DashboardBorder;

            var futureSheet = context.CreateWorksheet("FutureCosts");

            var futureHeaders = new List<string> { "Nominal Cost", "Year", "Timing", "PV Factor" };
            WriteHeaderRow(futureSheet, 1, 1, futureHeaders);

            int futureRow = 2;
            foreach (var entry in future)
            {
                futureSheet.Cell(futureRow, 1).Value = entry.Cost;
                futureSheet.Cell(futureRow, 1).Style.NumberFormat.Format = "$#,##0.00";

                futureSheet.Cell(futureRow, 2).Value = entry.Year;
                futureSheet.Cell(futureRow, 2).Style.NumberFormat.Format = "0";

                futureSheet.Cell(futureRow, 3).Value = entry.Timing;
                futureSheet.Cell(futureRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                futureSheet.Cell(futureRow, 4).Value = entry.PvFactor;
                futureSheet.Cell(futureRow, 4).Style.NumberFormat.Format = "0.000000";

                futureRow++;
            }

            int futureEndRow = Math.Max(2, futureRow - 1);
            var futureRange = futureSheet.Range(1, 1, futureEndRow, futureHeaders.Count);
            var futureTable = futureRange.CreateTable(context.GetTableName("AnnualizerFuture"));
            futureTable.Theme = XLTableTheme.TableStyleLight11;
            futureSheet.Columns(1, futureHeaders.Count).AdjustToContents();

                context.Save(filePath);
            });
            });
        }

        public void ExportEad(IEnumerable<EadViewModel.EadRow> rows, IEnumerable<string> damageColumns, bool useStage, string result, string filePath)
        {
            ExecuteExport("export the expected annual damage workbook", () =>
            {
                RunOnSta(() =>
                {
                    using var context = new ExportContext();

            var inputSheet = context.CreateWorksheet("EAD Inputs");

            var damageList = damageColumns.ToList();
            var sanitizedDamageList = SanitizeHeaders(damageList, "Damage");
            var rowList = rows.ToList();

            var headers = new List<string> { "Probability" };
            if (useStage)
            {
                headers.Add("Stage");
            }
            headers.AddRange(sanitizedDamageList);

            WriteHeaderRow(inputSheet, 1, 1, headers);

            int rowIndex = 2;
            foreach (var entry in rowList)
            {
                inputSheet.Cell(rowIndex, 1).Value = entry.Probability;
                inputSheet.Cell(rowIndex, 1).Style.NumberFormat.Format = "0.000";

                int columnIndex = 2;
                if (useStage)
                {
                    inputSheet.Cell(rowIndex, columnIndex).Value = entry.Stage;
                    columnIndex++;
                }

                for (int i = 0; i < sanitizedDamageList.Count; i++)
                {
                    double value = entry.Damages.Count > i ? entry.Damages[i] : 0.0;
                    inputSheet.Cell(rowIndex, columnIndex + i).Value = value;
                    inputSheet.Cell(rowIndex, columnIndex + i).Style.NumberFormat.Format = "$#,##0.00";
                }

                rowIndex++;
            }

            int dataRowCount = Math.Max(2, rowIndex - 1);
            var dataRange = inputSheet.Range(1, 1, dataRowCount, headers.Count);
            var table = dataRange.CreateTable(context.GetTableName("EadInputs"));
            table.Theme = XLTableTheme.TableStyleMedium9;
            inputSheet.Columns(1, headers.Count).AdjustToContents();

            int chartRow = dataRowCount + 3;
            AddEadChart(inputSheet, BuildEadPlotPoints(rowList, useStage), chartRow, 1, context);

            var summary = context.CreateWorksheet("Summary");

                    var summaryEntries = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
                    {
                        ("Frequency-Damage Points", rowList.Count, "0", "Number of rows exported from the calculator.", false),
                        ("Includes Stage Data", useStage ? "Yes" : "No", null, useStage ? "Stage values were captured alongside each probability." : "Stage values were not provided.", false)
                    };

                    if (rowList.Count > 0 && sanitizedDamageList.Count > 0)
                    {
                        var probabilities = rowList.Select(r => r.Probability).ToArray();
                        for (int i = 0; i < sanitizedDamageList.Count; i++)
                        {
                            var damages = rowList.Select(r => r.Damages.Count > i ? r.Damages[i] : 0.0).ToArray();
                            double eadValue = EadModel.Compute(probabilities, damages);
                            summaryEntries.Add(($"{sanitizedDamageList[i]} EAD", eadValue, "$#,##0.00", "Expected annual damage for this column.", i == 0));
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        summaryEntries.Add(("Narrative", result, null, "Summary text generated by the module.", false));
                    }

                    int summaryNextRow = WriteKeyValueTable(summary, 1, 1, "Expected Annual Damage", summaryEntries, context);
                    AddEadChart(summary, BuildEadPlotPoints(rowList, useStage), summaryNextRow, 1, context);

                    context.Save(filePath);
                });
            });
        }

        public void ExportAll(EadViewModel ead, AgricultureDepthDamageViewModel agriculture, UpdatedCostViewModel updated, AnnualizerViewModel annualizer, WaterDemandViewModel waterDemand, UdvViewModel udv, RecreationCapacityViewModel recreationCapacity, GanttViewModel gantt, string filePath)
        {
            ExecuteExport("export the combined workbook", () =>
            {
                RunOnSta(() =>
                {
                    using var context = new ExportContext();

            // EAD Sheet
            var eadSheet = context.CreateWorksheet("EAD");
            int col = 1;
            eadSheet.Cell(1, col++).Value = "Probability";
            if (ead.UseStage)
                eadSheet.Cell(1, col++).Value = "Stage";
            var damageHeaders = SanitizeHeaders(ead.DamageColumns.Select(c => c.Name), "Damage");
            int dcCount = damageHeaders.Count;
            foreach (var name in damageHeaders)
                eadSheet.Cell(1, col++).Value = name;
            int eadColumnCount = col - 1;
            int rowIdx = 2;
            foreach (var r in ead.Rows)
            {
                col = 1;
                eadSheet.Cell(rowIdx, col++).Value = r.Probability;
                if (ead.UseStage)
                    eadSheet.Cell(rowIdx, col++).Value = r.Stage;
                for (int i = 0; i < dcCount; i++)
                    eadSheet.Cell(rowIdx, col++).Value = r.Damages.Count > i ? r.Damages[i] : 0;
                rowIdx++;
            }
            if (rowIdx > 2)
            {
                var eadRange = eadSheet.Range(1, 1, rowIdx - 1, eadColumnCount);
                var eadTable = eadRange.CreateTable(context.GetTableName("EadInputs"));
                eadTable.Theme = XLTableTheme.TableStyleMedium9;
            }
            eadSheet.Cell(rowIdx + 1, 1).Value = "Result";
            eadSheet.Cell(rowIdx + 1, 1).Style.Font.SetBold();
            eadSheet.Cell(rowIdx + 1, 2).Value = JoinOrEmpty(" | ", ead.Results.Select(r => $"{r.Label}: {r.Result}"));
            eadSheet.Range(rowIdx + 1, 1, rowIdx + 1, Math.Max(2, eadColumnCount)).Merge();
            eadSheet.Range(1, 1, 1, eadColumnCount).Style.Font.SetBold();
            eadSheet.Columns(1, eadColumnCount).AdjustToContents();
            AddEadChart(eadSheet, BuildEadPlotPoints(ead.Rows, ead.UseStage), rowIdx + 3, 1, context);

            // Agriculture Depth-Damage Sheet
            var agSheet = context.CreateWorksheet("Agriculture");
            agSheet.Cell(1, 1).Value = "Agriculture Depth-Damage";
            agSheet.Range(1, 1, 1, 6).Merge();
            agSheet.Cell(1, 1).Style.Font.SetBold();
            agSheet.Cell(1, 1).Style.Font.FontSize = 16;
            agSheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            int agRow = 3;
            var agSummary = new (string Label, object Value, string? Format)[]
            {
                ("Region", agriculture.SelectedRegion?.Name ?? "Not selected", null),
                ("Crop", agriculture.SelectedCrop?.Name ?? "Not selected", null),
                ("Simulation Seasons", agriculture.SimulationYears, "0"),
                ("Modeled Impact Probability", agriculture.ModeledImpactProbability, "0.00%"),
                ("Average Damage", agriculture.MeanDamagePercent / 100.0, "0.00%"),
                ("CropScape Acreage", agriculture.CropScapeTotalAcreage, "#,##0.0")
            };

            foreach (var entry in agSummary)
            {
                agSheet.Cell(agRow, 1).Value = entry.Label;
                agSheet.Cell(agRow, 1).Style.Font.SetBold();
                var valueCell = agSheet.Cell(agRow, 2);
                SetCellValue(valueCell, entry.Value);

                switch (entry.Value)
                {
                    case double or float or decimal:
                        if (!string.IsNullOrWhiteSpace(entry.Format))
                            valueCell.Style.NumberFormat.Format = entry.Format;
                        break;
                    case int or long or short or byte or uint or ulong or ushort:
                        valueCell.Style.NumberFormat.Format = string.IsNullOrWhiteSpace(entry.Format) ? "0" : entry.Format;
                        break;
                }
                agRow++;
            }

            if (!string.IsNullOrWhiteSpace(agriculture.ImpactSummary))
            {
                agRow++;
                agSheet.Cell(agRow, 1).Value = agriculture.ImpactSummary;
                agSheet.Range(agRow, 1, agRow, 6).Merge();
                agSheet.Range(agRow, 1, agRow, 6).Style.Alignment.WrapText = true;
                agSheet.Range(agRow, 1, agRow, 6).Style.Fill.BackgroundColor = DashboardRowLight;
                agRow += 2;
            }

            if (!string.IsNullOrWhiteSpace(agriculture.CropInsight))
            {
                agSheet.Cell(agRow, 1).Value = agriculture.CropInsight;
                agSheet.Range(agRow, 1, agRow, 6).Merge();
                agSheet.Range(agRow, 1, agRow, 6).Style.Alignment.WrapText = true;
                agSheet.Range(agRow, 1, agRow, 6).Style.Fill.BackgroundColor = DashboardRowAlt;
                agRow += 2;
            }

            if (agriculture.StageExposures.Count > 0)
            {
                var stageHeaders = new List<string> { "Stage", "Exposure Days", "Tolerance (days)", "Overlap %", "Timing Modifier", "Guidance" };
                WriteHeaderRow(agSheet, agRow, 1, stageHeaders, center: false, includeBorder: false);
                int stageStart = agRow;
                agRow++;
                foreach (var stage in agriculture.StageExposures)
                {
                    agSheet.Cell(agRow, 1).Value = stage.StageName;
                    agSheet.Cell(agRow, 2).Value = stage.ExposureDays;
                    agSheet.Cell(agRow, 2).Style.NumberFormat.Format = "0.0";
                    agSheet.Cell(agRow, 3).Value = stage.FloodToleranceDays;
                    agSheet.Cell(agRow, 3).Style.NumberFormat.Format = "0.0";
                    agSheet.Cell(agRow, 4).Value = stage.OverlapFraction;
                    agSheet.Cell(agRow, 4).Style.NumberFormat.Format = "0.0%";
                    agSheet.Cell(agRow, 5).Value = stage.TimingModifier;
                    agSheet.Cell(agRow, 5).Style.NumberFormat.Format = "0.00";
                    agSheet.Cell(agRow, 6).Value = stage.StageGuidance;
                    agSheet.Cell(agRow, 6).Style.Alignment.WrapText = true;
                    agRow++;
                }
                var stageRange = agSheet.Range(stageStart, 1, agRow - 1, stageHeaders.Count);
                var stageTable = stageRange.CreateTable(context.GetTableName("AgricultureStages"));
                stageTable.Theme = XLTableTheme.TableStyleMedium2;
                agRow += 1;
            }

            // Use a distinct variable name to avoid conflicts with the earlier EAD damage headers.
            var agDamageHeaders = agriculture.CropScapeDamageRows.Count > 0
                ? new List<string> { "Depth (ft)", "Duration (days)", "Damage (%)", "Damaged Acres", "Residual Acres", "Total Acres" }
                : new List<string> { "Depth (ft)", "Duration (days)", "Damage (%)" };

            WriteHeaderRow(agSheet, agRow, 1, agDamageHeaders, center: false, includeBorder: false);

            int damageRowStart = agRow;
            agRow++;
            if (agriculture.CropScapeDamageRows.Count > 0)
            {
                foreach (var row in agriculture.CropScapeDamageRows)
                {
                    agSheet.Cell(agRow, 1).Value = row.DepthFeet;
                    agSheet.Cell(agRow, 1).Style.NumberFormat.Format = "0.00";
                    agSheet.Cell(agRow, 2).Value = row.DurationDays;
                    agSheet.Cell(agRow, 2).Style.NumberFormat.Format = "0.00";
                    agSheet.Cell(agRow, 3).Value = row.DamagePercent / 100.0;
                    agSheet.Cell(agRow, 3).Style.NumberFormat.Format = "0.00%";
                    agSheet.Cell(agRow, 4).Value = row.DamagedAcres;
                    agSheet.Cell(agRow, 4).Style.NumberFormat.Format = "#,##0.0";
                    agSheet.Cell(agRow, 5).Value = row.ResidualAcres;
                    agSheet.Cell(agRow, 5).Style.NumberFormat.Format = "#,##0.0";
                    agSheet.Cell(agRow, 6).Value = row.TotalAcres;
                    agSheet.Cell(agRow, 6).Style.NumberFormat.Format = "#,##0.0";
                    agRow++;
                }
            }
            else
            {
                foreach (var row in agriculture.DepthDurationRows)
                {
                    agSheet.Cell(agRow, 1).Value = row.DepthFeet;
                    agSheet.Cell(agRow, 1).Style.NumberFormat.Format = "0.00";
                    agSheet.Cell(agRow, 2).Value = row.DurationDays;
                    agSheet.Cell(agRow, 2).Style.NumberFormat.Format = "0.00";
                    agSheet.Cell(agRow, 3).Value = row.DamagePercent / 100.0;
                    agSheet.Cell(agRow, 3).Style.NumberFormat.Format = "0.00%";
                    agRow++;
                }
            }

            if (agRow > damageRowStart + 1)
            {
                var agRange = agSheet.Range(damageRowStart, 1, agRow - 1, agDamageHeaders.Count);
                var agTable = agRange.CreateTable(context.GetTableName("AgricultureDamage"));
                agTable.Theme = XLTableTheme.TableStyleMedium9;
            }

            if (agriculture.CropScapeSummaries.Count > 0)
            {
                agRow += 2;
                agSheet.Cell(agRow, 1).Value = "CropScape Summary";
                agSheet.Cell(agRow, 1).Style.Font.SetBold();
                agRow++;
                agSheet.Cell(agRow, 1).Value = "Code";
                agSheet.Cell(agRow, 2).Value = "Name";
                agSheet.Cell(agRow, 3).Value = "Pixels";
                agSheet.Cell(agRow, 4).Value = "Acres";
                agSheet.Cell(agRow, 5).Value = "% of Total";
                for (int i = 0; i < 5; i++)
                {
                    agSheet.Cell(agRow, i + 1).Style.Font.SetBold();
                    agSheet.Cell(agRow, i + 1).Style.Fill.BackgroundColor = DashboardSubHeaderFill;
                }
                int summaryStart = agRow;
                agRow++;
                foreach (var summary in agriculture.CropScapeSummaries)
                {
                    agSheet.Cell(agRow, 1).Value = summary.Code;
                    agSheet.Cell(agRow, 2).Value = summary.Name;
                    agSheet.Cell(agRow, 3).Value = summary.PixelCount;
                    agSheet.Cell(agRow, 4).Value = summary.Acres;
                    agSheet.Cell(agRow, 4).Style.NumberFormat.Format = "#,##0.0";
                    agSheet.Cell(agRow, 5).Value = summary.PercentOfTotal;
                    agSheet.Cell(agRow, 5).Style.NumberFormat.Format = "0.0%";
                    agRow++;
                }

                var summaryRange = agSheet.Range(summaryStart, 1, agRow - 1, 5);
                var summaryTable = summaryRange.CreateTable(context.GetTableName("CropScapeSummary"));
                summaryTable.Theme = XLTableTheme.TableStyleLight11;
            }

            agSheet.Columns().AdjustToContents();

            // Annualizer Sheets
            var annSummary = context.CreateWorksheet("Annualizer");
            var annData = new Dictionary<string, double>
            {
                {"First Cost", annualizer.FirstCost},
                {"Rate", annualizer.Rate},
                {"Annual O&M", annualizer.AnnualOm},
                {"Annual Benefits", annualizer.AnnualBenefits},
                {"IDC", annualizer.Idc},
                {"Total Investment", annualizer.TotalInvestment},
                {"CRF", annualizer.Crf},
                {"Annual Cost", annualizer.AnnualCost},
                {"BCR", annualizer.Bcr}
            };
            rowIdx = 1;
            foreach (var kv in annData)
            {
                annSummary.Cell(rowIdx, 1).Value = kv.Key;
                var cell = annSummary.Cell(rowIdx, 2);
                cell.Value = kv.Value;
                if (kv.Key.Contains("Cost") || kv.Key.Contains("Benefits") || kv.Key.Contains("Investment") || kv.Key == "IDC")
                    cell.Style.NumberFormat.Format = "$#,##0.00";
                if (kv.Key == "IDC")
                    cell.GetComment().AddText("Calculated from first cost, discount rate and IDC schedule");
                else if (kv.Key == "Total Investment")
                    cell.GetComment().AddText("First Cost + IDC + PV of Future Costs");
                else if (kv.Key == "CRF")
                    cell.GetComment().AddText("r(1+r)^n / ((1+r)^n - 1)");
                else if (kv.Key == "Annual Cost")
                    cell.GetComment().AddText("Total Investment * CRF + Annual O&M");
                else if (kv.Key == "BCR")
                    cell.GetComment().AddText("Annual Benefits / Annual Cost");
                rowIdx++;
            }
            if (rowIdx > 1)
            {
                var annRange = annSummary.Range(1, 1, rowIdx - 1, 2);
                var annTable = annRange.CreateTable(context.GetTableName("AnnualizerSummary"));
                annTable.Theme = XLTableTheme.TableStyleLight11;
                annSummary.Columns(1, 2).AdjustToContents();
            }
            var annFc = context.CreateWorksheet("FutureCosts");
            annFc.Cell(1,1).Value = "Cost";
            annFc.Cell(1,2).Value = "Year";
            rowIdx = 2;
            foreach (var f in annualizer.FutureCosts)
            {
                annFc.Cell(rowIdx,1).Value = f.Cost;
                annFc.Cell(rowIdx,2).Value = f.Year;
                rowIdx++;
            }
            if (rowIdx > 2)
            {
                var fcRange = annFc.Range(1, 1, rowIdx - 1, 2);
                var fcTable = fcRange.CreateTable(context.GetTableName("AnnualizerFuture"));
                fcTable.Theme = XLTableTheme.TableStyleLight11;
                annFc.Columns(1, 2).AdjustToContents();
            }

            // Water Demand Sheet
            foreach (var scenario in waterDemand.Scenarios)
            {
                var wdSheet = context.CreateWorksheet($"WaterDemand_{scenario.Name}");
                wdSheet.Cell(1,1).Value = "Year";
                wdSheet.Cell(1,2).Value = "Growth Rate";
                wdSheet.Cell(1,3).Value = "Demand (MGD)";
                wdSheet.Cell(1,4).Value = "Residential (MGD)";
                wdSheet.Cell(1,5).Value = "Commercial (MGD)";
                wdSheet.Cell(1,6).Value = "Industrial (MGD)";
                wdSheet.Cell(1,7).Value = "Agricultural (MGD)";
                wdSheet.Cell(1,8).Value = "Adjusted (MGD)";
                wdSheet.Cell(1,9).Value = "Adjusted (ac-ft/yr)";
                wdSheet.Cell(1,3).GetComment().AddText("Demand = Prior Demand × (1 + Growth Rate)");
                wdSheet.Cell(1,6).GetComment().AddText("Industrial = Demand × Industrial %");
                wdSheet.Cell(1,8).GetComment().AddText("Adjusted = Demand ÷ (1 - Losses %) × (1 - Improvements %)");
                wdSheet.Cell(1,9).GetComment().AddText("Adjusted Acre-Feet = Adjusted Demand × 365 ÷ 325,851");
                rowIdx = 2;
                foreach (var d in scenario.Results)
                {
                    wdSheet.Cell(rowIdx,1).Value = d.Year;
                    wdSheet.Cell(rowIdx,2).Value = d.GrowthRate;
                    wdSheet.Cell(rowIdx,2).Style.NumberFormat.Format = "0.0%";
                    wdSheet.Cell(rowIdx,3).Value = d.Demand;
                    wdSheet.Cell(rowIdx,3).Style.NumberFormat.Format = "#,##0.00";
                    wdSheet.Cell(rowIdx,4).Value = d.ResidentialDemand;
                    wdSheet.Cell(rowIdx,4).Style.NumberFormat.Format = "#,##0.00";
                    wdSheet.Cell(rowIdx,5).Value = d.CommercialDemand;
                    wdSheet.Cell(rowIdx,5).Style.NumberFormat.Format = "#,##0.00";
                    wdSheet.Cell(rowIdx,6).Value = d.IndustrialDemand;
                    wdSheet.Cell(rowIdx,6).Style.NumberFormat.Format = "#,##0.00";
                    wdSheet.Cell(rowIdx,7).Value = d.AgriculturalDemand;
                    wdSheet.Cell(rowIdx,7).Style.NumberFormat.Format = "#,##0.00";
                    wdSheet.Cell(rowIdx,8).Value = d.AdjustedDemand;
                    wdSheet.Cell(rowIdx,8).Style.NumberFormat.Format = "#,##0.00";
                    wdSheet.Cell(rowIdx,9).Value = d.AdjustedDemandAcreFeet;
                    wdSheet.Cell(rowIdx,9).Style.NumberFormat.Format = "#,##0.00";
                    rowIdx++;
                }
                if (rowIdx > 2)
                {
                    var wdRange = wdSheet.Range(1, 1, rowIdx - 1, 9);
                    var wdTable = wdRange.CreateTable(context.GetTableName($"WaterDemand_{scenario.Name}"));
                    wdTable.Theme = XLTableTheme.TableStyleMedium4;
                }
                wdSheet.Columns(1, 9).AdjustToContents();
            }

            // Updated Cost Sheets
            var ucItems = context.CreateWorksheet("UpdatedCost");
            var ucHeaders = new[]
            {
                "Category",
                "Original Joint Use Costs at Midpoint of Construction",
                "Original Joint Use Costs ENR Index Value",
                "ENR Ratio",
                "Transition ENR Index Value",
                "1967 ENR Index Value",
                "Adjusted Joint Use Costs at Midpoint of Construction",
                "ENR Update Ratio",
                "CWCCIS Base Index",
                "Transition ENR Index Value",
                "Updated Joint Use Costs as of 1967 base",
                "Current CWCCIS Index",
                "CWCCIS Update Value",
                "Updated Storage Cost"
            };
            WriteSanitizedHeaders(ucItems, 1, 1, ucHeaders, "Column");
            rowIdx = 2;
            foreach (var item in updated.UpdatedCostItems)
            {
                ucItems.Cell(rowIdx,1).Value = item.Category;
                ucItems.Cell(rowIdx,2).Value = item.JointUsePre1967;
                ucItems.Cell(rowIdx,3).Value = item.Pre1967EnrIndex;
                ucItems.Cell(rowIdx,4).Value = item.EnrRatioPreToTransition;
                ucItems.Cell(rowIdx,5).Value = item.TransitionEnrIndex;
                ucItems.Cell(rowIdx,6).Value = item.Enr1967Index;
                ucItems.Cell(rowIdx,7).Value = item.JointUseTransition;
                ucItems.Cell(rowIdx,8).Value = item.EnrRatioTransitionTo1967;
                ucItems.Cell(rowIdx,9).Value = item.CwccisBase;
                ucItems.Cell(rowIdx,10).Value = item.TransitionEnrIndex;
                ucItems.Cell(rowIdx,11).Value = item.JointUse1967;
                ucItems.Cell(rowIdx,12).Value = item.CwccisIndex;
                ucItems.Cell(rowIdx,13).Value = item.CwccisUpdateFactor;
                ucItems.Cell(rowIdx,14).Value = item.UpdatedJointCost;
                rowIdx++;
            }
            if (rowIdx > 2)
            {
                var ucRange = ucItems.Range(1, 1, rowIdx - 1, 14);
                var ucTable = ucRange.CreateTable(context.GetTableName("UpdatedCostItems"));
                ucTable.Theme = XLTableTheme.TableStyleMedium9;
                ucItems.Columns(1, 14).AdjustToContents();
            }
            var ucRrr = context.CreateWorksheet("RRR");
            ucRrr.Cell(1,1).Value = "Item";
            ucRrr.Cell(1,2).Value = "Future Cost";
            ucRrr.Cell(1,3).Value = "Year";
            ucRrr.Cell(1,4).Value = "PV Factor";
            ucRrr.Cell(1,5).Value = "Present Value";
            rowIdx = 2;
            foreach (var item in updated.RrrCostItems)
            {
                ucRrr.Cell(rowIdx,1).Value = item.Item;
                ucRrr.Cell(rowIdx,2).Value = item.FutureCost;
                ucRrr.Cell(rowIdx,3).Value = item.Year;
                ucRrr.Cell(rowIdx,4).Value = item.PvFactor;
                ucRrr.Cell(rowIdx,5).Value = item.PresentValue;
                rowIdx++;
            }
            if (rowIdx > 2)
            {
                var rrrRange = ucRrr.Range(1, 1, rowIdx - 1, 5);
                var rrrTable = rrrRange.CreateTable(context.GetTableName("UpdatedCostRrr"));
                rrrTable.Theme = XLTableTheme.TableStyleMedium6;
                ucRrr.Columns(1, 5).AdjustToContents();
            }
            var ucSummary = context.CreateWorksheet("UpdatedCostSummary");
            var ucData = new Dictionary<string, double>
            {
                {"Percent", updated.Percent},
                {"Total Joint O&M", updated.TotalJointOm},
                {"Total Updated Cost", updated.TotalUpdatedCost},
                {"RRR Updated Cost", updated.RrrUpdatedCost},
                {"RRR Annualized", updated.RrrAnnualized},
                {"OM Scaled", updated.OmScaled},
                {"RRR Scaled", updated.RrrScaled},
                {"Cost Recommendation", updated.CostRecommendation},
                {"Capital1", updated.Capital1},
                {"Total1", updated.Total1},
                {"Capital2", updated.Capital2},
                {"Total2", updated.Total2}
            };
            rowIdx = 1;
            foreach (var kv in ucData)
            {
                ucSummary.Cell(rowIdx,1).Value = kv.Key;
                var cell = ucSummary.Cell(rowIdx,2);
                cell.Value = kv.Value;
                if (kv.Key.Contains("Cost") || kv.Key.Contains("Annualized") || kv.Key.Contains("Scaled") || kv.Key.Contains("Capital") || kv.Key.Contains("OM"))
                    cell.Style.NumberFormat.Format = "$#,##0.00";
                if (kv.Key == "Percent")
                    cell.GetComment().AddText("Percent = Storage Recommendation / Total Usable Storage");
                else if (kv.Key == "Total Joint O&M")
                    cell.GetComment().AddText("Total Joint O&M = Joint Operations Cost + Joint Maintenance Cost");
                else if (kv.Key == "Total Updated Cost")
                    cell.GetComment().AddText("Total Updated Cost = Σ(Updated Joint-Use 1967 × CWCCIS Update Value)");
                else if (kv.Key == "RRR Updated Cost")
                    cell.GetComment().AddText("RRR Updated Cost = Present Value × CWCCI");
                else if (kv.Key == "RRR Annualized")
                    cell.GetComment().AddText("RRR Annualized = RRR Updated Cost × CRF");
                else if (kv.Key == "OM Scaled")
                    cell.GetComment().AddText("OM Scaled = Total Joint O&M × Percent");
                else if (kv.Key == "RRR Scaled")
                    cell.GetComment().AddText("RRR Scaled = RRR Annualized × Percent");
                else if (kv.Key == "Cost Recommendation")
                    cell.GetComment().AddText("Cost Recommendation = Total Updated Cost × Percent");
                else if (kv.Key == "Capital1")
                    cell.GetComment().AddText("Capital1 = Total Updated Cost × Percent × CRF1");
                else if (kv.Key == "Total1")
                    cell.GetComment().AddText("Total1 = Capital1 + OM Scaled + RRR Scaled");
                else if (kv.Key == "Capital2")
                    cell.GetComment().AddText("Capital2 = Total Updated Cost × Percent × CRF2");
                else if (kv.Key == "Total2")
                    cell.GetComment().AddText("Total2 = Capital2 + OM Scaled");
                rowIdx++;
            }
            if (rowIdx > 1)
            {
                var summaryRange = ucSummary.Range(1, 1, rowIdx - 1, 2);
                var summaryTable = summaryRange.CreateTable(context.GetTableName("UpdatedCostSummary"));
                summaryTable.Theme = XLTableTheme.TableStyleLight11;
                ucSummary.Columns(1, 2).AdjustToContents();
            }

            // Unit Day Value Sheet
            var udvSheet = context.CreateWorksheet("Udv");
            var udvData = new Dictionary<string, object>
            {
                {"Recreation Type", udv.RecreationType},
                {"Activity Type", udv.ActivityType},
                {"Point Value", udv.Points},
                {"Unit Day Value", udv.UnitDayValue},
                {"Season Length (days)", udv.SeasonDays},
                {"Visitation Input", udv.VisitationInput},
                {"Visitation Cadence", udv.VisitationPeriod},
                {"Total User Days", udv.TotalUserDays},
                {"Annual Recreation Benefit", udv.AnnualRecreationBenefit}
            };
            rowIdx = 1;
            foreach (var kv in udvData)
            {
                udvSheet.Cell(rowIdx,1).SetValue(kv.Key);
                // ClosedXML's SetValue does not accept a plain object, so convert
                // each value explicitly to an XLCellValue before assignment.
                udvSheet.Cell(rowIdx,2).Value = XLCellValue.FromObject(kv.Value);
                rowIdx++;
            }
            if (rowIdx > 1)
            {
                var udvRange = udvSheet.Range(1, 1, rowIdx - 1, 2);
                var udvTable = udvRange.CreateTable(context.GetTableName("UnitDayValue"));
                udvTable.Theme = XLTableTheme.TableStyleLight9;
                udvSheet.Columns(1, 2).AdjustToContents();
            }

            // Recreation Capacity Sheet
            var recreationSheet = context.CreateWorksheet("RecreationCapacity");

            var capacityHeaders = new[]
            {
                "Activity",
                "Resource",
                "Quantity",
                "Units",
                "People per Unit",
                "Daily Turnover",
                "Season Days",
                "People at One Time",
                "Daily Capacity",
                "Seasonal Capacity",
                "Guidance"
            };

            for (int i = 0; i < capacityHeaders.Length; i++)
            {
                var headerCell = recreationSheet.Cell(1, i + 1);
                headerCell.Value = capacityHeaders[i];
                headerCell.Style.Font.SetBold();
                headerCell.Style.Fill.BackgroundColor = DashboardSubHeaderFill;
                headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerCell.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                headerCell.Style.Border.OutsideBorderColor = DashboardBorder;
            }

            int capacityRow = 2;
            foreach (var activity in recreationCapacity.ActivitySummaries)
            {
                recreationSheet.Cell(capacityRow, 1).Value = activity.Activity;
                recreationSheet.Cell(capacityRow, 2).Value = activity.ResourceDescription;
                recreationSheet.Cell(capacityRow, 3).Value = activity.ResourceQuantity;
                recreationSheet.Cell(capacityRow, 3).Style.NumberFormat.Format = "#,##0.##";
                recreationSheet.Cell(capacityRow, 4).Value = activity.ResourceUnits;
                recreationSheet.Cell(capacityRow, 5).Value = activity.PeoplePerUnit;
                recreationSheet.Cell(capacityRow, 5).Style.NumberFormat.Format = "0.000";
                recreationSheet.Cell(capacityRow, 6).Value = activity.DailyTurnover;
                recreationSheet.Cell(capacityRow, 6).Style.NumberFormat.Format = "0.00";
                recreationSheet.Cell(capacityRow, 7).Value = activity.SeasonDays;
                recreationSheet.Cell(capacityRow, 7).Style.NumberFormat.Format = "0.0";
                recreationSheet.Cell(capacityRow, 8).Value = activity.PeopleAtOneTime;
                recreationSheet.Cell(capacityRow, 8).Style.NumberFormat.Format = "#,##0.##";
                recreationSheet.Cell(capacityRow, 9).Value = activity.DailyCapacity;
                recreationSheet.Cell(capacityRow, 9).Style.NumberFormat.Format = "#,##0.##";
                recreationSheet.Cell(capacityRow, 10).Value = activity.SeasonalCapacity;
                recreationSheet.Cell(capacityRow, 10).Style.NumberFormat.Format = "#,##0.##";
                recreationSheet.Cell(capacityRow, 11).Value = activity.GuidanceNote;
                capacityRow++;
            }

            if (capacityRow > 2)
            {
                var capacityRange = recreationSheet.Range(1, 1, capacityRow - 1, capacityHeaders.Length);
                var capacityTable = capacityRange.CreateTable(context.GetTableName("RecreationCapacity"));
                capacityTable.Theme = XLTableTheme.TableStyleMedium4;
            }

            var capacitySummaryRange = recreationSheet.Range(capacityRow + 1, 1, capacityRow + 1, capacityHeaders.Length);
            capacitySummaryRange.Merge();
            capacitySummaryRange.Value = recreationCapacity.Summary;
            capacitySummaryRange.Style.Alignment.WrapText = true;
            capacitySummaryRange.Style.Fill.BackgroundColor = DashboardRowLight;
            capacitySummaryRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            capacitySummaryRange.Style.Border.OutsideBorderColor = DashboardBorder;

            var capacityGuidanceRange = recreationSheet.Range(capacityRow + 2, 1, capacityRow + 2, capacityHeaders.Length);
            capacityGuidanceRange.Merge();
            capacityGuidanceRange.Value = recreationCapacity.GuidanceNotes;
            capacityGuidanceRange.Style.Alignment.WrapText = true;
            capacityGuidanceRange.Style.Fill.BackgroundColor = DashboardRowAlt;
            capacityGuidanceRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            capacityGuidanceRange.Style.Border.OutsideBorderColor = DashboardBorder;

            recreationSheet.Columns(1, capacityHeaders.Length).AdjustToContents();

            // Gantt Sheet
            var ganttSheet = context.CreateWorksheet("Gantt");
            ganttSheet.Cell(1, 1).Value = "Task";
            ganttSheet.Cell(1, 2).Value = "Workstream";
            ganttSheet.Cell(1, 3).Value = "Start";
            ganttSheet.Cell(1, 4).Value = "Finish";
            ganttSheet.Cell(1, 5).Value = "Duration (days)";
            ganttSheet.Cell(1, 6).Value = "Labor Cost $/day";
            ganttSheet.Cell(1, 7).Value = "Task Cost $";
            ganttSheet.Cell(1, 8).Value = "Dependencies";
            ganttSheet.Cell(1, 9).Value = "Milestone";
            ganttSheet.Cell(1, 10).Value = "% Complete";
            rowIdx = 2;
            foreach (var task in gantt.Tasks)
            {
                ganttSheet.Cell(rowIdx, 1).Value = task.Name;
                ganttSheet.Cell(rowIdx, 2).Value = task.Workstream;
                ganttSheet.Cell(rowIdx, 3).Value = task.StartDate;
                ganttSheet.Cell(rowIdx, 4).Value = task.EndDate;
                ganttSheet.Cell(rowIdx, 5).Value = task.DurationDays;
                ganttSheet.Cell(rowIdx, 6).Value = task.LaborCostPerDay;
                ganttSheet.Cell(rowIdx, 6).Style.NumberFormat.Format = "$#,##0.00";
                ganttSheet.Cell(rowIdx, 7).Value = task.TotalCost;
                ganttSheet.Cell(rowIdx, 7).Style.NumberFormat.Format = "$#,##0.00";
                ganttSheet.Cell(rowIdx, 8).Value = task.Dependencies;
                ganttSheet.Cell(rowIdx, 9).Value = task.IsMilestone ? "Yes" : "No";
                ganttSheet.Cell(rowIdx, 10).Value = task.PercentComplete / 100.0;
                ganttSheet.Cell(rowIdx, 10).Style.NumberFormat.Format = "0.00%";
                rowIdx++;
            }
            if (rowIdx > 2)
            {
                var ganttRange = ganttSheet.Range(1, 1, rowIdx - 1, 10);
                var ganttTable = ganttRange.CreateTable(context.GetTableName("GanttTasks"));
                ganttTable.Theme = XLTableTheme.TableStyleMedium2;
            }

            BuildDashboard(context, ead, agriculture, annualizer, updated, waterDemand, udv, recreationCapacity, gantt);

            context.Save(filePath);
            });
            });
        }

        private static void BuildDashboard(ExportContext context, EadViewModel ead, AgricultureDepthDamageViewModel agriculture, AnnualizerViewModel annualizer, UpdatedCostViewModel updated, WaterDemandViewModel waterDemand, UdvViewModel udv, RecreationCapacityViewModel recreationCapacity, GanttViewModel gantt)
        {
            var ws = context.CreateWorksheet("Dashboard");
            ws.Position = 1;
            ws.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

            ws.Column(1).Width = 32;
            ws.Column(2).Width = 20;
            ws.Column(3).Width = 16;
            ws.Column(4).Width = 4;
            ws.Column(5).Width = 20;
            ws.Column(6).Width = 20;
            ws.Column(7).Width = 20;
            ws.Column(8).Width = 20;

            var titleRange = ws.Range(1, 1, 1, 8);
            titleRange.Merge();
            titleRange.Value = "Economic Toolbox Dashboard";
            titleRange.Style.Font.SetBold();
            titleRange.Style.Font.FontSize = 20;
            titleRange.Style.Font.FontColor = DashboardHeaderText;
            titleRange.Style.Fill.BackgroundColor = DashboardHeaderFill;
            titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var subtitleRange = ws.Range(2, 1, 2, 8);
            subtitleRange.Merge();
            subtitleRange.Value = "Consolidated view of key planning results";
            subtitleRange.Style.Font.SetFontColor(DashboardAccentText);
            subtitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var timestampRange = ws.Range(3, 1, 3, 8);
            timestampRange.Merge();
            timestampRange.Value = $"Generated {DateTime.Now:MMMM d, yyyy h:mm tt}";
            timestampRange.Style.Font.SetFontColor(DashboardPrimaryText);
            timestampRange.Style.Font.FontSize = 11;
            timestampRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            timestampRange.Style.Fill.BackgroundColor = DashboardRowLight;

            ws.SheetView.FreezeRows(3);

            int currentRow = 5;

            var financialRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("First Cost", annualizer.FirstCost, "$#,##0.00", "Initial capital expenditure before financing.", false),
                ("Discount Rate", annualizer.Rate / 100.0, "0.00%", "Input discount rate used in the CRF.", false),
                ("Annual O&M", annualizer.AnnualOm, "$#,##0.00", "Recurring operations and maintenance cost.", false),
                ("Annual Benefits", annualizer.AnnualBenefits, "$#,##0.00", "Estimated average annual benefits.", true),
                ("IDC", annualizer.Idc, "$#,##0.00", "Calculated from first cost, discount rate and IDC schedule.", false),
                ("Total Investment", annualizer.TotalInvestment, "$#,##0.00", "First Cost + IDC + PV of Future Costs.", false),
                ("CRF", annualizer.Crf, "0.0000", "r(1+r)^n / ((1+r)^n - 1)", false),
                ("Annual Cost", annualizer.AnnualCost, "$#,##0.00", "Total Investment × CRF + Annual O&M.", false),
                ("Benefit-Cost Ratio", annualizer.Bcr, "0.00", "Annual Benefits / Annual Cost.", true),
                ("Storage Utilization", updated.Percent, "0.00%", "Storage Recommendation / Total Usable Storage.", false),
                ("Total Joint O&M", updated.TotalJointOm, "$#,##0.00", "Joint Operations Cost + Joint Maintenance Cost.", false),
                ("Total Updated Cost", updated.TotalUpdatedCost, "$#,##0.00", "Σ(Updated Joint-Use 1967 × CWCCIS Update Value).", false),
                ("RRR Updated Cost", updated.RrrUpdatedCost, "$#,##0.00", "Present Value × CWCCI.", false),
                ("RRR Annualized", updated.RrrAnnualized, "$#,##0.00", "RRR Updated Cost × CRF.", false),
                ("O&M Scaled", updated.OmScaled, "$#,##0.00", "Total Joint O&M × Storage Utilization.", false),
                ("RRR Scaled", updated.RrrScaled, "$#,##0.00", "RRR Annualized × Storage Utilization.", false),
                ("Cost Recommendation", updated.CostRecommendation, "$#,##0.00", "Total Updated Cost × Storage Utilization.", true),
                ("Capital (Scenario 1)", updated.Capital1, "$#,##0.00", "Total Updated Cost × Storage Utilization × CRF1.", false),
                ("Total Annual Cost (Scenario 1)", updated.Total1, "$#,##0.00", "Capital1 + O&M Scaled + RRR Scaled.", false),
                ("Capital (Scenario 2)", updated.Capital2, "$#,##0.00", "Total Updated Cost × Storage Utilization × CRF2.", false),
                ("Total Annual Cost (Scenario 2)", updated.Total2, "$#,##0.00", "Capital2 + O&M Scaled.", false)
            };

            int capitalStart = currentRow;
            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Capital Investment Metrics", financialRows, context);
            AddAnnualizerComparisonChart(ws, annualizer.AnnualBenefits, annualizer.AnnualCost, annualizer.Bcr, capitalStart, 5, context);

            double? primaryEadValue = null;
            string primaryDamageColumn = ead.DamageColumns.Count > 0 ? ead.DamageColumns[0].Name : "Damage";
            var eadRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("Rows Evaluated", ead.Rows.Count, "0", "Number of probability-damage pairs included.", false),
                ("Includes Stage Data", ead.UseStage ? "Yes" : "No", null, ead.UseStage ? "Stage values informed the visual trend." : "Stage values were not provided.", false)
            };

            if (ead.Rows.Count > 0 && ead.DamageColumns.Count > 0)
            {
                var probabilities = ead.Rows.Select(r => r.Probability).ToArray();
                for (int i = 0; i < ead.DamageColumns.Count; i++)
                {
                    var damages = ead.Rows.Select(r => r.Damages.Count > i ? r.Damages[i] : 0.0).ToArray();
                    double eadValue = EadModel.Compute(probabilities, damages);
                    if (i == 0)
                        primaryEadValue = eadValue;
                    eadRows.Add(($"{ead.DamageColumns[i].Name} EAD", eadValue, "$#,##0.00", "Expected annual damage for this damage column.", i == 0));
                }
                eadRows.Add(("Summary", JoinOrEmpty(" | ", ead.Results.Select(r => $"{r.Label}: {r.Result}")), null, "Combined textual output from the calculator.", false));
            }
            else
            {
                eadRows.Add(("EAD Status", "Enter frequency and damage data to compute.", null, null, false));
            }

            int eadStart = currentRow;
            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Expected Annual Damage", eadRows, context);
            AddEadDashboardChart(ws, ead, primaryDamageColumn, primaryEadValue, eadStart, 5, context);

            var agricultureRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("Region", agriculture.SelectedRegion?.Name ?? "Not selected", null, agriculture.SelectedRegion?.Description, false),
                ("Crop", agriculture.SelectedCrop?.Name ?? "Not selected", null, agriculture.SelectedCrop?.Description, false),
                ("Simulation Seasons", agriculture.SimulationYears, "0", "Number of Monte Carlo seasons evaluated.", false),
                ("Modeled Impact Probability", agriculture.ModeledImpactProbability, "0.00%", "Scaled baseline AEP considering seasonal stress.", agriculture.ModeledImpactProbability > 0),
                ("Average Damage", agriculture.MeanDamagePercent / 100.0, "0.00%", "Mean depth-duration damage across simulated events.", agriculture.MeanDamagePercent > 0),
                ("CropScape Acreage", agriculture.CropScapeTotalAcreage, "#,##0.0", agriculture.CropScapeTotalAcreage > 0 ? "Total acreage imported from CropScape rasters." : null, agriculture.CropScapeTotalAcreage > 0)
            };

            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Agriculture Depth-Damage", agricultureRows, context);

            if (!string.IsNullOrWhiteSpace(agriculture.ImpactSummary))
            {
                ws.Cell(currentRow, 1).Value = "Impact Summary";
                ws.Cell(currentRow, 1).Style.Font.SetBold();
                ws.Cell(currentRow, 1).Style.Font.FontColor = DashboardAccentText;
                ws.Range(currentRow, 1, currentRow, 2).Merge();
                currentRow++;
                var impactRange = ws.Range(currentRow, 1, currentRow, 2);
                impactRange.Merge();
                impactRange.Value = agriculture.ImpactSummary;
                impactRange.Style.Alignment.WrapText = true;
                impactRange.Style.Fill.BackgroundColor = DashboardRowLight;
                currentRow += 2;
            }

            if (!string.IsNullOrWhiteSpace(agriculture.CropInsight))
            {
                ws.Cell(currentRow, 1).Value = "Crop Insight";
                ws.Cell(currentRow, 1).Style.Font.SetBold();
                ws.Cell(currentRow, 1).Style.Font.FontColor = DashboardAccentText;
                ws.Range(currentRow, 1, currentRow, 2).Merge();
                currentRow++;
                var cropRange = ws.Range(currentRow, 1, currentRow, 2);
                cropRange.Merge();
                cropRange.Value = agriculture.CropInsight;
                cropRange.Style.Alignment.WrapText = true;
                cropRange.Style.Fill.BackgroundColor = DashboardRowAlt;
                currentRow += 2;
            }

            var waterEntries = new List<(string Scenario, int Year, double Adjusted, string? Description, double? ChangePercent, bool Highlight, bool HasResults)>();
            foreach (var scenario in waterDemand.Scenarios)
            {
                if (scenario.Results.Count == 0)
                {
                    waterEntries.Add((scenario.Name, scenario.BaseYear, 0.0, scenario.Description, null, scenario == waterDemand.SelectedScenario, false));
                    continue;
                }

                var first = scenario.Results.First();
                var last = scenario.Results.Last();
                double? change = first.AdjustedDemand == 0 ? null : (last.AdjustedDemand - first.AdjustedDemand) / first.AdjustedDemand;
                waterEntries.Add((scenario.Name, last.Year, last.AdjustedDemand, scenario.Description, change, scenario == waterDemand.SelectedScenario, true));
            }

            int waterStart = currentRow;
            currentRow = WriteWaterDemandTable(ws, currentRow, 1, "Water Demand Outlook", waterEntries, context);
            AddWaterDemandChart(ws, waterDemand.Scenarios, waterStart, 5, context);

            double recreationBenefit = UdvModel.ComputeBenefit(udv.UnitDayValue, udv.TotalUserDays);
            var recreationRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("Camping PAOT", recreationCapacity.CampingPeopleAtOneTime, "#,##0.0", "People-at-one-time supported by the campsite inventory.", false),
                ("Camping Design Day", recreationCapacity.CampingDailyCapacity, "#,##0.0", "Design day user capacity for camping.", false),
                ("Fishing PAOT", recreationCapacity.FishingPeopleAtOneTime, "#,##0.0", "Accessible shoreline positions multiplied by typical party size.", false),
                ("Boating PAOT", recreationCapacity.BoatingPeopleAtOneTime, "#,##0.0", "Water surface capacity using USACE acres-per-vessel guidance.", false),
                ("Total PAOT", recreationCapacity.TotalPeopleAtOneTime, "#,##0.0", "Aggregate people-at-one-time across recreation activities.", true),
                ("Design Day Total", recreationCapacity.TotalDailyCapacity, "#,##0.0", "Combined design day capacity across camping, fishing, and boating.", true),
                ("Seasonal User Days", recreationCapacity.TotalSeasonCapacity, "#,##0", "Seasonal user capacity based on the supplied season lengths and turnover rates.", true)
            };
            var udvRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("Recreation Type", udv.RecreationType, null, null, false),
                ("Activity Type", udv.ActivityType, null, null, false),
                ("Point Value", udv.Points, "0.0", "Value applied against the recreation look-up table.", false),
                ("Unit Day Value", udv.UnitDayValue, "$#,##0.00", "Interpolated value from the recreation tables.", true),
                ("Season Length (days)", udv.SeasonDays, "0.0", "Number of operating days considered in the season.", false),
                ("Visitation Input", udv.VisitationInput, "#,##0.##", $"Value provided on a {udv.VisitationPeriod.ToLowerInvariant()} basis.", false),
                ("Total User Days", udv.TotalUserDays, "#,##0.##", "Season days adjusted for visitation input.", false),
                ("Annual Recreation Benefit", recreationBenefit, "$#,##0.00", "Unit Day Value × Total User Days.", true)
            };

            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Recreation Capacity", recreationRows, context);

            int udvStart = currentRow;
            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Recreation Highlights", udvRows, context);
            AddUdvChart(ws, udv, udvStart, 5, context);

            var ganttTaskCount = gantt.Tasks.Count;
            DateTime? ganttStart = ganttTaskCount > 0 ? gantt.Tasks.Min(t => t.StartDate) : null;
            DateTime? ganttFinish = ganttTaskCount > 0 ? gantt.Tasks.Max(t => t.EndDate) : null;
            int milestoneCount = gantt.Tasks.Count(t => t.IsMilestone);
            double averagePercent = ganttTaskCount > 0 ? gantt.Tasks.Average(t => t.PercentComplete) : 0;

            var ganttRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("Tasks Planned", ganttTaskCount, "0", "Number of activities scheduled in the Gantt module.", false),
                ("Project Start", ganttStart.HasValue ? (object)ganttStart.Value : "Not scheduled", ganttStart.HasValue ? "mmmm d, yyyy" : null, null, false),
                ("Project Finish", ganttFinish.HasValue ? (object)ganttFinish.Value : "Not scheduled", ganttFinish.HasValue ? "mmmm d, yyyy" : null, null, false),
                ("Duration (days)", ganttTaskCount > 0 ? gantt.TotalDurationDays : 0, "0", "Total span between start and finish.", false),
                ("Total Labor Cost", gantt.TotalLaborCost, "$#,##0.00", "Sum of labor spending computed from task rates and durations.", gantt.TotalLaborCost > 0),
                ("Milestones", milestoneCount, "0", "Tasks flagged as milestones.", milestoneCount > 0),
                ("Average % Complete", ganttTaskCount > 0 ? averagePercent / 100.0 : 0, "0.0%", "Mean percent complete across all tasks.", false)
            };

            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Gantt Highlights", ganttRows, context);

            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Sketch Summary", new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>(), context);
        }

        private static int WriteKeyValueTable(IXLWorksheet ws, int startRow, int startColumn, string title, List<(string Label, object Value, string? Format, string? Comment, bool Highlight)> entries, ExportContext context)
        {
            if (entries.Count == 0)
                return startRow;

            var headerRange = ws.Range(startRow, startColumn, startRow, startColumn + 1);
            headerRange.Merge();
            headerRange.Value = title;
            headerRange.Style.Font.SetBold();
            headerRange.Style.Font.FontSize = 13;
            headerRange.Style.Font.FontColor = DashboardHeaderText;
            headerRange.Style.Fill.BackgroundColor = DashboardHeaderFill;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            headerRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            headerRange.Style.Border.OutsideBorderColor = DashboardBorder;

            var columnHeaders = ws.Range(startRow + 1, startColumn, startRow + 1, startColumn + 1);
            columnHeaders.Style.Font.SetBold();
            columnHeaders.Style.Fill.BackgroundColor = DashboardSubHeaderFill;
            columnHeaders.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            columnHeaders.Style.Border.OutsideBorderColor = DashboardBorder;
            ws.Cell(startRow + 1, startColumn).Value = "Metric";
            ws.Cell(startRow + 1, startColumn + 1).Value = "Value";

            int row = startRow + 2;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var dataRange = ws.Range(row, startColumn, row, startColumn + 1);
                dataRange.Style.Fill.BackgroundColor = i % 2 == 0 ? DashboardRowLight : DashboardRowAlt;
                dataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                dataRange.Style.Border.OutsideBorderColor = DashboardBorder;

                var labelCell = ws.Cell(row, startColumn);
                labelCell.Value = entry.Label;
                labelCell.Style.Font.FontColor = DashboardPrimaryText;

                var valueCell = ws.Cell(row, startColumn + 1);
                valueCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                valueCell.Style.Font.FontColor = entry.Highlight ? DashboardAccentText : DashboardPrimaryText;
                valueCell.Style.Font.SetBold(entry.Highlight);

                switch (entry.Value)
                {
                    case double d:
                        valueCell.Value = d;
                        if (!string.IsNullOrWhiteSpace(entry.Format))
                            valueCell.Style.NumberFormat.Format = entry.Format;
                        break;
                    case float f:
                        valueCell.Value = f;
                        if (!string.IsNullOrWhiteSpace(entry.Format))
                            valueCell.Style.NumberFormat.Format = entry.Format;
                        break;
                    case int i32:
                        valueCell.Value = i32;
                        valueCell.Style.NumberFormat.Format = string.IsNullOrWhiteSpace(entry.Format) ? "0" : entry.Format;
                        break;
                    case long i64:
                        valueCell.Value = i64;
                        valueCell.Style.NumberFormat.Format = string.IsNullOrWhiteSpace(entry.Format) ? "0" : entry.Format;
                        break;
                    case decimal dec:
                        valueCell.Value = (double)dec;
                        if (!string.IsNullOrWhiteSpace(entry.Format))
                            valueCell.Style.NumberFormat.Format = entry.Format;
                        break;
                    case null:
                        valueCell.Value = string.Empty;
                        valueCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        break;
                    default:
                        valueCell.Value = entry.Value.ToString();
                        valueCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        break;
                }

                if (!string.IsNullOrWhiteSpace(entry.Comment))
                    valueCell.GetComment().AddText(entry.Comment);

                row++;
            }

            ws.Range(startRow, startColumn, row - 1, startColumn + 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var tableRange = ws.Range(startRow + 1, startColumn, row - 1, startColumn + 1);
            var table = tableRange.CreateTable(context.GetTableName(title));
            table.ShowAutoFilter = false;
            table.Theme = XLTableTheme.TableStyleMedium9;

            return row + 2;
        }

        private static void SetCellValue(IXLCell cell, object? value)
        {
            switch (value)
            {
                case null:
                    cell.Value = string.Empty;
                    break;
                case XLCellValue xlValue:
                    cell.Value = xlValue;
                    break;
                case double d:
                    cell.Value = d;
                    break;
                case float f:
                    cell.Value = f;
                    break;
                case decimal dec:
                    cell.Value = (double)dec;
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case long l:
                    cell.Value = l;
                    break;
                case short s:
                    cell.Value = s;
                    break;
                case sbyte sb:
                    cell.Value = sb;
                    break;
                case byte b:
                    cell.Value = b;
                    break;
                case uint ui:
                    cell.Value = ui;
                    break;
                case ulong ul:
                    cell.Value = ul;
                    break;
                case ushort us:
                    cell.Value = us;
                    break;
                case bool boolValue:
                    cell.Value = boolValue;
                    break;
                case DateTime dateTime:
                    cell.Value = dateTime;
                    break;
                case TimeSpan timeSpan:
                    cell.Value = timeSpan;
                    break;
                default:
                    cell.Value = value?.ToString() ?? string.Empty;
                    break;
            }
        }

        private static int WriteWaterDemandTable(IXLWorksheet ws, int startRow, int startColumn, string title, List<(string Scenario, int Year, double Adjusted, string? Description, double? ChangePercent, bool Highlight, bool HasResults)> entries, ExportContext context)
        {
            var headerRange = ws.Range(startRow, startColumn, startRow, startColumn + 2);
            headerRange.Merge();
            headerRange.Value = title;
            headerRange.Style.Font.SetBold();
            headerRange.Style.Font.FontSize = 13;
            headerRange.Style.Font.FontColor = DashboardHeaderText;
            headerRange.Style.Fill.BackgroundColor = DashboardHeaderFill;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            headerRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            headerRange.Style.Border.OutsideBorderColor = DashboardBorder;

            var columns = ws.Range(startRow + 1, startColumn, startRow + 1, startColumn + 2);
            columns.Style.Font.SetBold();
            columns.Style.Fill.BackgroundColor = DashboardSubHeaderFill;
            columns.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            columns.Style.Border.OutsideBorderColor = DashboardBorder;
            ws.Cell(startRow + 1, startColumn).Value = "Scenario";
            ws.Cell(startRow + 1, startColumn + 1).Value = "Final Year";
            ws.Cell(startRow + 1, startColumn + 2).Value = "Adjusted Demand (MGD)";

            if (entries.Count == 0)
            {
                int emptyRow = startRow + 2;
                var emptyRange = ws.Range(emptyRow, startColumn, emptyRow, startColumn + 2);
                emptyRange.Merge();
                emptyRange.Value = "Add forecast inputs to view scenario results.";
                emptyRange.Style.Fill.BackgroundColor = DashboardRowLight;
                emptyRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                emptyRange.Style.Border.OutsideBorderColor = DashboardBorder;
                emptyRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                return emptyRow + 2;
            }

            int row = startRow + 2;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var range = ws.Range(row, startColumn, row, startColumn + 2);
                range.Style.Fill.BackgroundColor = i % 2 == 0 ? DashboardRowLight : DashboardRowAlt;
                range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                range.Style.Border.OutsideBorderColor = DashboardBorder;

                var nameCell = ws.Cell(row, startColumn);
                nameCell.Value = entry.Scenario;
                nameCell.Style.Font.SetBold(entry.Highlight);
                nameCell.Style.Font.FontColor = entry.Highlight ? DashboardAccentText : DashboardPrimaryText;
                if (!string.IsNullOrWhiteSpace(entry.Description))
                    nameCell.GetComment().AddText(entry.Description);

                var yearCell = ws.Cell(row, startColumn + 1);
                yearCell.Value = entry.Year;
                yearCell.Style.NumberFormat.Format = "0";
                yearCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var demandCell = ws.Cell(row, startColumn + 2);
                demandCell.Value = entry.Adjusted;
                demandCell.Style.NumberFormat.Format = "#,##0.00";
                demandCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                demandCell.Style.Font.SetBold(entry.Highlight);
                demandCell.Style.Font.FontColor = entry.Highlight ? DashboardAccentText : DashboardPrimaryText;
                if (entry.ChangePercent.HasValue)
                    demandCell.GetComment().AddText($"Change from baseline: {entry.ChangePercent.Value:P1}");
                else if (!entry.HasResults)
                    demandCell.GetComment().AddText("No forecast results have been generated yet.");

                row++;
            }

            ws.Range(startRow, startColumn, row - 1, startColumn + 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            var tableRange = ws.Range(startRow + 1, startColumn, row - 1, startColumn + 2);
            var table = tableRange.CreateTable(context.GetTableName(title));
            table.ShowAutoFilter = false;
            table.Theme = XLTableTheme.TableStyleLight11;
            return row + 2;
        }

        private static string CreateWorksheetName(XLWorkbook workbook, string baseName)
        {
            string sanitized = string.IsNullOrWhiteSpace(baseName) ? "Sheet" : baseName;
            foreach (char invalid in new[] { ':', '\\', '/', '?', '*', '[', ']' })
            {
                sanitized = sanitized.Replace(invalid, ' ');
            }

            sanitized = sanitized.Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "Sheet";
            }

            string trimmed = sanitized.Length > ExcelMaxWorksheetNameLength ? sanitized[..ExcelMaxWorksheetNameLength] : sanitized;
            string candidate = trimmed;
            int suffix = 1;
            while (workbook.Worksheets.Any(ws => string.Equals(ws.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                string suffixText = $"_{suffix}";
                int maxLength = ExcelMaxWorksheetNameLength - suffixText.Length;
                maxLength = Math.Max(1, maxLength);
                string prefix = trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
                candidate = prefix + suffixText;
                suffix++;
            }

            return candidate;
        }

        private static void AddAnnualizerComparisonChart(IXLWorksheet ws, double annualBenefits, double annualCost, double bcr, int row, int column, ExportContext context)
        {
            byte[] bytes = CreateAnnualizerBarChartImage(annualBenefits, annualCost, bcr);
            using var stream = new MemoryStream(bytes);
            var picture = ws.AddPicture(stream, XLPictureFormat.Png, context.GetPictureName("AnnualizerChart_"));
            picture.MoveTo(ws.Cell(row, column));
        }

        private static void AddEadDashboardChart(IXLWorksheet ws, EadViewModel ead, string curveName, double? eadValue, int row, int column, ExportContext context)
        {
            var data = ead.Rows
                .Where(r => r.Damages.Count > 0)
                .Select(r => (Probability: Math.Clamp(r.Probability, 0.0, 1.0), Stage: ead.UseStage ? r.Stage : null, Damage: r.Damages[0]))
                .OrderBy(item => item.Probability)
                .ToList();
            if (data.Count == 0)
                return;

            byte[] bytes = CreateEadDashboardChartImage(data, curveName, eadValue);
            using var stream = new MemoryStream(bytes);
            var picture = ws.AddPicture(stream, XLPictureFormat.Png, context.GetPictureName("EadDashboardChart_"));
            picture.MoveTo(ws.Cell(row, column));
        }

        private static void AddWaterDemandChart(IXLWorksheet ws, IEnumerable<Scenario> scenarios, int row, int column, ExportContext context)
        {
            var series = scenarios
                .Select(s => new
                {
                    s.Name,
                    Points = s.Results.Select(r => (Year: (double)r.Year, Demand: r.AdjustedDemand)).ToList(),
                    Color = GetColorFromBrush(s.LineBrush, ChartBlue)
                })
                .Where(s => s.Points.Count >= 2)
                .ToList();
            if (series.Count == 0)
                return;

            var data = series
                .Select(s => (s.Name, Points: (IReadOnlyList<(double Year, double Demand)>)s.Points, s.Color))
                .ToList();
            byte[] bytes = CreateWaterDemandChartImage(data);
            using var stream = new MemoryStream(bytes);
            var picture = ws.AddPicture(stream, XLPictureFormat.Png, context.GetPictureName("WaterDemandChart_"));
            picture.MoveTo(ws.Cell(row, column));
        }

        private static byte[] CreateAnnualizerBarChartImage(double annualBenefits, double annualCost, double bcr)
        {
            const double width = 360;
            const double height = 200;

            if (annualBenefits <= 0 && annualCost <= 0)
                return Array.Empty<byte>();

            double maxValue = Math.Max(annualBenefits, annualCost);
            double barAreaHeight = height - 60;
            double barWidth = 80;
            double spacing = 40;
            double originX = 60;
            double originY = height - 40;

            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var surfaceBrush = ThemeResourceHelper.GetBrush("App.Surface", Brushes.White);
                var axisBrush = ThemeResourceHelper.GetBrush("App.TextSecondary", new SolidColorBrush(Color.FromRgb(80, 80, 80)));
                var textBrush = ThemeResourceHelper.GetBrush("App.TextPrimary", Brushes.Black);
                dc.DrawRectangle(surfaceBrush, null, new Rect(0, 0, width, height));

                // Axis
                Pen axisPen = new(axisBrush, 1);
                dc.DrawLine(axisPen, new Point(originX, 20), new Point(originX, originY));
                dc.DrawLine(axisPen, new Point(originX, originY), new Point(width - 20, originY));

                // Bars
                double benefitsHeight = maxValue > 0 ? (annualBenefits / maxValue) * barAreaHeight : 0;
                double costHeight = maxValue > 0 ? (annualCost / maxValue) * barAreaHeight : 0;

                Rect benefitsRect = new(originX + spacing, originY - benefitsHeight, barWidth, benefitsHeight);
                dc.DrawRectangle(new SolidColorBrush(ChartBlue), null, benefitsRect);

                Rect costRect = new(originX + spacing + barWidth + spacing, originY - costHeight, barWidth, costHeight);
                dc.DrawRectangle(new SolidColorBrush(ChartOrange), null, costRect);

                // Labels
                FormattedText benefitsLabel = new($"Benefits: {annualBenefits:N0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 12, textBrush, 1.0);
                dc.DrawText(benefitsLabel, new Point(benefitsRect.X, originY + 5));

                FormattedText costLabel = new($"Costs: {annualCost:N0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 12, textBrush, 1.0);
                dc.DrawText(costLabel, new Point(costRect.X, originY + 5));

                FormattedText bcrLabel = new($"BCR: {bcr:0.00}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 14, textBrush, 1.0);
                dc.DrawText(bcrLabel, new Point(width - 120, 25));
            }

            return RenderVisualToPng(visual, width, height);
        }

        private static byte[] CreateEadDashboardChartImage(IReadOnlyList<(double Probability, double? Stage, double Damage)> data, string curveName, double? eadValue)
        {
            const double width = 360;
            const double height = 200;
            if (data.Count == 0)
                return Array.Empty<byte>();

            double maxDamage = data.Max(p => p.Damage);
            if (maxDamage <= 0)
                return Array.Empty<byte>();

            double margin = 40;
            double plotWidth = width - (margin * 2);
            double plotHeight = height - (margin * 2);
            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var surfaceBrush = ThemeResourceHelper.GetBrush("App.Surface", Brushes.White);
                var axisBrush = ThemeResourceHelper.GetBrush("App.TextSecondary", new SolidColorBrush(Color.FromRgb(80, 80, 80)));
                var textBrush = ThemeResourceHelper.GetBrush("App.TextPrimary", Brushes.Black);
                dc.DrawRectangle(surfaceBrush, null, new Rect(0, 0, width, height));

                Pen axisPen = new(axisBrush, 1);
                Point origin = new(margin, height - margin);
                dc.DrawLine(axisPen, origin, new Point(width - margin, origin.Y));
                dc.DrawLine(axisPen, origin, new Point(origin.X, margin));

                Point[] points = data
                    .OrderBy(p => p.Probability)
                    .Select(p => new Point(
                        origin.X + Math.Clamp(p.Probability, 0.0, 1.0) * plotWidth,
                        origin.Y - (p.Damage / maxDamage) * plotHeight))
                    .ToArray();
                DrawPolyline(dc, points, new Pen(new SolidColorBrush(ChartBlue), 2));

                if (eadValue.HasValue)
                {
                    double y = origin.Y - (eadValue.Value / maxDamage) * plotHeight;
                    Pen dashed = new(new SolidColorBrush(ChartOrange), 1) { DashStyle = DashStyles.Dash };
                    dc.DrawLine(dashed, new Point(origin.X, y), new Point(width - margin, y));
                    FormattedText eadLabel = new($"EAD: {eadValue.Value:N0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 12, textBrush, 1.0);
                    dc.DrawText(eadLabel, new Point(origin.X + 5, y - 18));
                }

                FormattedText title = new(curveName ?? "Damage Curve", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 13, textBrush, 1.0);
                dc.DrawText(title, new Point(margin, 10));
            }

            return RenderVisualToPng(visual, width, height);
        }

        private static byte[] CreateWaterDemandChartImage(IReadOnlyList<(string Name, IReadOnlyList<(double Year, double Demand)> Points, Color Color)> series)
        {
            const double width = 420;
            const double height = 240;
            if (series.Count == 0)
                return Array.Empty<byte>();

            var allPoints = series.SelectMany(s => s.Points).ToList();
            double minYear = allPoints.Min(p => p.Year);
            double maxYear = allPoints.Max(p => p.Year);
            double maxDemand = allPoints.Max(p => p.Demand);
            if (maxYear <= minYear || maxDemand <= 0)
                return Array.Empty<byte>();

            double margin = 50;
            double plotWidth = width - (margin * 2);
            double plotHeight = height - (margin * 2);

            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var surfaceBrush = ThemeResourceHelper.GetBrush("App.Surface", Brushes.White);
                var axisBrush = ThemeResourceHelper.GetBrush("App.TextSecondary", new SolidColorBrush(Color.FromRgb(80, 80, 80)));
                dc.DrawRectangle(surfaceBrush, null, new Rect(0, 0, width, height));
                Pen axisPen = new(axisBrush, 1);
                Point origin = new(margin, height - margin);
                dc.DrawLine(axisPen, origin, new Point(width - margin, origin.Y));
                dc.DrawLine(axisPen, origin, new Point(origin.X, margin));

                foreach (var seriesItem in series)
                {
                    if (seriesItem.Points.Count < 2)
                        continue;

                    Point[] points = seriesItem.Points
                        .OrderBy(p => p.Year)
                        .Select(p => new Point(
                            origin.X + ((p.Year - minYear) / (maxYear - minYear)) * plotWidth,
                            origin.Y - (p.Demand / maxDemand) * plotHeight))
                        .ToArray();

                    Pen pen = new(new SolidColorBrush(seriesItem.Color), 2);
                    DrawPolyline(dc, points, pen);
                }
            }

            return RenderVisualToPng(visual, width, height);
        }
        private static void AddUdvChart(IXLWorksheet ws, UdvViewModel udv, int row, int column, ExportContext context)
        {
            byte[] bytes = CreateUdvChartImage(udv);
            if (bytes.Length == 0)
                return;
            using var stream = new MemoryStream(bytes);
            var picture = ws.AddPicture(stream, XLPictureFormat.Png, context.GetPictureName("UdvChart_"));
            picture.MoveTo(ws.Cell(row, column));
        }

        private static byte[] RenderVisualToPng(DrawingVisual visual, double width, double height)
        {
            RenderTargetBitmap rtb = new((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using MemoryStream ms = new();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static byte[] CreateUdvChartImage(UdvViewModel udv)
        {
            string columnKey = GetUdvColumnKey(udv);
            var data = udv.Table
                .Select(row => ((double)row.Points, GetUdvColumnValue(row, columnKey)))
                .OrderBy(p => p.Item1)
                .ToList();
            if (data.Count < 2)
                return Array.Empty<byte>();

            const double width = 360;
            const double height = 220;
            const double marginLeft = 60;
            const double marginRight = 26;
            const double marginTop = 45;
            const double marginBottom = 65;

            double minX = data.Min(p => p.Item1);
            double maxX = data.Max(p => p.Item1);
            double minY = 0;
            double maxY = Math.Max(data.Max(p => p.Item2), Math.Max(udv.UnitDayValue, 1.0));

            double plotWidth = width - marginLeft - marginRight;
            double plotHeight = height - marginTop - marginBottom;

            DrawingVisual dv = new();
            using var dc = dv.RenderOpen();

            var highlightColor = ThemeResourceHelper.GetColor("App.HighlightBackground.Color", Color.FromRgb(248, 251, 255));
            var surfaceColor = ThemeResourceHelper.GetColor("App.Surface.Color", Color.FromRgb(230, 239, 252));
            var background = new LinearGradientBrush(highlightColor, surfaceColor, new Point(0, 0), new Point(0, 1));
            background.Freeze();
            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));

            var origin = new Point(marginLeft, marginTop + plotHeight);
            var axisPen = new Pen(new SolidColorBrush(ChartGray), 1.0);
            axisPen.Freeze();
            dc.DrawLine(axisPen, origin, new Point(origin.X + plotWidth, origin.Y));
            dc.DrawLine(axisPen, origin, new Point(origin.X, marginTop));

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(85, ChartGray.R, ChartGray.G, ChartGray.B)), 0.6)
            {
                DashStyle = DashStyles.Dot
            };
            gridPen.Freeze();
            const int verticalTickCount = 4;
            for (int i = 1; i <= verticalTickCount; i++)
            {
                double x = origin.X + (plotWidth / verticalTickCount) * i;
                dc.DrawLine(gridPen, new Point(x, origin.Y), new Point(x, marginTop));
            }
            const int horizontalTickCount = 4;
            for (int i = 1; i <= horizontalTickCount; i++)
            {
                double y = origin.Y - (plotHeight / horizontalTickCount) * i;
                dc.DrawLine(gridPen, new Point(origin.X, y), new Point(origin.X + plotWidth, y));
            }

            var areaGeometry = new StreamGeometry();
            using (var ctx = areaGeometry.Open())
            {
                double firstX = origin.X + (data[0].Item1 - minX) / (maxX - minX) * plotWidth;
                double firstY = origin.Y - (data[0].Item2 - minY) / (maxY - minY) * plotHeight;
                ctx.BeginFigure(new Point(firstX, origin.Y), true, true);
                ctx.LineTo(new Point(firstX, firstY), true, true);
                for (int i = 1; i < data.Count; i++)
                {
                    double x = origin.X + (data[i].Item1 - minX) / (maxX - minX) * plotWidth;
                    double y = origin.Y - (data[i].Item2 - minY) / (maxY - minY) * plotHeight;
                    ctx.LineTo(new Point(x, y), true, true);
                }
                double lastX = origin.X + (data[^1].Item1 - minX) / (maxX - minX) * plotWidth;
                ctx.LineTo(new Point(lastX, origin.Y), true, true);
            }
            areaGeometry.Freeze();
            var areaBrush = new LinearGradientBrush(Color.FromArgb(90, ChartPlum.R, ChartPlum.G, ChartPlum.B), Color.FromArgb(20, ChartPlum.R, ChartPlum.G, ChartPlum.B), new Point(0.5, 0), new Point(0.5, 1));
            areaBrush.Freeze();
            dc.DrawGeometry(areaBrush, null, areaGeometry);

            var lineGeometry = new StreamGeometry();
            using (var ctx = lineGeometry.Open())
            {
                for (int i = 0; i < data.Count; i++)
                {
                    double x = origin.X + (data[i].Item1 - minX) / (maxX - minX) * plotWidth;
                    double y = origin.Y - (data[i].Item2 - minY) / (maxY - minY) * plotHeight;
                    if (i == 0)
                        ctx.BeginFigure(new Point(x, y), false, false);
                    else
                        ctx.LineTo(new Point(x, y), true, true);
                }
            }
            lineGeometry.Freeze();
            var linePen = new Pen(new SolidColorBrush(ChartPlum), 2.3)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            linePen.Freeze();
            dc.DrawGeometry(null, linePen, lineGeometry);

            var markerBrush = new SolidColorBrush(ChartPlum);
            markerBrush.Freeze();
            foreach (var point in data)
            {
                double x = origin.X + (point.Item1 - minX) / (maxX - minX) * plotWidth;
                double y = origin.Y - (point.Item2 - minY) / (maxY - minY) * plotHeight;
                dc.DrawEllipse(markerBrush, null, new Point(x, y), 3.0, 3.0);
            }

            double highlightX = origin.X + (Math.Clamp(udv.Points, minX, maxX) - minX) / (maxX - minX) * plotWidth;
            double highlightY = origin.Y - (Math.Clamp(udv.UnitDayValue, minY, maxY) - minY) / (maxY - minY) * plotHeight;
            var highlightBrush = new SolidColorBrush(ChartOrange);
            highlightBrush.Freeze();
            dc.DrawEllipse(highlightBrush, null, new Point(highlightX, highlightY), 5.0, 5.0);

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var axisBrush = new SolidColorBrush(ChartGray);
            axisBrush.Freeze();

            for (int i = 0; i <= verticalTickCount; i++)
            {
                double value = minX + (maxX - minX) * i / verticalTickCount;
                var label = new FormattedText($"{value:0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
                double x = origin.X + (plotWidth * i / verticalTickCount) - label.Width / 2.0;
                double y = origin.Y + 10;
                dc.DrawText(label, new Point(x, y));
            }
            for (int i = 0; i <= horizontalTickCount; i++)
            {
                double value = minY + (maxY - minY) * i / horizontalTickCount;
                var label = new FormattedText(FormatCurrencyLabel(value), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
                double x = marginLeft - label.Width - 8;
                double y = origin.Y - (plotHeight * i / horizontalTickCount) - label.Height / 2.0;
                dc.DrawText(label, new Point(x, y));
            }

            var title = new FormattedText("Recreation Value Curve", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, new SolidColorBrush(ChartPlum), 1.0);
            title.SetFontWeight(FontWeights.SemiBold);
            dc.DrawText(title, new Point(marginLeft, marginTop - title.Height - 14));

            var highlightLabel = new FormattedText($"Selected UDV: {udv.UnitDayValue:C2}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, ThemeResourceHelper.GetBrush("App.OnAccent", Brushes.White), 1.0);
            highlightLabel.SetFontWeight(FontWeights.SemiBold);
            var badgeRect = new Rect(origin.X + plotWidth - highlightLabel.Width - 24, marginTop - highlightLabel.Height - 16, highlightLabel.Width + 20, highlightLabel.Height + 10);
            var badgeBrush = new SolidColorBrush(ChartPlum) { Opacity = 0.9 };
            badgeBrush.Freeze();
            var badgeGeometry = new RectangleGeometry(badgeRect, 8, 8);
            dc.DrawGeometry(badgeBrush, null, badgeGeometry);
            dc.DrawText(highlightLabel, new Point(badgeRect.X + 10, badgeRect.Y + 5));

            var xAxisLabel = new FormattedText("Point Score", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, 1.0);
            dc.DrawText(xAxisLabel, new Point(origin.X + (plotWidth - xAxisLabel.Width) / 2.0, origin.Y + 28));

            var yAxisLabel = new FormattedText("Unit Day Value ($)", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, 1.0);
            yAxisLabel.SetFontWeight(FontWeights.SemiBold);
            dc.PushTransform(new RotateTransform(-90, marginLeft - 50, marginTop + plotHeight / 2.0));
            dc.DrawText(yAxisLabel, new Point(marginLeft - 50 - yAxisLabel.Width / 2.0, marginTop + plotHeight / 2.0 - yAxisLabel.Height / 2.0));
            dc.Pop();

            RenderTargetBitmap rtb = new((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using MemoryStream ms = new();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static Color GetColorFromBrush(Brush brush, Color fallback)
        {
            return brush switch
            {
                SolidColorBrush solid => solid.Color,
                LinearGradientBrush gradient when gradient.GradientStops.Count > 0 => gradient.GradientStops[^1].Color,
                _ => fallback
            };
        }

        private static string FormatCurrencyLabel(double value)
        {
            double abs = Math.Abs(value);
            if (abs >= 1_000_000_000)
                return $"${value / 1_000_000_000:0.##}B";
            if (abs >= 1_000_000)
                return $"${value / 1_000_000:0.##}M";
            if (abs >= 1_000)
                return $"${value / 1_000:0.#}K";
            return $"${value:0}";
        }

        private static string FormatNumberLabel(double value)
        {
            double abs = Math.Abs(value);
            if (abs >= 1_000_000)
                return $"{value / 1_000_000:0.##}M";
            if (abs >= 1_000)
                return $"{value / 1_000:0.#}K";
            return $"{value:0}";
        }

        private static string GetUdvColumnKey(UdvViewModel udv)
        {
            return (udv.RecreationType, udv.ActivityType) switch
            {
                ("General", "General Recreation") => "General Recreation",
                ("General", "Fishing and Hunting") => "General Fishing and Hunting",
                ("Specialized", "Fishing and Hunting") => "Specialized Fishing and Hunting",
                ("Specialized", "Other (e.g., Boating)") => "Specialized Recreation",
                _ => "General Recreation"
            };
        }

        private static double GetUdvColumnValue(PointValueRow row, string columnKey)
        {
            return columnKey switch
            {
                "General Recreation" => row.GeneralRecreation,
                "General Fishing and Hunting" => row.GeneralFishingHunting,
                "Specialized Fishing and Hunting" => row.SpecializedFishingHunting,
                "Specialized Recreation" => row.SpecializedRecreation,
                _ => row.GeneralRecreation
            };
        }

        private static IReadOnlyList<Point> BuildEadPlotPoints(IEnumerable<EadViewModel.EadRow> rows, bool useStage)
        {
            var ordered = useStage
                ? rows.Where(r => r.Stage.HasValue && r.Damages.Count > 0)
                      .OrderBy(r => r.Stage!.Value)
                      .Select(r => new Point(r.Stage!.Value, r.Damages.FirstOrDefault()))
                : rows.Where(r => r.Damages.Count > 0)
                      .OrderBy(r => r.Probability)
                      .Select(r => new Point(r.Probability, r.Damages.FirstOrDefault()));

            return ordered.ToList();
        }

        private static void AddEadChart(IXLWorksheet ws, IReadOnlyList<Point>? damagePoints, int row, int column, ExportContext context)
        {
            if (damagePoints == null || damagePoints.Count == 0)
                return;
            byte[] img = CreateEadChartImage(damagePoints);
            using var stream = new MemoryStream(img);
            var pic = ws.AddPicture(stream, XLPictureFormat.Png, context.GetPictureName("EADChart_"));
            pic.MoveTo(ws.Cell(row, column));
        }

        private static byte[] CreateEadChartImage(IReadOnlyList<Point>? damagePoints)
        {
            const double width = 300;
            const double height = 150;
            if (damagePoints == null || damagePoints.Count < 2)
                return Array.Empty<byte>();

            double minX = damagePoints.Min(p => p.X);
            double maxX = damagePoints.Max(p => p.X);
            double minY = damagePoints.Min(p => p.Y);
            double maxY = damagePoints.Max(p => p.Y);

            if (Math.Abs(maxX - minX) < double.Epsilon || Math.Abs(maxY - minY) < double.Epsilon)
                return Array.Empty<byte>();

            double xPadding = (maxX - minX) * 0.05;
            double yPadding = (maxY - minY) * 0.1;

            double paddedMinX = minX - xPadding;
            double paddedMaxX = maxX + xPadding;
            double paddedMinY = minY - yPadding;
            double paddedMaxY = maxY + yPadding;

            var scaledPoints = damagePoints
                .OrderBy(p => p.X)
                .Select(p => new Point(
                    (p.X - paddedMinX) / (paddedMaxX - paddedMinX) * width,
                    height - (p.Y - paddedMinY) / (paddedMaxY - paddedMinY) * height))
                .ToArray();

            DrawingVisual dv = new();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(ThemeResourceHelper.GetBrush("App.Surface", Brushes.White), null, new Rect(0, 0, width, height));
                DrawPolyline(dc, scaledPoints, new Pen(new SolidColorBrush(ChartBlue), 2));
            }
            RenderTargetBitmap rtb = new((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using MemoryStream ms = new();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static void DrawPolyline(DrawingContext dc, IReadOnlyList<Point> points, Pen pen)
        {
            if (points.Count < 2) return;
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(points[0], false, false);
                ctx.PolyLineTo(points.Skip(1).ToArray(), true, false);
            }
            dc.DrawGeometry(null, pen, geom);
        }
    }
}
