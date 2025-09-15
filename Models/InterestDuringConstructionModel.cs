using System;
using System.Linq;

namespace EconToolbox.Desktop.Models
{
    public static class InterestDuringConstructionModel
    {
        public static double Compute(
            double totalInitialCost,
            double rate,
            int months,
            double[]? costs = null,
            string[]? timings = null,
            int[]? monthIndices = null)
        {
            if (months <= 0)
                return 0.0;

            double monthlyRate = rate / 12.0;

            if (costs == null || costs.Length == 0)
            {
                double monthlyCost = months == 0 ? 0.0 : totalInitialCost / months;
                costs = Enumerable.Repeat(monthlyCost, months).ToArray();

                timings = new string[months];
                monthIndices = new int[months];
                for (int i = 0; i < months; i++)
                {
                    monthIndices[i] = i;
                    timings[i] = i == 0 ? "beginning" : "midpoint";
                }
            }
            else
            {
                timings = NormalizeTimings(timings, costs.Length);
                monthIndices = NormalizeMonths(monthIndices, costs.Length);
            }

            double idc = 0.0;
            for (int i = 0; i < costs.Length; i++)
            {
                double timingOffset = timings![i].ToLowerInvariant() switch
                {
                    "beginning" => 0.0,
                    "end" => 1.0,
                    "middle" => 0.5,
                    "midpoint" => 0.5,
                    _ => 1.0
                };

                int monthIndex = monthIndices![i];
                if (monthIndex < 0)
                    monthIndex = 0;
                if (monthIndex >= months)
                    monthIndex = months - 1;

                double eventMonth = monthIndex + timingOffset;
                double remaining = Math.Max(0.0, months - eventMonth);
                idc += costs[i] * monthlyRate * remaining;
            }

            return idc;
        }

        private static string[] NormalizeTimings(string[]? timings, int length)
        {
            var normalized = new string[length];
            for (int i = 0; i < length; i++)
            {
                string? timing = timings != null && i < timings.Length ? timings[i] : null;
                if (string.IsNullOrWhiteSpace(timing))
                    normalized[i] = "midpoint";
                else
                    normalized[i] = timing.Trim();
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
