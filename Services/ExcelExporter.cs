using System;
using System.Collections.Generic;
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

namespace EconToolbox.Desktop.Services
{
    public static class ExcelExporter
    {
        private const int ExcelMaxPictureNameLength = 31;

        private static readonly Color ChartBlue = (Color)ColorConverter.ConvertFromString("#2D6A8E");
        private static readonly Color ChartTeal = (Color)ColorConverter.ConvertFromString("#1ABC9C");
        private static readonly Color ChartOrange = (Color)ColorConverter.ConvertFromString("#F39C12");
        private static readonly Color ChartPlum = (Color)ColorConverter.ConvertFromString("#7F56D9");
        private static readonly Color ChartGray = (Color)ColorConverter.ConvertFromString("#6B7280");

        private static readonly XLColor DashboardHeaderFill = XLColor.FromHtml("#2D6A8E");
        private static readonly XLColor DashboardHeaderText = XLColor.White;
        private static readonly XLColor DashboardSubHeaderFill = XLColor.FromHtml("#EFF5FB");
        private static readonly XLColor DashboardRowLight = XLColor.FromHtml("#FFFFFF");
        private static readonly XLColor DashboardRowAlt = XLColor.FromHtml("#F6F9FC");
        private static readonly XLColor DashboardBorder = XLColor.FromHtml("#D0D7E5");
        private static readonly XLColor DashboardAccentText = XLColor.FromHtml("#2D6A8E");
        private static readonly XLColor DashboardPrimaryText = XLColor.FromHtml("#1F2937");

        public static void ExportCapitalRecovery(double rate, int periods, double factor, string filePath)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("CapitalRecovery");
            ws.Cell(1,1).Value = "Rate";
            ws.Cell(1,2).Value = rate;
            ws.Cell(2,1).Value = "Periods";
            ws.Cell(2,2).Value = periods;
            ws.Cell(3,1).Value = "Factor";
            ws.Cell(3,2).Value = factor;
            wb.SaveAs(filePath);
        }

        public static void ExportWaterDemand(IEnumerable<Scenario> scenarios, string filePath)
        {
            using var wb = new XLWorkbook();
            foreach (var scenario in scenarios)
            {
                var name = string.IsNullOrWhiteSpace(scenario.Name) ? "Scenario" : scenario.Name;
                var ws = wb.Worksheets.Add(name);
                ws.Cell(1,1).Value = "Year";
                ws.Cell(1,2).Value = "Demand";
                ws.Cell(1,3).Value = "Residential";
                ws.Cell(1,4).Value = "Commercial";
                ws.Cell(1,5).Value = "Industrial";
                ws.Cell(1,6).Value = "Agricultural";
                ws.Cell(1,7).Value = "Adjusted";
                ws.Cell(1,8).Value = "Adjusted (ac-ft/yr)";
                ws.Cell(1,2).GetComment().AddText("Demand = Prior Demand × (1 + Growth Rate)");
                ws.Cell(1,3).GetComment().AddText("Residential = Demand × Residential %");
                ws.Cell(1,4).GetComment().AddText("Commercial = Demand × Commercial %");
                ws.Cell(1,5).GetComment().AddText("Industrial = Demand × Industrial %");
                ws.Cell(1,6).GetComment().AddText("Agricultural = Demand × Agricultural %");
                ws.Cell(1,7).GetComment().AddText("Adjusted = Demand ÷ (1 - Losses %) × (1 - Improvements %)");
                ws.Cell(1,8).GetComment().AddText("Adjusted Acre-Feet = Adjusted Demand × 365 ÷ 325,851");
                int row = 2;
                foreach (var d in scenario.Results)
                {
                    ws.Cell(row,1).Value = d.Year;
                    ws.Cell(row,2).Value = d.Demand;
                    ws.Cell(row,3).Value = d.ResidentialDemand;
                    ws.Cell(row,4).Value = d.CommercialDemand;
                    ws.Cell(row,5).Value = d.IndustrialDemand;
                    ws.Cell(row,6).Value = d.AgriculturalDemand;
                    ws.Cell(row,7).Value = d.AdjustedDemand;
                    ws.Cell(row,8).Value = d.AdjustedDemandAcreFeet;
                    row++;
                }
            }
            wb.SaveAs(filePath);
        }

