using System;
using System.Collections.Generic;

namespace EconToolbox.Desktop.Models
{
    public static class AnnualizerModel
    {
        public record Result(double Idc, double TotalInvestment, double Crf, double AnnualCost, double Bcr);

        public static Result Compute(
            double firstCost,
            double rate,
            double annualOm,
            double annualBenefits,
            IEnumerable<(double cost, int year)>? futureCosts = null,
            int analysisPeriod = 1,
            int constructionMonths = 12,
            double[]? idcCosts = null,
            string[]? idcTimings = null,
            int[]? idcMonths = null)
        {
            double idc = ComputeIdc(firstCost, rate, constructionMonths, idcCosts, idcTimings, idcMonths);

            double pvFuture = 0.0;
            if (futureCosts != null)
            {
                foreach (var (cost, year) in futureCosts)
                {
                    double pvFactor = 1.0 / Math.Pow(1.0 + rate, year);
                    pvFuture += cost * pvFactor;
                }
            }

            double totalInvestment = firstCost + idc + pvFuture;
            int normalizedAnalysisPeriod = analysisPeriod <= 0 ? 1 : analysisPeriod;
            double crf = CapitalRecoveryModel.Calculate(rate, normalizedAnalysisPeriod);
            double annualConstruction = totalInvestment * crf;
            double annualCost = annualConstruction + annualOm;
            double bcr = annualCost == 0 ? double.NaN : annualBenefits / annualCost;

            return new Result(idc, totalInvestment, crf, annualCost, bcr);
        }

        private static double ComputeIdc(
            double totalInitialCost,
            double rate,
            int months,
            double[]? costs,
            string[]? timings,
            int[]? monthIndices)
        {
            if (months <= 0)
                return 0.0;

            double monthlyRate = rate / 12.0;

            double[] scheduleCosts;
            string[] scheduleTimings;
            int[] scheduleMonths;

            if (costs == null || costs.Length == 0)
            {
                scheduleCosts = new double[months];
                scheduleTimings = new string[months];
                scheduleMonths = new int[months];

                double monthlyCost = totalInitialCost / months;
                for (int i = 0; i < months; i++)
                {
                    scheduleCosts[i] = monthlyCost;
                    scheduleMonths[i] = i;
                    scheduleTimings[i] = i == 0 ? "beginning" : "midpoint";
                }
            }
            else
            {
                scheduleCosts = costs;
                scheduleTimings = NormalizeTimings(timings, costs.Length);
                scheduleMonths = NormalizeMonths(monthIndices, costs.Length);
            }

            double idc = 0.0;
            for (int i = 0; i < scheduleCosts.Length; i++)
            {
                double timingOffset = scheduleTimings[i].ToLowerInvariant() switch
                {
                    "beginning" => 0.0,
                    "end" => 1.0,
                    "middle" => 0.5,
                    "midpoint" => 0.5,
                    _ => 1.0
                };

                int monthIndex = scheduleMonths[i];
                if (monthIndex < 0)
                    monthIndex = 0;
                if (monthIndex >= months)
                    monthIndex = months - 1;

                double eventMonth = monthIndex + timingOffset;
                double remaining = Math.Max(0.0, months - eventMonth);
                idc += scheduleCosts[i] * monthlyRate * remaining;
            }

            return idc;
        }

        private static string[] NormalizeTimings(string[]? timings, int length)
        {
            var normalized = new string[length];
            for (int i = 0; i < length; i++)
            {
                string? timing = timings != null && i < timings.Length ? timings[i] : null;
                normalized[i] = string.IsNullOrWhiteSpace(timing) ? "midpoint" : timing.Trim();
            }

            return normalized;
        }

        private static int[] NormalizeMonths(int[]? monthIndices, int length)
        {
            var normalized = new int[length];
            if (monthIndices == null || monthIndices.Length == 0)
            {
                for (int i = 0; i < length; i++)
                    normalized[i] = i;
                return normalized;
            }

            for (int i = 0; i < length; i++)
            {
                int month = i < monthIndices.Length ? monthIndices[i] : monthIndices[^1];
                normalized[i] = month < 0 ? 0 : month;
            }

            return normalized;
        }
    }
}

