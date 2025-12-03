using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EconToolbox.Desktop.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests
{
    [TestClass]
    public class PerformanceBenchmarks
    {
        [TestMethod]
        public void EadModel_Compute_HandlesLargeInputsQuickly()
        {
            int count = 10_000;
            double[] probabilities = Enumerable.Range(0, count)
                .Select(i => 1.0 - (i + 1) / (double)(count + 1))
                .ToArray();
            double[] damages = Enumerable.Range(0, count)
                .Select(i => i * 10.0)
                .ToArray();

            var stopwatch = Stopwatch.StartNew();
            double result = EadModel.Compute(probabilities, damages);
            stopwatch.Stop();

            Assert.IsTrue(result > 0, "EAD result should be positive for increasing damages.");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 250, "EAD computation took longer than expected for 10k rows.");
        }

        [TestMethod]
        public void CropScapeSummary_Mapping_ScalesLinearly()
        {
            var areas = new List<CropScapeClassArea>();
            for (int i = 0; i < 5000; i++)
            {
                areas.Add(new CropScapeClassArea(100 + i, $"Class {i}", 1_000 + i, 10 + i % 5));
            }

            var stopwatch = Stopwatch.StartNew();
            var summaries = CropScapeAcreageSummary.FromAreas(areas, out double totalAcres);
            stopwatch.Stop();

            Assert.AreEqual(areas.Count, summaries.Count, "All areas should produce summaries.");
            Assert.IsTrue(totalAcres > 0, "Total acreage should accumulate across inputs.");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 150, "CropScape summary mapping should remain responsive for thousands of entries.");
        }
    }
}
