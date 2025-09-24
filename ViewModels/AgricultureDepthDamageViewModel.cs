using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;

namespace EconToolbox.Desktop.ViewModels
{
    public class AgricultureDepthDamageViewModel : BaseViewModel
    {
        private readonly RelayCommand _removeRowCommand;
        private readonly RelayCommand _computeCommand;

        public ObservableCollection<CustomRegionRow> CustomRegionRows { get; } = new();

        public ObservableCollection<string> MonteCarloInsights { get; } = new();

        public ObservableCollection<ReturnPeriodInsight> ReturnPeriodInsights { get; } = new();

        private string _monteCarloSummary = "Add at least two probability-depth rows and press Calculate to generate insights.";
        public string MonteCarloSummary
        {
            get => _monteCarloSummary;
            private set { _monteCarloSummary = value; OnPropertyChanged(); }
        }

        private PointCollection _depthDamagePoints = new();
        public PointCollection DepthDamagePoints
        {
            get => _depthDamagePoints;
            private set { _depthDamagePoints = value; OnPropertyChanged(); }
        }

        public ICommand AddRowCommand { get; }

        public ICommand RemoveRowCommand => _removeRowCommand;

        public ICommand ComputeCommand => _computeCommand;

        public AgricultureDepthDamageViewModel()
        {
            AddRowCommand = new RelayCommand(AddRow);
            _removeRowCommand = new RelayCommand(RemoveRow, () => CustomRegionRows.Count > 1);
            _computeCommand = new RelayCommand(Compute, CanCompute);

            CustomRegionRows.CollectionChanged += CustomRegionRows_CollectionChanged;

            // Seed with typical agricultural depth-damage points
            CustomRegionRows.Add(new CustomRegionRow
            {
                AnnualExceedanceProbability = 0.5,
                FloodDepthFeet = 0.0,
                DamagePercent = 0.0
            });
            CustomRegionRows.Add(new CustomRegionRow
            {
                AnnualExceedanceProbability = 0.1,
                FloodDepthFeet = 1.5,
                DamagePercent = 25.0
            });
            CustomRegionRows.Add(new CustomRegionRow
            {
                AnnualExceedanceProbability = 0.02,
                FloodDepthFeet = 3.5,
                DamagePercent = 75.0
            });

            Compute();
        }

        private void CustomRegionRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (CustomRegionRow row in e.OldItems)
                {
                    row.PropertyChanged -= Row_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (CustomRegionRow row in e.NewItems)
                {
                    row.PropertyChanged += Row_PropertyChanged;
                }
            }

            _removeRowCommand.RaiseCanExecuteChanged();
            _computeCommand.RaiseCanExecuteChanged();
            MonteCarloSummary = "Inputs updated. Press Calculate to refresh the Monte Carlo results.";
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _computeCommand.RaiseCanExecuteChanged();
            MonteCarloSummary = "Inputs updated. Press Calculate to refresh the Monte Carlo results.";
        }

        private void AddRow()
        {
            if (CustomRegionRows.Count == 0)
            {
                CustomRegionRows.Add(new CustomRegionRow
                {
                    AnnualExceedanceProbability = 0.5,
                    FloodDepthFeet = 0,
                    DamagePercent = 0
                });
                return;
            }

            var lastRow = CustomRegionRows[^1];

            double nextProbability = Math.Max(Math.Round(lastRow.AnnualExceedanceProbability / 2, 3), 0.001);
            double nextDepth = Math.Round(lastRow.FloodDepthFeet + 1.0, 2);
            double nextDamage = Math.Min(100.0, Math.Round(lastRow.DamagePercent + 15.0, 2));

            CustomRegionRows.Add(new CustomRegionRow
            {
                AnnualExceedanceProbability = nextProbability,
                FloodDepthFeet = nextDepth,
                DamagePercent = nextDamage
            });
        }

        private void RemoveRow()
        {
            if (CustomRegionRows.Count <= 1)
            {
                return;
            }

            CustomRegionRows.RemoveAt(CustomRegionRows.Count - 1);
        }

        private bool CanCompute()
        {
            return TryGetValidRows(out var _);
        }

        private bool TryGetValidRows(out List<CustomRegionRow> rows)
        {
            rows = CustomRegionRows
                .Where(r => double.IsFinite(r.AnnualExceedanceProbability)
                    && double.IsFinite(r.FloodDepthFeet)
                    && double.IsFinite(r.DamagePercent))
                .Where(r => r.AnnualExceedanceProbability >= 0 && r.AnnualExceedanceProbability <= 1)
                .Where(r => r.FloodDepthFeet >= 0 && r.DamagePercent >= 0)
                .OrderByDescending(r => r.AnnualExceedanceProbability)
                .ToList();

            return rows.Count >= 2;
        }

