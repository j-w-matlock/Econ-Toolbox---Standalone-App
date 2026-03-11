using System.Linq;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests
{
    [TestClass]
    public class FloodImpactAnalysisServiceTests
    {
        [TestMethod]
        public void Run_WithDeterministicInputs_ReturnsExpectedSummary()
        {
            var service = new FloodImpactAnalysisService();
            var request = new FloodImpactAnalysisRequest(
                new[] { new FloodEventInput("10-year", 2.0, 6, 10) },
                new[] { new CropImpactInput(1, "Corn", "10-year", 100, 1000, "6", "0:0,1:0.5,2:1", 2.0) },
                new FloodImpactUncertaintySettings("0:0,1:0.5,2:1", 1000, 0, 0, 0, 2, 2, 42, false));

            var result = service.Run(request);

            Assert.AreEqual(1, result.Events.Count);
            Assert.AreEqual(100000, result.Events[0].MeanDamage, 1e-6);
            Assert.AreEqual(10000, result.Events[0].DiscreteEadContribution, 1e-6);
            Assert.AreEqual(10000, result.Summary.TotalDiscreteEad, 1e-6);
            Assert.AreEqual(4, result.Summary.Samples);
        }

        [TestMethod]
        public void Run_IgnoresCropOutsideGrowingMonth()
        {
            var service = new FloodImpactAnalysisService();
            var request = new FloodImpactAnalysisRequest(
                new[] { new FloodEventInput("spring", 2.0, 3, 5) },
                new[] { new CropImpactInput(1, "Corn", "spring", 100, 1000, "7,8", "0:0,1:0.5,2:1", 2.0) },
                new FloodImpactUncertaintySettings("0:0,1:0.5,2:1", 1000, 0, 0, 0, 1, 1, 1, false));

            var result = service.Run(request);

            Assert.AreEqual(0, result.Events.Single().MeanDamage, 1e-6);
            Assert.AreEqual(0, result.Summary.TotalDiscreteEad, 1e-6);
        }
    }
}
