using System;
using System.Collections.Generic;
using System.Linq;

namespace EconToolbox.Desktop.Models
{
    /// <summary>
    /// Provides simple water demand forecasting utilities.
    /// Supports linear regression and average growth rate methods.
    /// </summary>
    public static class WaterDemandModel
    {
        /// <summary>
        /// Represents the output of a demand forecast.
        /// </summary>
        public record ForecastResult(List<(int Year, double Demand)> Data, string Explanation);

        /// <summary>
        /// Forecasts future demand using linear regression.
        /// </summary>
        /// <param name="historical">Historical data as year and demand pairs.</param>
        /// <param name="yearsToProject">Number of future years to project.</param>
        /// <returns>Forecast data and an explanation string.</returns>
        public static ForecastResult LinearRegressionForecast(
            List<(int Year, double Demand)> historical,
            int yearsToProject)
        {
            if (historical.Count == 0)
                return new ForecastResult(new List<(int Year, double Demand)>(), "No data provided.");

            // Compute slope and intercept for y = a*x + b
            int n = historical.Count;
            double sumX = historical.Sum(p => (double)p.Year);
            double sumY = historical.Sum(p => p.Demand);
            double sumXY = historical.Sum(p => p.Year * p.Demand);
            double sumX2 = historical.Sum(p => (double)p.Year * p.Year);

            double denominator = n * sumX2 - sumX * sumX;
            double slope = denominator == 0 ? 0 : (n * sumXY - sumX * sumY) / denominator;
            double intercept = (sumY - slope * sumX) / n;

            var result = new List<(int Year, double Demand)>(historical);
            int lastYear = historical[^1].Year;
            for (int i = 1; i <= yearsToProject; i++)
            {
                int year = lastYear + i;
                double demand = slope * year + intercept;
                result.Add((year, demand));
            }

            string explanation = $"Demand = {slope:F2} × Year + {intercept:F2}. Forecast produced using linear regression.";
            return new ForecastResult(result, explanation);
        }

        /// <summary>
        /// Forecasts future demand using the compound annual growth rate
        /// based on the first and last historical observation.
        /// </summary>
        /// <param name="historical">Historical data ordered by year.</param>
        /// <param name="yearsToProject">Number of future years to project.</param>
        /// <returns>Forecast data and an explanation string.</returns>
        public static ForecastResult GrowthRateForecast(
            List<(int Year, double Demand)> historical,
            int yearsToProject,
            double? rateOverride = null)
        {
            var result = new List<(int Year, double Demand)>(historical);
            if (historical.Count == 0)
                return new ForecastResult(result, "No data provided.");

            if (historical.Count == 1)
            {
                if (rateOverride is null)
                    return new ForecastResult(result, "Insufficient data for growth rate forecast.");

                double rate = rateOverride.Value;
                int lastYear = historical[0].Year;
                double previous = historical[0].Demand;
                for (int i = 1; i <= yearsToProject; i++)
                {
                    previous = previous * (1.0 + rate);
                    result.Add((lastYear + i, previous));
                }

                string explanationSingle = $"Forecast produced using a compound annual growth rate of {rate:P2}.";
                return new ForecastResult(result, explanationSingle);
            }

            double first = historical.First().Demand;
            double last = historical.Last().Demand;
            double yearSpan = historical.Last().Year - historical.First().Year;
            if (yearSpan <= 0 || first <= 0)
                return new ForecastResult(result, "Invalid historical data for growth rate forecast.");

            double rateComputed = rateOverride ?? (Math.Pow(last / first, 1.0 / yearSpan) - 1.0);
            int lastYearMulti = historical.Last().Year;
            double previousMulti = last;
            for (int i = 1; i <= yearsToProject; i++)
            {
                previousMulti = previousMulti * (1.0 + rateComputed);
                result.Add((lastYearMulti + i, previousMulti));
            }

            string explanation = $"Forecast produced using a compound annual growth rate of {rateComputed:P2}.";
            return new ForecastResult(result, explanation);
        }

        public static ForecastResult LinearRegressionForecast(int baseYear, double baseDemand, int yearsToProject)
            => LinearRegressionForecast(new List<(int Year, double Demand)> { (baseYear, baseDemand) }, yearsToProject);

        public static ForecastResult GrowthRateForecast(int baseYear, double baseDemand, int yearsToProject, double rate)
            => GrowthRateForecast(new List<(int Year, double Demand)> { (baseYear, baseDemand) }, yearsToProject, rate);
    }
}

