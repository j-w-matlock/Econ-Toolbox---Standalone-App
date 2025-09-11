using System.Collections.Generic;
using ClosedXML.Excel;
using EconToolbox.Desktop.Models;
using System.Linq;

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
            int row = 2;
            foreach(var d in data)
            {
                ws.Cell(row,1).Value = d.Year;
                ws.Cell(row,2).Value = d.Demand;
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
    }
}