        public static void ExportAnnualizer(double firstCost, double rate, double annualOm, double annualBenefits, IEnumerable<FutureCostEntry> future, double idc, double totalInvestment, double crf, double annualCost, double bcr, string filePath)
        {
            using var wb = new XLWorkbook();
            var summary = wb.Worksheets.Add("Summary");
            var data = new Dictionary<string, double>
            {
                {"First Cost", firstCost},
                {"Rate", rate},
                {"Annual O&M", annualOm},
                {"Annual Benefits", annualBenefits},
                {"IDC", idc},
                {"Total Investment", totalInvestment},
                {"CRF", crf},
                {"Annual Cost", annualCost},
                {"BCR", bcr}
            };
            int row = 1;
            foreach(var kv in data)
            {
                summary.Cell(row,1).Value = kv.Key;
                var cell = summary.Cell(row,2);
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
                row++;
            }

            var fcSheet = wb.Worksheets.Add("FutureCosts");
            fcSheet.Cell(1,1).Value = "Cost";
            fcSheet.Cell(1,2).Value = "Year";
            row = 2;
            foreach(var f in future)
            {
                fcSheet.Cell(row,1).Value = f.Cost;
                fcSheet.Cell(row,2).Value = f.Year;
                row++;
            }
            wb.SaveAs(filePath);
        }

        public static void ExportEad(IEnumerable<EadViewModel.EadRow> rows, IEnumerable<string> damageColumns, bool useStage, string result, PointCollection stagePoints, PointCollection frequencyPoints, string filePath)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("EAD");
            int col = 1;
            ws.Cell(1, col++).Value = "Probability";
            if (useStage)
                ws.Cell(1, col++).Value = "Stage";
            int dcCount = damageColumns.Count();
            foreach (var name in damageColumns)
                ws.Cell(1, col++).Value = name;

            int rowIdx = 2;
            foreach (var r in rows)
            {
                col = 1;
                ws.Cell(rowIdx, col++).Value = r.Probability;
                if (useStage)
                    ws.Cell(rowIdx, col++).Value = r.Stage;
                for (int i = 0; i < dcCount; i++)
                    ws.Cell(rowIdx, col++).Value = r.Damages.Count > i ? r.Damages[i] : 0;
                rowIdx++;
            }

