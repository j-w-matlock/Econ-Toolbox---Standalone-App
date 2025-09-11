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
        /// Forecasts future demand using linear regression.
        /// </summary>
        /// <param name="historical">Historical data as year and demand pairs.</param>
        /// <param name="yearsToProject">Number of future years to project.</param>
        /// <returns>List containing historical and forecasted demand.</returns>
        public static List<(int Year, double Demand)> LinearRegressionForecast(
            List<(int Year, double Demand)> historical,
            int yearsToProject)
        {
            if (historical.Count == 0)
                return new List<(int Year, double Demand)>();

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
            return result;
        }

        /// <summary>
        /// Forecasts future demand using the compound annual growth rate
        /// based on the first and last historical observation.
        /// </summary>
        /// <param name="historical">Historical data ordered by year.</param>
        /// <param name="yearsToProject">Number of future years to project.</param>
        /// <returns>List containing historical and forecasted demand.</returns>
        public static List<(int Year, double Demand)> GrowthRateForecast(
            List<(int Year, double Demand)> historical,
            int yearsToProject)
        {
            var result = new List<(int Year, double Demand)>(historical);
            if (historical.Count < 2)
                return result;

            double first = historical.First().Demand;
            double last = historical.Last().Demand;
            double yearSpan = historical.Last().Year - historical.First().Year;
            if (yearSpan <= 0 || first <= 0)
                return result;

            double rate = Math.Pow(last / first, 1.0 / yearSpan) - 1.0;
            int lastYear = historical.Last().Year;
            double previous = last;
            for (int i = 1; i <= yearsToProject; i++)
            {
                previous = previous * (1.0 + rate);
                result.Add((lastYear + i, previous));
            }
            return result;
        }
    }
}