        private void Compute()
        {
            if (!TryGetValidRows(out var rows))
            {
                MonteCarloSummary = "Enter at least two rows with valid annual exceedance probabilities to run the simulation.";
                MonteCarloInsights.Clear();
                DepthDamagePoints = new PointCollection();
                ReturnPeriodInsights.Clear();
                return;
            }

            UpdateDepthDamagePoints(rows);

            var damageCurve = BuildProbabilityCurve(rows, r => r.DamagePercent, anchorValue: 0.0, anchorMaxValue: rows.Max(r => r.DamagePercent));
            var depthCurve = BuildProbabilityCurve(rows, r => r.FloodDepthFeet, anchorValue: 0.0, anchorMaxValue: rows.Max(r => r.FloodDepthFeet));

            RunMonteCarlo(damageCurve, depthCurve, rows.Count);
            UpdateReturnPeriodInsights(damageCurve, depthCurve);
        }

        private void UpdateDepthDamagePoints(IReadOnlyCollection<CustomRegionRow> rows)
        {
            var ordered = rows
                .Where(r => r.FloodDepthFeet >= 0 && r.DamagePercent >= 0)
                .OrderBy(r => r.FloodDepthFeet)
                .ToList();

            if (ordered.Count == 0)
            {
                DepthDamagePoints = new PointCollection();
                return;
            }

            // Ensure the curve starts at zero depth / zero damage
            if (ordered[0].FloodDepthFeet > 0)
            {
                ordered.Insert(0, new CustomRegionRow
                {
                    FloodDepthFeet = 0,
                    DamagePercent = 0,
                    AnnualExceedanceProbability = ordered[0].AnnualExceedanceProbability
                });
            }

            // Ensure the curve extends to the maximum depth provided
            double maxDepth = ordered.Max(r => r.FloodDepthFeet);
            double maxDamage = ordered.Max(r => r.DamagePercent);

            PointCollection points = CreatePointCollection(
                ordered.Select(r => (r.FloodDepthFeet, r.DamagePercent)).ToList(),
                maxDepth,
                maxDamage);
            DepthDamagePoints = points;
        }

        private static PointCollection CreatePointCollection(List<(double X, double Y)> data, double maxDepth, double maxDamage)
        {
            PointCollection points = new();
            if (data.Count == 0)
            {
                return points;
            }

            double width = 360;
            double height = 200;

            double minX = data.Min(p => p.X);
            double maxX = Math.Max(data.Max(p => p.X), Math.Max(minX + 1e-6, maxDepth));
            double minY = data.Min(p => p.Y);
            double maxY = Math.Max(data.Max(p => p.Y), Math.Max(minY + 1e-6, maxDamage));

            double xRange = maxX - minX;
            if (xRange <= 0) xRange = 1;
            double yRange = maxY - minY;
            if (yRange <= 0) yRange = 1;

            foreach (var (xValue, yValue) in data)
            {
                double x = (xValue - minX) / xRange * width;
                double y = height - (yValue - minY) / yRange * height;
                points.Add(new Point(x, y));
            }

            return points;
        }

        private void RunMonteCarlo(
            List<(double Probability, double Value)> damageCurve,
            List<(double Probability, double Value)> depthCurve,
            int rowCount)
        {
            int iterations = 5000;
            List<double> damageSamples = new(iterations);
            List<double> depthSamples = new(iterations);

            Random random = new();
            for (int i = 0; i < iterations; i++)
            {
                double exceedance = random.NextDouble();
                double damage = Interpolate(exceedance, damageCurve);
                double depth = Interpolate(exceedance, depthCurve);
                damageSamples.Add(damage);
                depthSamples.Add(depth);
            }

            damageSamples.Sort();
            depthSamples.Sort();

            double meanDamage = damageSamples.Average();
            double medianDamage = GetPercentile(damageSamples, 0.5);
            double p90Damage = GetPercentile(damageSamples, 0.9);
            double meanDepth = depthSamples.Average();
            double p90Depth = GetPercentile(depthSamples, 0.9);

            MonteCarloSummary =
                $"Simulated {iterations:N0} annual events using {rowCount} calibration points and linear interpolation across the supplied AEP curve.";
            MonteCarloInsights.Clear();
            MonteCarloInsights.Add($"Mean damage: {meanDamage:0.##}%");
            MonteCarloInsights.Add($"Median damage: {medianDamage:0.##}%");
            MonteCarloInsights.Add($"90th percentile damage: {p90Damage:0.##}%");
            MonteCarloInsights.Add($"Average inundation depth: {meanDepth:0.##} ft");
            MonteCarloInsights.Add($"90th percentile depth: {p90Depth:0.##} ft");
        }

