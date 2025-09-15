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

namespace EconToolbox.Desktop.Services
{
    public static class ExcelExporter
    {
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
                ws.Cell(1,7).GetComment().AddText("Adjusted = Demand × (1 - Improvements %) × (1 - Losses %)");
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

        public static void ExportAll(EadViewModel ead, UpdatedCostViewModel updated, AnnualizerViewModel annualizer, WaterDemandViewModel waterDemand, UdvViewModel udv, string filePath)
        {
            using var wb = new XLWorkbook();

            // EAD Sheet
            var eadSheet = wb.Worksheets.Add("EAD");
            int col = 1;
            eadSheet.Cell(1, col++).Value = "Probability";
            if (ead.UseStage)
                eadSheet.Cell(1, col++).Value = "Stage";
            int dcCount = ead.DamageColumns.Count;
            foreach (var name in ead.DamageColumns)
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
                wdSheet.Cell(1,4).GetComment().AddText("Adjusted = Demand × (1 - Improvements %) × (1 - Losses %)");
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

            wb.SaveAs(filePath);
        }

        private static void AddEadChart(IXLWorksheet ws, PointCollection stagePoints, PointCollection frequencyPoints, int row, int column)
        {
            if ((stagePoints == null || stagePoints.Count == 0) && (frequencyPoints == null || frequencyPoints.Count == 0))
                return;
            byte[] img = CreateEadChartImage(stagePoints, frequencyPoints);
            using var stream = new MemoryStream(img);
            var pic = ws.AddPicture(stream, XLPictureFormat.Png, "EADChart");
            pic.MoveTo(ws.Cell(row, column));
        }

        private static byte[] CreateEadChartImage(PointCollection stagePoints, PointCollection frequencyPoints)
        {
            double width = 300;
            double height = 150;
            DrawingVisual dv = new();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                if (frequencyPoints != null && frequencyPoints.Count > 0)
                    DrawPolyline(dc, frequencyPoints, new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1ABC9C")), 2));
                if (stagePoints != null && stagePoints.Count > 0)
                    DrawPolyline(dc, stagePoints, new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D6A8E")), 2));
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
