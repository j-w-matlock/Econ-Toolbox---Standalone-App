using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.Services
{
    public class FloodImpactAnalysisService
    {
        public FloodImpactAnalysisResult Run(FloodImpactAnalysisRequest request)
        {
            if (request.Events.Count == 0)
            {
                throw new InvalidOperationException("At least one flood event is required.");
            }

            if (request.Crops.Count == 0)
            {
                throw new InvalidOperationException("At least one crop row is required.");
            }

            var random = new Random(request.Settings.RandomSeed);
            var defaultCurve = ParseDepthDamageCurve(request.Settings.DefaultCurve);
            var eventResults = new List<FloodImpactEventResult>();
            double totalEad = 0;
            double totalMean = 0;
            double covAccumulator = 0;
            int validEvents = 0;

            foreach (var floodEvent in request.Events)
            {
                if (floodEvent.ReturnPeriodYears <= 0)
                {
                    continue;
                }

                var damageSamples = new List<double>(request.Settings.MonteCarloRuns * request.Settings.AnalysisYears);
                for (int year = 0; year < request.Settings.AnalysisYears; year++)
                {
                    for (int run = 0; run < request.Settings.MonteCarloRuns; run++)
                    {
                        int sampledMonth = request.Settings.RandomizeFloodMonth ? random.Next(1, 13) : floodEvent.FloodMonth;
                        double totalDamage = 0;

                        foreach (var crop in request.Crops.Where(c => string.Equals(c.EventName, floodEvent.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            var months = ParseMonths(crop.GrowingMonthsCsv);
                            if (months.Count > 0 && !months.Contains(sampledMonth))
                            {
                                continue;
                            }

                            var curve = string.IsNullOrWhiteSpace(crop.SpecificCurve) ? defaultCurve : ParseDepthDamageCurve(crop.SpecificCurve);
                            var baselineDepth = crop.SpatialAverageDepthFeet > 0 ? crop.SpatialAverageDepthFeet : floodEvent.DepthFeet;
                            var depthSample = Math.Max(0, baselineDepth + NextGaussian(random, 0, request.Settings.DepthStdDev));
                            var baseDamage = InterpolateDamage(depthSample, curve);
                            var noisyDamage = Math.Clamp(baseDamage + NextGaussian(random, 0, request.Settings.DamageStdDev), 0, 1);

                            var rawValue = crop.ValuePerAcre > 0 ? crop.ValuePerAcre : request.Settings.DefaultCropValue;
                            var valueSample = Math.Max(0, rawValue * (1 + NextGaussian(random, 0, request.Settings.ValueStdDev)));
                            totalDamage += crop.Acres * valueSample * noisyDamage;
                        }

                        damageSamples.Add(totalDamage);
                    }
                }

                damageSamples.Sort();
                var mean = damageSamples.Average();
                var std = Math.Sqrt(damageSamples.Average(v => Math.Pow(v - mean, 2)));
                var p5 = PercentileFromSorted(damageSamples, 0.05);
                var p95 = PercentileFromSorted(damageSamples, 0.95);
                var discrete = mean / floodEvent.ReturnPeriodYears;

                totalEad += discrete;
                totalMean += mean;
                covAccumulator += mean > 0 ? std / mean : 0;
                validEvents++;

                eventResults.Add(new FloodImpactEventResult(floodEvent.Name, mean, std, p5, p95, discrete, damageSamples.Count));
            }

            var summary = new FloodImpactSummaryResult(
                totalEad,
                totalMean,
                eventResults.Count,
                request.Crops.Count,
                eventResults.Sum(e => e.Samples),
                validEvents > 0 ? covAccumulator / validEvents : 0);

            return new FloodImpactAnalysisResult(eventResults.OrderBy(e => e.EventName).ToList(), summary);
        }

        private static List<(double Depth, double Damage)> ParseDepthDamageCurve(string curveText)
        {
            if (string.IsNullOrWhiteSpace(curveText))
            {
                throw new InvalidOperationException("Default curve cannot be blank.");
            }

            var points = new List<(double Depth, double Damage)>();
            foreach (var token in curveText.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException("Curve format must be depth:damage pairs separated by commas.");
                }

                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var depth)
                    || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var damage))
                {
                    throw new InvalidOperationException("Curve values must be numeric.");
                }

                points.Add((Math.Max(0, depth), Math.Clamp(damage, 0, 1)));
            }

            if (points.Count < 2)
            {
                throw new InvalidOperationException("Curve must have at least two points.");
            }

            points.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            return points;
        }

        private static HashSet<int> ParseMonths(string monthsCsv)
        {
            var months = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(monthsCsv))
            {
                return months;
            }

            foreach (var token in monthsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token.Trim(), out var month) && month >= 1 && month <= 12)
                {
                    months.Add(month);
                }
            }

            return months;
        }

        private static double InterpolateDamage(double depth, List<(double Depth, double Damage)> curve)
        {
            if (depth <= curve[0].Depth)
            {
                return curve[0].Damage;
            }

            for (int i = 1; i < curve.Count; i++)
            {
                if (depth <= curve[i].Depth)
                {
                    var p0 = curve[i - 1];
                    var p1 = curve[i];
                    if (Math.Abs(p1.Depth - p0.Depth) < 1e-9)
                    {
                        return p1.Damage;
                    }

                    var t = (depth - p0.Depth) / (p1.Depth - p0.Depth);
                    return p0.Damage + ((p1.Damage - p0.Damage) * t);
                }
            }

            return curve[^1].Damage;
        }

        private static double NextGaussian(Random random, double mean, double stdDev)
        {
            if (stdDev <= 0)
            {
                return 0;
            }

            var u1 = 1.0 - random.NextDouble();
            var u2 = 1.0 - random.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + (stdDev * randStdNormal);
        }

        private static double PercentileFromSorted(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0;
            }

            percentile = Math.Clamp(percentile, 0, 1);
            var index = percentile * (sortedValues.Count - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);
            if (lower == upper)
            {
                return sortedValues[lower];
            }

            var fraction = index - lower;
            return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction);
        }
    }
}
