using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests
{
    [TestClass]
    public class ExcelNamingPolicyTests
    {
        [TestMethod]
        public void WorksheetNameEnforcesPolicy()
        {
            var workbook = new XLWorkbook();
            var logs = new List<string>();
            string longName = new string('X', 80);

            string first = ExcelNamingPolicy.CreateWorksheetName(workbook, longName, logs.Add);
            workbook.Worksheets.Add(first);
            string second = ExcelNamingPolicy.CreateWorksheetName(workbook, longName, logs.Add);

            Assert.IsTrue(first.Length <= ExcelNamingPolicy.ExcelMaxWorksheetNameLength);
            Assert.IsTrue(second.Length <= ExcelNamingPolicy.ExcelMaxWorksheetNameLength);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(logs.Count > 0);
        }

        [TestMethod]
        public void TableNameRemovesInvalidCharacters()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var logs = new List<string>();
            string name = ExcelNamingPolicy.CreateUniqueName("?table with spaces&symbols", used, ExcelNamingPolicy.ExcelMaxTableNameLength, "Tbl", true, logs.Add);

            Assert.IsTrue(name.StartsWith("Tbl", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(name.All(c => char.IsLetterOrDigit(c) || c == '_'));
            Assert.IsTrue(logs.Count > 0);
        }

        [TestMethod]
        public void ExportWaterDemandCreatesSanitizedSheets()
        {
            var service = new ExcelExportService();
            var scenarios = new[]
            {
                new Scenario
                {
                    Name = new string('S', 70),
                    Results =
                    {
                        new DemandEntry { Year = 2023, GrowthRate = 0.01, Demand = 10, ResidentialDemand = 4, CommercialDemand = 3, IndustrialDemand = 2, AgriculturalDemand = 1, AdjustedDemand = 11 },
                        new DemandEntry { Year = 2024, GrowthRate = 0.02, Demand = 12, ResidentialDemand = 4.5, CommercialDemand = 3.5, IndustrialDemand = 2.5, AgriculturalDemand = 1.5, AdjustedDemand = 13 }
                    }
                },
                new Scenario
                {
                    Name = "Name:With/Invalid*Chars",
                    Description = "extra info",
                    Results =
                    {
                        new DemandEntry { Year = 2023, GrowthRate = 0.01, Demand = 8, ResidentialDemand = 3, CommercialDemand = 2, IndustrialDemand = 2, AgriculturalDemand = 1, AdjustedDemand = 9 },
                        new DemandEntry { Year = 2024, GrowthRate = 0.015, Demand = 9, ResidentialDemand = 3.5, CommercialDemand = 2.5, IndustrialDemand = 2.5, AgriculturalDemand = 1.5, AdjustedDemand = 10 }
                    }
                }
            };

            string path = Path.Combine(Path.GetTempPath(), $"water_{Guid.NewGuid():N}.xlsx");
            try
            {
                service.ExportWaterDemand(scenarios, path);
                using var workbook = new XLWorkbook(path);
                Assert.AreEqual(2, workbook.Worksheets.Count);
                Assert.IsTrue(workbook.Worksheets.All(ws => ws.Name.Length <= ExcelNamingPolicy.ExcelMaxWorksheetNameLength));
                CollectionAssert.AllItemsAreUnique(workbook.Worksheets.Select(ws => ws.Name).ToList());
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
