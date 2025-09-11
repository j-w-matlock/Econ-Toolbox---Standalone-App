using System.Collections.Generic;
using ClosedXML.Excel;
using EconToolbox.Desktop.Models;
using System.Linq;
using EconToolbox.Desktop.ViewModels;

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

        public static void ExportWaterDemand(IEnumerable<DemandEntry> data, string filePath)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("WaterDemand");
            ws.Cell(1,1).Value = "Year";
            ws.Cell(1,2).Value = "Demand";
            ws.Cell(1,3).Value = "Industrial";
            ws.Cell(1,4).Value = "Adjusted";
            int row = 2;
            foreach(var d in data)
            {
                ws.Cell(row,1).Value = d.Year;
                ws.Cell(row,2).Value = d.Demand;
                ws.Cell(row,3).Value = d.IndustrialDemand;
                ws.Cell(row,4).Value = d.AdjustedDemand;
                row++;
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
                summary.Cell(row,2).Value = kv.Value;
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

        public static void ExportEad(IEnumerable<EadViewModel.EadRow> rows, IEnumerable<string> damageColumns, bool useStage, string result, string filePath)
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
                annSummary.Cell(rowIdx, 2).Value = kv.Value;
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
            var wdSheet = wb.Worksheets.Add("WaterDemand");
            wdSheet.Cell(1,1).Value = "Year";
            wdSheet.Cell(1,2).Value = "Demand";
            wdSheet.Cell(1,3).Value = "Industrial";
            wdSheet.Cell(1,4).Value = "Adjusted";
            rowIdx = 2;
            foreach (var d in waterDemand.Results)
            {
                wdSheet.Cell(rowIdx,1).Value = d.Year;
                wdSheet.Cell(rowIdx,2).Value = d.Demand;
                wdSheet.Cell(rowIdx,3).Value = d.IndustrialDemand;
                wdSheet.Cell(rowIdx,4).Value = d.AdjustedDemand;
                rowIdx++;
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
                ucSummary.Cell(rowIdx,2).Value = kv.Value;
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
    }
}