        private void UpdateReturnPeriodInsights(
            List<(double Probability, double Value)> damageCurve,
            List<(double Probability, double Value)> depthCurve)
        {
            ReturnPeriodInsights.Clear();

            if (damageCurve.Count == 0 || depthCurve.Count == 0)
            {
                return;
            }

            foreach (var (probability, label) in ReturnPeriodInsight.DefaultCheckpoints)
            {
                double damage = Interpolate(probability, damageCurve);
                double depth = Interpolate(probability, depthCurve);

                ReturnPeriodInsights.Add(new ReturnPeriodInsight(probability, damage, depth, label));
            }
        }

        private static List<(double Probability, double Value)> BuildProbabilityCurve(
            IEnumerable<CustomRegionRow> rows,
            Func<CustomRegionRow, double> selector,
            double anchorValue,
            double anchorMaxValue)
        {
            List<(double Probability, double Value)> curve = rows
                .Select(r => (Probability: ClampProbability(r.AnnualExceedanceProbability), Value: selector(r)))
                .OrderByDescending(p => p.Probability)
                .ToList();

            if (curve.Count == 0)
            {
                return curve;
            }

            if (curve[0].Probability < 1.0)
            {
                curve.Insert(0, (1.0, anchorValue));
            }

            var last = curve[^1];
            if (last.Probability > 0)
            {
                curve.Add((0.0, Math.Max(last.Value, anchorMaxValue)));
            }

            return curve;
        }

        private static double Interpolate(double probability, List<(double Probability, double Value)> curve)
        {
            if (curve.Count == 0)
            {
                return 0.0;
            }

            probability = ClampProbability(probability);

            for (int i = 0; i < curve.Count - 1; i++)
            {
                var (p1, v1) = curve[i];
                var (p2, v2) = curve[i + 1];
                if (Math.Abs(p1 - p2) < 1e-9)
                {
                    continue;
                }

                if (probability <= p1 && probability >= p2)
                {
                    double t = (probability - p2) / (p1 - p2);
                    return v2 + t * (v1 - v2);
                }
            }

            return curve[^1].Value;
        }

        private static double GetPercentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0.0;
            }

            percentile = Math.Clamp(percentile, 0, 1);
            double index = (sortedValues.Count - 1) * percentile;
            int lowerIndex = (int)Math.Floor(index);
            int upperIndex = (int)Math.Ceiling(index);
            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            double fraction = index - lowerIndex;
            return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * fraction;
        }

        private static double ClampProbability(double value)
        {
            if (double.IsNaN(value))
            {
                return 0.0;
            }

            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        public class CustomRegionRow : BaseViewModel
        {
            private double _annualExceedanceProbability;
            public double AnnualExceedanceProbability
            {
                get => _annualExceedanceProbability;
                set
                {
                    if (Math.Abs(_annualExceedanceProbability - value) < 1e-9) return;
                    _annualExceedanceProbability = value;
                    OnPropertyChanged();
                }
            }

            private double _floodDepthFeet;
            public double FloodDepthFeet
            {
                get => _floodDepthFeet;
                set
                {
                    if (Math.Abs(_floodDepthFeet - value) < 1e-9) return;
                    _floodDepthFeet = value;
                    OnPropertyChanged();
                }
            }

            private double _damagePercent;
            public double DamagePercent
            {
                get => _damagePercent;
                set
                {
                    if (Math.Abs(_damagePercent - value) < 1e-9) return;
                    _damagePercent = value;
                    OnPropertyChanged();
                }
            }
        }

        public class ReturnPeriodInsight
        {
            public static readonly (double Probability, string Label)[] DefaultCheckpoints = new[]
            {
                (0.5, "2-year"),
                (0.2, "5-year"),
                (0.1, "10-year"),
                (0.04, "25-year"),
                (0.02, "50-year"),
                (0.01, "100-year")
            };

            public ReturnPeriodInsight(double probability, double damagePercent, double floodDepthFeet, string label)
            {
                Probability = ClampProbability(probability);
                DamagePercent = Math.Max(0, damagePercent);
                FloodDepthFeet = Math.Max(0, floodDepthFeet);
                Label = label;
            }

            public double Probability { get; }

            public double DamagePercent { get; }

            public double FloodDepthFeet { get; }

            public string Label { get; }

            public string ProbabilityDisplay => Probability.ToString("P0");

            public string DamageDisplay => $"{DamagePercent:0.##}%";

            public string DepthDisplay => $"{FloodDepthFeet:0.##} ft";
        }
    }
}