            var summary = wb.Worksheets.Add("Summary");
            summary.Cell(1,1).Value = "Result";
            summary.Cell(1,2).Value = result;
            AddEadChart(summary, stagePoints, frequencyPoints, 3, 1);
            wb.SaveAs(filePath);
        }

        public static void ExportAll(EadViewModel ead, UpdatedCostViewModel updated, AnnualizerViewModel annualizer, WaterDemandViewModel waterDemand, UdvViewModel udv, MindMapViewModel mindMap, string filePath)
        {
            using var wb = new XLWorkbook();

            // EAD Sheet
            var eadSheet = wb.Worksheets.Add("EAD");
            int col = 1;
            eadSheet.Cell(1, col++).Value = "Probability";
            if (ead.UseStage)
                eadSheet.Cell(1, col++).Value = "Stage";
            int dcCount = ead.DamageColumns.Count;
            foreach (var name in ead.DamageColumns.Select(c => c.Name))
                eadSheet.Cell(1, col++).Value = name;
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
            eadSheet.Cell(rowIdx + 1, 1).Value = "Result";
            eadSheet.Cell(rowIdx + 1, 2).Value = string.Join(" | ", ead.Results);
            AddEadChart(eadSheet, ead.StageDamagePoints, ead.FrequencyDamagePoints, rowIdx + 3, 1);

            // Annualizer Sheets
            var annSummary = wb.Worksheets.Add("Annualizer");
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
            var annFc = wb.Worksheets.Add("FutureCosts");
            annFc.Cell(1,1).Value = "Cost";
            annFc.Cell(1,2).Value = "Year";
            rowIdx = 2;
            foreach (var f in annualizer.FutureCosts)
            {
                annFc.Cell(rowIdx,1).Value = f.Cost;
                annFc.Cell(rowIdx,2).Value = f.Year;
                rowIdx++;
            }

            // Water Demand Sheet
            foreach (var scenario in waterDemand.Scenarios)
            {
                var wdSheet = wb.Worksheets.Add($"WaterDemand_{scenario.Name}");
                wdSheet.Cell(1,1).Value = "Year";
                wdSheet.Cell(1,2).Value = "Demand";
                wdSheet.Cell(1,3).Value = "Industrial";
                wdSheet.Cell(1,4).Value = "Adjusted";
                wdSheet.Cell(1,5).Value = "Adjusted (ac-ft/yr)";
                wdSheet.Cell(1,2).GetComment().AddText("Demand = Prior Demand × (1 + Growth Rate)");
                wdSheet.Cell(1,3).GetComment().AddText("Industrial = Demand × Industrial %");
                wdSheet.Cell(1,4).GetComment().AddText("Adjusted = Demand ÷ (1 - Losses %) × (1 - Improvements %)");
                wdSheet.Cell(1,5).GetComment().AddText("Adjusted Acre-Feet = Adjusted Demand × 365 ÷ 325,851");
                rowIdx = 2;
                foreach (var d in scenario.Results)
                {
                    wdSheet.Cell(rowIdx,1).Value = d.Year;
                    wdSheet.Cell(rowIdx,2).Value = d.Demand;
                    wdSheet.Cell(rowIdx,3).Value = d.IndustrialDemand;
                    wdSheet.Cell(rowIdx,4).Value = d.AdjustedDemand;
                    wdSheet.Cell(rowIdx,5).Value = d.AdjustedDemandAcreFeet;
                    rowIdx++;
                }
            }

            // Updated Cost Sheets
            var ucItems = wb.Worksheets.Add("UpdatedCost");
            ucItems.Cell(1,1).Value = "Category";
            ucItems.Cell(1,2).Value = "Actual Cost";
            ucItems.Cell(1,3).Value = "Update Factor";
            ucItems.Cell(1,4).Value = "Updated Cost";
            rowIdx = 2;
            foreach (var item in updated.UpdatedCostItems)
            {
                ucItems.Cell(rowIdx,1).Value = item.Category;
                ucItems.Cell(rowIdx,2).Value = item.ActualCost;
                ucItems.Cell(rowIdx,3).Value = item.UpdateFactor;
                ucItems.Cell(rowIdx,4).Value = item.UpdatedCost;
                rowIdx++;
            }
            var ucRrr = wb.Worksheets.Add("RRR");
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
            var ucSummary = wb.Worksheets.Add("UpdatedCostSummary");
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
                    cell.GetComment().AddText("Total Updated Cost = Σ(Actual Cost × Update Factor)");
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

            // Unit Day Value Sheet
            var udvSheet = wb.Worksheets.Add("Udv");
            var udvData = new Dictionary<string, object>
            {
                {"Recreation Type", udv.RecreationType},
                {"Activity Type", udv.ActivityType},
                {"Points", udv.Points},
                {"Unit Day Value", udv.UnitDayValue},
                {"User Days", udv.UserDays},
                {"Visitation", udv.Visitation},
                {"Result", udv.Result}
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

            // Mind Map Sheet
            var mindMapSheet = wb.Worksheets.Add("MindMap");
            mindMapSheet.Cell(1,1).Value = "Depth";
            mindMapSheet.Cell(1,2).Value = "Idea";
            mindMapSheet.Cell(1,3).Value = "Path";
            mindMapSheet.Cell(1,4).Value = "Notes";
            int mindMapRow = 2;
            foreach (var node in mindMap.Flatten())
            {
                var path = node.GetPath().ToList();
                mindMapSheet.Cell(mindMapRow,1).Value = path.Count - 1;
                mindMapSheet.Cell(mindMapRow,2).Value = node.Title;
                mindMapSheet.Cell(mindMapRow,3).Value = string.Join(" > ", path.Select(p => p.Title));
                mindMapSheet.Cell(mindMapRow,4).Value = node.Notes;
                mindMapRow++;
            }
            if (mindMapRow > 2)
            {
                mindMapSheet.Columns(1,4).AdjustToContents();
            }

            BuildDashboard(wb, ead, annualizer, updated, waterDemand, udv, mindMap);

            wb.SaveAs(filePath);
        }

        private static void BuildDashboard(XLWorkbook wb, EadViewModel ead, AnnualizerViewModel annualizer, UpdatedCostViewModel updated, WaterDemandViewModel waterDemand, UdvViewModel udv, MindMapViewModel mindMap)
        {
            var ws = wb.Worksheets.Add("Dashboard");
            ws.Position = 1;
            ws.Style.Font.SetFontName("Segoe UI");
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
                ("Total Updated Cost", updated.TotalUpdatedCost, "$#,##0.00", "Σ(Actual Cost × Update Factor).", false),
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
            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Capital Investment Metrics", financialRows);
            AddAnnualizerComparisonChart(ws, annualizer.AnnualBenefits, annualizer.AnnualCost, annualizer.Bcr, capitalStart, 5);

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
                eadRows.Add(("Summary", string.Join(" | ", ead.Results), null, "Combined textual output from the calculator.", false));
            }
            else
            {
                eadRows.Add(("EAD Status", "Enter frequency and damage data to compute.", null, null, false));
            }

            int eadStart = currentRow;
            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Expected Annual Damage", eadRows);
            AddEadDashboardChart(ws, ead, primaryDamageColumn, primaryEadValue, eadStart, 5);

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
            currentRow = WriteWaterDemandTable(ws, currentRow, 1, "Water Demand Outlook", waterEntries);
            AddWaterDemandChart(ws, waterDemand.Scenarios, waterStart, 5);

            var mindMapNodes = mindMap.Flatten().ToList();
            int notedIdeas = mindMapNodes.Count(n => !string.IsNullOrWhiteSpace(n.Notes));
            string primaryThemes = mindMap.Nodes.Count > 0
                ? string.Join(" | ", mindMap.Nodes.Select(n => n.Title))
                : "Add ideas to build the map";

            double recreationBenefit = UdvModel.ComputeBenefit(udv.UnitDayValue, udv.UserDays, udv.Visitation);
            var udvRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("Recreation Type", udv.RecreationType, null, null, false),
                ("Activity Type", udv.ActivityType, null, null, false),
                ("Point Score", udv.Points, "0.0", "Score applied against the recreation look-up table.", false),
                ("Unit Day Value", udv.UnitDayValue, "$#,##0.00", "Interpolated value from the recreation tables.", true),
                ("User Days", udv.UserDays, "#,##0", "Projected annual participation.", false),
                ("Visitation Multiplier", udv.Visitation, "0.00", "Adjustment applied to user days.", false),
                ("Annual Recreation Benefit", recreationBenefit, "$#,##0.00", "Unit Day Value × User Days × Visitation.", true)
            };

            var mindMapRows = new List<(string Label, object Value, string? Format, string? Comment, bool Highlight)>
            {
                ("Ideas Documented", mindMapNodes.Count, "0", "Total nodes captured within the mind map.", false),
                ("Ideas With Notes", notedIdeas, "0", "Nodes that include detailed notes.", false),
                ("Primary Themes", primaryThemes, null, "Top-level branches currently defined.", false)
            };

            int udvStart = currentRow;
            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Recreation Highlights", udvRows);
            AddUdvChart(ws, udv, udvStart, 5);

            currentRow = WriteKeyValueTable(ws, currentRow, 1, "Mind Map Highlights", mindMapRows);
        }

        private static int WriteKeyValueTable(IXLWorksheet ws, int startRow, int startColumn, string title, List<(string Label, object Value, string? Format, string? Comment, bool Highlight)> entries)
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
            return row + 2;
        }

        private static int WriteWaterDemandTable(IXLWorksheet ws, int startRow, int startColumn, string title, List<(string Scenario, int Year, double Adjusted, string? Description, double? ChangePercent, bool Highlight, bool HasResults)> entries)
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
            return row + 2;
        }

        private static string CreatePictureName(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "Picture";

            if (prefix.Length > ExcelMaxPictureNameLength)
                prefix = prefix.Substring(0, ExcelMaxPictureNameLength);

            var uniquePart = Guid.NewGuid().ToString("N");
            int maxUniqueLength = ExcelMaxPictureNameLength - prefix.Length;
            if (maxUniqueLength <= 0)
                return prefix;

            return prefix + uniquePart.Substring(0, Math.Min(maxUniqueLength, uniquePart.Length));
        }

        private static void AddAnnualizerComparisonChart(IXLWorksheet ws, double annualBenefits, double annualCost, double bcr, int row, int column)
        {
            byte[] bytes = CreateAnnualizerBarChartImage(annualBenefits, annualCost, bcr);
            using var stream = new MemoryStream(bytes);
            var picture = ws.AddPicture(stream, XLPictureFormat.Png, CreatePictureName("AnnualizerChart_"));
            picture.MoveTo(ws.Cell(row, column));
        }

        private static void AddEadDashboardChart(IXLWorksheet ws, EadViewModel ead, string curveName, double? eadValue, int row, int column)
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
            var picture = ws.AddPicture(stream, XLPictureFormat.Png, CreatePictureName("EadDashboardChart_"));
            picture.MoveTo(ws.Cell(row, column));
        }

        private static void AddWaterDemandChart(IXLWorksheet ws, IEnumerable<Scenario> scenarios, int row, int column)
        {
            var series = scenarios
                .Select(s => new
                {
                    s.Name,
                    Points = s.Results.Select(r => ((double)r.Year, r.AdjustedDemand)).ToList(),
                    Color = GetColorFromBrush(s.LineBrush, ChartBlue)
                })
                .Where(s => s.Points.Count >= 2)
                .ToList();
            if (series.Count == 0)
                return;

            var data = series.Select(s => (s.Name, s.Points, s.Color)).ToList();
            byte[] bytes = CreateWaterDemandChartImage(data);
            using var stream = new MemoryStream(bytes);
            var picture = ws.AddPicture(stream, XLPictureFormat.Png, CreatePictureName("WaterDemandChart_"));
            picture.MoveTo(ws.Cell(row, column));
        }

        private static void AddUdvChart(IXLWorksheet ws, UdvViewModel udv, int row, int column)
        {
            byte[] bytes = CreateUdvChartImage(udv);
            if (bytes.Length == 0)
                return;
            using var stream = new MemoryStream(bytes);
            var picture = ws.AddPicture(stream, XLPictureFormat.Png, CreatePictureName("UdvChart_"));
            picture.MoveTo(ws.Cell(row, column));
        }

        private static byte[] CreateAnnualizerBarChartImage(double annualBenefits, double annualCost, double bcr)
        {
            const double width = 380;
            const double height = 240;
            const double marginLeft = 70;
            const double marginRight = 30;
            const double marginTop = 50;
            const double marginBottom = 65;

            double plotWidth = width - marginLeft - marginRight;
            double plotHeight = height - marginTop - marginBottom;

            DrawingVisual dv = new();
            using var dc = dv.RenderOpen();

            var background = new LinearGradientBrush(Color.FromRgb(248, 251, 255), Color.FromRgb(228, 236, 250), new Point(0, 0), new Point(0, 1));
            background.Freeze();
            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));

            var origin = new Point(marginLeft, marginTop + plotHeight);
            var axisPen = new Pen(new SolidColorBrush(ChartGray), 1.0);
            axisPen.Freeze();
            dc.DrawLine(axisPen, origin, new Point(origin.X + plotWidth, origin.Y));
            dc.DrawLine(axisPen, origin, new Point(origin.X, marginTop));

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(90, ChartGray.R, ChartGray.G, ChartGray.B)), 0.6)
            {
                DashStyle = DashStyles.Dot
            };
            gridPen.Freeze();
            int gridLines = 4;
            for (int i = 1; i <= gridLines; i++)
            {
                double y = origin.Y - (plotHeight / gridLines) * i;
                dc.DrawLine(gridPen, new Point(origin.X, y), new Point(origin.X + plotWidth, y));
            }

            var values = new[] { annualBenefits, annualCost };
            var labels = new[] { "Annual Benefits", "Annual Cost" };
            var colors = new[] { ChartTeal, ChartBlue };
            double maxValue = Math.Max(values.Max(), 1.0);

            var typeface = new Typeface("Segoe UI");
            var axisBrush = new SolidColorBrush(ChartGray);
            axisBrush.Freeze();

            for (int i = 0; i < values.Length; i++)
            {
                double columnWidth = plotWidth / values.Length;
                double barWidth = columnWidth * 0.55;
                double barHeight = Math.Clamp(values[i] / maxValue, 0.0, 1.0) * plotHeight;
                double x = marginLeft + columnWidth * i + (columnWidth - barWidth) / 2.0;
                var rect = new Rect(x, origin.Y - barHeight, barWidth, barHeight);

                var barBrush = new LinearGradientBrush(
                    Color.FromArgb(210, (byte)Math.Min(colors[i].R + 30, 255), (byte)Math.Min(colors[i].G + 30, 255), (byte)Math.Min(colors[i].B + 30, 255)),
                    Color.FromArgb(255, colors[i].R, colors[i].G, colors[i].B),
                    new Point(0.5, 0),
                    new Point(0.5, 1));
                barBrush.Freeze();
                var barGeometry = new RectangleGeometry(rect, 8, 8);
                dc.DrawGeometry(barBrush, null, barGeometry);

                string valueLabel = FormatCurrencyLabel(values[i]);
                var valueText = new FormattedText(valueLabel, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12, new SolidColorBrush(colors[i]), 1.0);
                valueText.SetFontWeight(FontWeights.SemiBold);
                double labelX = rect.X + (rect.Width - valueText.Width) / 2.0;
                double labelY = rect.Y - valueText.Height - 6;
                if (labelY < marginTop + 4)
                    labelY = rect.Y + 6;
                dc.DrawText(valueText, new Point(labelX, labelY));

                var caption = new FormattedText(labels[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, 1.0);
                double captionX = rect.X + (rect.Width - caption.Width) / 2.0;
                double captionY = origin.Y + 20;
                dc.DrawText(caption, new Point(captionX, captionY));
            }

            for (int i = 0; i <= gridLines; i++)
            {
                double value = maxValue * i / gridLines;
                string label = FormatCurrencyLabel(value);
                var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
                double x = marginLeft - text.Width - 8;
                double y = origin.Y - (plotHeight * i / gridLines) - text.Height / 2.0;
                dc.DrawText(text, new Point(x, y));
            }

            string titleText = "Annual Benefits vs. Cost";
            var title = new FormattedText(titleText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, new SolidColorBrush(ChartBlue), 1.0);
            title.SetFontWeight(FontWeights.SemiBold);
            dc.DrawText(title, new Point(marginLeft, marginTop - title.Height - 14));

            string bcrLabel = $"BCR: {bcr:0.00}";
            var bcrText = new FormattedText(bcrLabel, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12, Brushes.White, 1.0);
            bcrText.SetFontWeight(FontWeights.SemiBold);
            var badgeRect = new Rect(origin.X + plotWidth - bcrText.Width - 26, marginTop - bcrText.Height - 18, bcrText.Width + 24, bcrText.Height + 12);
            var badgeBrush = new SolidColorBrush(ChartOrange) { Opacity = 0.85 };
            badgeBrush.Freeze();
            var badgeGeometry = new RectangleGeometry(badgeRect, 8, 8);
            dc.DrawGeometry(badgeBrush, null, badgeGeometry);
            dc.DrawText(bcrText, new Point(badgeRect.X + 12, badgeRect.Y + 6));

            var xAxisLabel = new FormattedText("Investment Metrics", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
            dc.DrawText(xAxisLabel, new Point(origin.X + (plotWidth - xAxisLabel.Width) / 2.0, origin.Y + 42));

            RenderTargetBitmap rtb = new((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using MemoryStream ms = new();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static byte[] CreateEadDashboardChartImage(List<(double Probability, double? Stage, double Damage)> data, string curveName, double? eadValue)
        {
            const double width = 420;
            const double height = 260;
            const double marginLeft = 70;
            const double marginRight = 30;
            const double marginTop = 60;
            const double marginBottom = 70;

            double plotWidth = width - marginLeft - marginRight;
            double plotHeight = height - marginTop - marginBottom;
            double maxDamage = Math.Max(data.Max(d => d.Damage), 1.0);

            DrawingVisual dv = new();
            using var dc = dv.RenderOpen();

            var background = new LinearGradientBrush(Color.FromRgb(247, 250, 255), Color.FromRgb(224, 235, 250), new Point(0, 0), new Point(0, 1));
            background.Freeze();
            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));

            var origin = new Point(marginLeft, marginTop + plotHeight);
            var axisPen = new Pen(new SolidColorBrush(ChartGray), 1.0);
            axisPen.Freeze();
            dc.DrawLine(axisPen, origin, new Point(origin.X + plotWidth, origin.Y));
            dc.DrawLine(axisPen, origin, new Point(origin.X, marginTop));

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(80, ChartGray.R, ChartGray.G, ChartGray.B)), 0.7)
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
                var firstPoint = data[0];
                double firstX = origin.X + firstPoint.Probability * plotWidth;
                double firstY = origin.Y - (firstPoint.Damage / maxDamage) * plotHeight;
                ctx.BeginFigure(new Point(firstX, origin.Y), true, true);
                ctx.LineTo(new Point(firstX, firstY), true, true);
                for (int i = 1; i < data.Count; i++)
                {
                    double x = origin.X + data[i].Probability * plotWidth;
                    double y = origin.Y - (data[i].Damage / maxDamage) * plotHeight;
                    ctx.LineTo(new Point(x, y), true, true);
                }
                double lastX = origin.X + data[^1].Probability * plotWidth;
                ctx.LineTo(new Point(lastX, origin.Y), true, true);
            }
            areaGeometry.Freeze();
            var areaBrush = new LinearGradientBrush(Color.FromArgb(90, ChartTeal.R, ChartTeal.G, ChartTeal.B), Color.FromArgb(15, ChartTeal.R, ChartTeal.G, ChartTeal.B), new Point(0.5, 0), new Point(0.5, 1));
            areaBrush.Freeze();
            dc.DrawGeometry(areaBrush, null, areaGeometry);

            var lineGeometry = new StreamGeometry();
            using (var ctx = lineGeometry.Open())
            {
                for (int i = 0; i < data.Count; i++)
                {
                    double x = origin.X + data[i].Probability * plotWidth;
                    double y = origin.Y - (data[i].Damage / maxDamage) * plotHeight;
                    if (i == 0)
                        ctx.BeginFigure(new Point(x, y), false, false);
                    else
                        ctx.LineTo(new Point(x, y), true, true);
                }
            }
            lineGeometry.Freeze();
            var linePen = new Pen(new SolidColorBrush(ChartTeal), 2.3)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            linePen.Freeze();
            dc.DrawGeometry(null, linePen, lineGeometry);

            var markerBrush = new SolidColorBrush(ChartTeal);
            markerBrush.Freeze();
            bool showStageLabels = data.Any(d => d.Stage.HasValue) && data.Count <= 6;
            var stageBrush = new SolidColorBrush(ChartGray);
            stageBrush.Freeze();
            var typeface = new Typeface("Segoe UI");

            for (int i = 0; i < data.Count; i++)
            {
                double x = origin.X + data[i].Probability * plotWidth;
                double y = origin.Y - (data[i].Damage / maxDamage) * plotHeight;
                var point = new Point(x, y);
                dc.DrawEllipse(markerBrush, null, point, 3.5, 3.5);

                if (showStageLabels && data[i].Stage.HasValue)
                {
                    string stageText = $"Stage {data[i].Stage!.Value:F1}";
                    var stageLabel = new FormattedText(stageText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, stageBrush, 1.0);
                    double labelX = point.X - stageLabel.Width / 2.0;
                    double labelY = point.Y - stageLabel.Height - 10;
                    if (labelY < marginTop + 4)
                        labelY = point.Y + 6;
                    dc.DrawText(stageLabel, new Point(labelX, labelY));
                }
            }

            var axisBrush = new SolidColorBrush(ChartGray);
            axisBrush.Freeze();
            for (int i = 0; i <= verticalTickCount; i++)
            {
                double fraction = i / (double)verticalTickCount;
                double probability = fraction;
                var label = new FormattedText($"{probability * 100:0}%", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
                double x = origin.X + fraction * plotWidth - label.Width / 2.0;
                double y = origin.Y + 10;
                dc.DrawText(label, new Point(x, y));
            }
            for (int i = 0; i <= horizontalTickCount; i++)
            {
                double value = maxDamage * i / horizontalTickCount;
                string textValue = FormatCurrencyLabel(value);
                var label = new FormattedText(textValue, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
                double x = marginLeft - label.Width - 8;
                double y = origin.Y - (plotHeight * i / horizontalTickCount) - label.Height / 2.0;
                dc.DrawText(label, new Point(x, y));
            }

            var title = new FormattedText($"{curveName} Damage Curve", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, new SolidColorBrush(ChartBlue), 1.0);
            title.SetFontWeight(FontWeights.SemiBold);
            dc.DrawText(title, new Point(marginLeft, marginTop - title.Height - 16));

            if (eadValue.HasValue)
            {
                var eadBadge = new FormattedText($"EAD ≈ {eadValue.Value:C0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12, Brushes.White, 1.0);
                eadBadge.SetFontWeight(FontWeights.SemiBold);
                var rect = new Rect(origin.X + plotWidth - eadBadge.Width - 26, marginTop - eadBadge.Height - 18, eadBadge.Width + 24, eadBadge.Height + 12);
                var badgeBrush = new SolidColorBrush(ChartBlue) { Opacity = 0.85 };
                badgeBrush.Freeze();
                var geometry = new RectangleGeometry(rect, 8, 8);
                dc.DrawGeometry(badgeBrush, null, geometry);
                dc.DrawText(eadBadge, new Point(rect.X + 12, rect.Y + 6));
            }

            var xAxisLabel = new FormattedText("Exceedance Probability", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, 1.0);
            dc.DrawText(xAxisLabel, new Point(origin.X + (plotWidth - xAxisLabel.Width) / 2.0, origin.Y + 28));

            var yAxisLabel = new FormattedText("Damage ($)", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, 1.0);
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


        private static byte[] CreateWaterDemandChartImage(List<(string Name, List<(double X, double Y)> Points, Color Color)> series)
        {
            const double width = 420;
            const double height = 260;
            const double marginLeft = 70;
            const double marginRight = 30;
            const double marginTop = 55;
            const double marginBottom = 70;

            var allPoints = series.SelectMany(s => s.Points).ToList();
            if (allPoints.Count == 0)
                return Array.Empty<byte>();

            double minX = allPoints.Min(p => p.X);
            double maxX = allPoints.Max(p => p.X);
            double minY = allPoints.Min(p => p.Y);
            double maxY = allPoints.Max(p => p.Y);
            if (Math.Abs(maxX - minX) < 0.001)
            {
                maxX += 1;
                minX -= 1;
            }
            if (Math.Abs(maxY - minY) < 0.001)
            {
                maxY += 1;
                minY = Math.Max(0, minY - 1);
            }

            double plotWidth = width - marginLeft - marginRight;
            double plotHeight = height - marginTop - marginBottom;

            DrawingVisual dv = new();
            using var dc = dv.RenderOpen();

            var background = new LinearGradientBrush(Color.FromRgb(248, 251, 255), Color.FromRgb(227, 236, 250), new Point(0, 0), new Point(0, 1));
            background.Freeze();
            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));

            var origin = new Point(marginLeft, marginTop + plotHeight);
            var axisPen = new Pen(new SolidColorBrush(ChartGray), 1.0);
            axisPen.Freeze();
            dc.DrawLine(axisPen, origin, new Point(origin.X + plotWidth, origin.Y));
            dc.DrawLine(axisPen, origin, new Point(origin.X, marginTop));

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(80, ChartGray.R, ChartGray.G, ChartGray.B)), 0.6)
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

            var typeface = new Typeface("Segoe UI");
            var axisBrush = new SolidColorBrush(ChartGray);
            axisBrush.Freeze();

            foreach (var seriesEntry in series)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    for (int i = 0; i < seriesEntry.Points.Count; i++)
                    {
                        var (xValue, yValue) = seriesEntry.Points[i];
                        double x = origin.X + (xValue - minX) / (maxX - minX) * plotWidth;
                        double y = origin.Y - (yValue - minY) / (maxY - minY) * plotHeight;
                        if (i == 0)
                            ctx.BeginFigure(new Point(x, y), false, false);
                        else
                            ctx.LineTo(new Point(x, y), true, true);
                    }
                }
                geometry.Freeze();

                var lineBrush = new SolidColorBrush(seriesEntry.Color);
                lineBrush.Freeze();
                var pen = new Pen(lineBrush, 2.2)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                pen.Freeze();
                dc.DrawGeometry(null, pen, geometry);
            }

            int legendRow = 0;
            foreach (var seriesEntry in series)
            {
                var swatch = new Rect(origin.X + plotWidth + 10, marginTop + legendRow * 20, 12, 12);
                var swatchBrush = new SolidColorBrush(seriesEntry.Color);
                swatchBrush.Freeze();
                dc.DrawRectangle(swatchBrush, null, swatch);

                var label = new FormattedText(seriesEntry.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
                dc.DrawText(label, new Point(swatch.Right + 6, swatch.Top - 2));
                legendRow++;
            }

            for (int i = 0; i <= verticalTickCount; i++)
            {
                double year = minX + (maxX - minX) * i / verticalTickCount;
                var label = new FormattedText($"{year:0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
                double x = origin.X + (plotWidth * i / verticalTickCount) - label.Width / 2.0;
                double y = origin.Y + 10;
                dc.DrawText(label, new Point(x, y));
            }

            for (int i = 0; i <= horizontalTickCount; i++)
            {
                double demand = minY + (maxY - minY) * i / horizontalTickCount;
                var label = new FormattedText(FormatNumberLabel(demand), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, axisBrush, 1.0);
                double x = marginLeft - label.Width - 8;
                double y = origin.Y - (plotHeight * i / horizontalTickCount) - label.Height / 2.0;
                dc.DrawText(label, new Point(x, y));
            }

            var title = new FormattedText("Adjusted Demand by Scenario", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, new SolidColorBrush(ChartBlue), 1.0);
            title.SetFontWeight(FontWeights.SemiBold);
            dc.DrawText(title, new Point(marginLeft, marginTop - title.Height - 16));

            var xAxisLabel = new FormattedText("Year", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, 1.0);
            dc.DrawText(xAxisLabel, new Point(origin.X + (plotWidth - xAxisLabel.Width) / 2.0, origin.Y + 28));

            var yAxisLabel = new FormattedText("Adjusted Demand", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, 1.0);
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

            var background = new LinearGradientBrush(Color.FromRgb(248, 251, 255), Color.FromRgb(230, 239, 252), new Point(0, 0), new Point(0, 1));
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

            var typeface = new Typeface("Segoe UI");
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

            var highlightLabel = new FormattedText($"Selected UDV: {udv.UnitDayValue:C2}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, Brushes.White, 1.0);
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

        private static void AddEadChart(IXLWorksheet ws, PointCollection? stagePoints, PointCollection? frequencyPoints, int row, int column)
        {
            if ((stagePoints == null || stagePoints.Count == 0) && (frequencyPoints == null || frequencyPoints.Count == 0))
                return;
            byte[] img = CreateEadChartImage(stagePoints, frequencyPoints);
            using var stream = new MemoryStream(img);
            var pic = ws.AddPicture(stream, XLPictureFormat.Png, "EADChart");
            pic.MoveTo(ws.Cell(row, column));
        }

        private static byte[] CreateEadChartImage(PointCollection? stagePoints, PointCollection? frequencyPoints)
        {
            double width = 300;
            double height = 150;
            DrawingVisual dv = new();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                if (frequencyPoints is { Count: > 0 } freqPoints)
                    DrawPolyline(dc, freqPoints, new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1ABC9C")), 2));
                if (stagePoints is { Count: > 0 } stagePts)
                    DrawPolyline(dc, stagePts, new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D6A8E")), 2));
            }
            RenderTargetBitmap rtb = new((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using MemoryStream ms = new();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static void DrawPolyline(DrawingContext dc, PointCollection points, Pen pen)
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
