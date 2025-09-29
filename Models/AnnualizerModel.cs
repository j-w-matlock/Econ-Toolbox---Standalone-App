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
            IEnumerable<(double cost, double yearOffset, double timingOffset)>? futureCosts = null,
            int analysisPeriod = 1,
            int baseYear = 0,
            int constructionMonths = 12,
            double[]? idcCosts = null,
            string[]? idcTimings = null,
            int[]? idcMonths = null,
            string? defaultIdcTiming = null,
            bool calculateInterestAtPeriod = false,
            string? firstPaymentTiming = null,
            string? lastPaymentTiming = null)
        {
            double idc = ComputeIdc(firstCost, rate, constructionMonths, idcCosts, idcTimings, idcMonths, defaultIdcTiming,
                calculateInterestAtPeriod, firstPaymentTiming, lastPaymentTiming);

            double pvFuture = 0.0;
            if (futureCosts != null)
            {
                foreach (var (cost, yearOffset, timingOffset) in futureCosts)
                {
                    double exponent = yearOffset + timingOffset;
                    double pvFactor = Math.Pow(1.0 + rate, -exponent);
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
            int[]? monthIndices,
            string? defaultTiming,
            bool calculateAtPeriod,
            string? firstPaymentTiming,
            string? lastPaymentTiming)
        {
            if (months <= 0)
                return 0.0;

            double monthlyRate = rate / 12.0;

            double[] scheduleCosts;
            string[] scheduleTimings;
            int[] scheduleMonths;

            string normalizedDefaultTiming = NormalizeTiming(defaultTiming);
            string defaultTimingForSchedule = calculateAtPeriod ? "beginning" : normalizedDefaultTiming;

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
                    scheduleTimings[i] = defaultTimingForSchedule;
                }
            }
            else
            {
                scheduleCosts = costs;
                scheduleTimings = NormalizeTimings(timings, costs.Length, defaultTimingForSchedule);
                scheduleMonths = NormalizeMonths(monthIndices, costs.Length);
            }

            return calculateAtPeriod
                ? ComputePeriodBasedIdc(scheduleCosts, scheduleTimings, scheduleMonths, months, monthlyRate,
                    firstPaymentTiming, lastPaymentTiming)
                : ComputeMonthlyIdc(scheduleCosts, scheduleTimings, scheduleMonths, months, monthlyRate);
        }

        private static double ComputeMonthlyIdc(double[] costs, string[] timings, int[] months, int totalMonths, double monthlyRate)
        {
            double idc = 0.0;
            for (int i = 0; i < costs.Length; i++)
            {
                double timingOffset = GetTimingOffset(timings[i]);

                int monthIndex = ClampMonthIndex(months[i], totalMonths);
                double eventMonth = monthIndex + timingOffset;
                double remaining = Math.Max(0.0, totalMonths - eventMonth);
                idc += costs[i] * monthlyRate * remaining;
            }

            return idc;
        }

        private static double ComputePeriodBasedIdc(
            double[] costs,
            string[] timings,
            int[] months,
            int totalMonths,
            double monthlyRate,
            string? firstPaymentTiming,
            string? lastPaymentTiming)
        {
            if (costs.Length == 0)
                return 0.0;

            double[] eventTimes = new double[costs.Length];
            for (int i = 0; i < costs.Length; i++)
            {
                int monthIndex = ClampMonthIndex(months[i], totalMonths);
                double timingOffset = GetTimingOffset(timings[i]);
                eventTimes[i] = monthIndex + timingOffset;
            }

            int firstIndex = 0;
            int lastIndex = 0;
            for (int i = 1; i < eventTimes.Length; i++)
            {
                if (eventTimes[i] < eventTimes[firstIndex])
                    firstIndex = i;
                if (eventTimes[i] > eventTimes[lastIndex])
                    lastIndex = i;
            }

            int clampedFirstMonth = ClampMonthIndex(months[firstIndex], totalMonths);
            double normalizedFirstOffset = NormalizeFirstPayment(firstPaymentTiming);
            eventTimes[firstIndex] = clampedFirstMonth + normalizedFirstOffset;

            int clampedLastMonth = ClampMonthIndex(months[lastIndex], totalMonths);
            int maxConstructionMonth = totalMonths > 0 ? totalMonths - 1 : 0;
            clampedLastMonth = Math.Max(clampedLastMonth, maxConstructionMonth);
            double normalizedLastOffset = NormalizeLastPayment(lastPaymentTiming);
            double lastBoundary = Math.Max(eventTimes[lastIndex], clampedLastMonth + normalizedLastOffset);

            double idc = 0.0;
            for (int i = 0; i < costs.Length; i++)
            {
                double eventTime = eventTimes[i];
                double remaining = Math.Max(0.0, lastBoundary - eventTime);
                idc += costs[i] * monthlyRate * remaining;
            }

            return idc;
        }

        private static double GetTimingOffset(string? timing)
        {
            return NormalizeTiming(timing) switch
            {
                "beginning" => 0.0,
                "end" => 1.0,
                "middle" => 0.5,
                "midpoint" => 0.5,
                _ => 1.0
            };
        }

        private static int ClampMonthIndex(int monthIndex, int totalMonths)
        {
            if (monthIndex < 0)
                return 0;
            if (totalMonths <= 0)
                return 0;
            if (monthIndex >= totalMonths)
                return totalMonths - 1;
            return monthIndex;
        }

        private static double NormalizeFirstPayment(string? timing)
        {
            return NormalizeTiming(timing) switch
            {
                "end" => 1.0,
                _ => 0.0
            };
        }

        private static double NormalizeLastPayment(string? timing)
        {
            return NormalizeTiming(timing) switch
            {
                "beginning" => 0.0,
                "end" => 1.0,
                _ => 0.5
            };
        }

        private static string NormalizeTiming(string? timing)
        {
            if (string.IsNullOrWhiteSpace(timing))
                return "midpoint";

            string normalized = timing.Trim().ToLowerInvariant();
            return normalized switch
            {
                "middle" => "midpoint",
                "beginning" => "beginning",
                "end" => "end",
                "midpoint" => "midpoint",
                _ => "midpoint"
            };
        }

        private static string[] NormalizeTimings(string[]? timings, int length, string fallback)
        {
            var normalized = new string[length];
            for (int i = 0; i < length; i++)
            {
                string? timing = timings != null && i < timings.Length ? timings[i] : null;
                normalized[i] = string.IsNullOrWhiteSpace(timing) ? fallback : timing.Trim();
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

