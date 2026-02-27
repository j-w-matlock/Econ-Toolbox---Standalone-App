using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels
{
    public sealed class UncertaintyStatisticsViewModel : DiagnosticViewModelBase, IComputeModule
    {
        private const string NoFilterOption = "(None)";

        private static readonly CultureInfo[] NumericCultures =
        {
            CultureInfo.InvariantCulture,
            CultureInfo.CurrentCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("en-GB"),
            CultureInfo.GetCultureInfo("fr-FR"),
            CultureInfo.GetCultureInfo("de-DE")
        };

        private readonly List<ShapefileRecord> _records = new();
        private readonly Dictionary<string, List<double>> _numericAttributeValues = new(StringComparer.OrdinalIgnoreCase);

        private string _statusMessage = "Load a shapefile to analyze uncertainty statistics for structure-related attributes.";
        private string _selectedCategory = "Structure Value";
        private string? _selectedAttribute;
        private string _selectedRefinementAttribute = NoFilterOption;
        private string? _selectedRefinementValue;
        private string _selectedStatisticalTest = "Auto (Recommended)";
        private string _recommendedStatisticalTest = "Auto (Recommended)";
        private string _nullHypothesisValue = "0";
        private string _shapefilePath = string.Empty;
        private bool _ignoreZeroValues;
        private bool _ignoreNegativeValues;
        private bool _excludeIqrOutliers;

        public ObservableCollection<string> CategoryOptions { get; } = new(new[]
        {
            "Structure Value",
            "Content Value",
            "First Floor Elevation"
        });

        public ObservableCollection<string> AvailableAttributes { get; } = new();

        public ObservableCollection<string> RefinementAttributeOptions { get; } = new();

        public ObservableCollection<string> RefinementValueOptions { get; } = new();

        public ObservableCollection<string> StatisticalTestOptions { get; } = new(new[]
        {
            "Auto (Recommended)",
            "One-Sample Z-Test (Mean)",
            "One-Sample Sign Test (Median)"
        });

        public ObservableCollection<AttributeStatistic> Statistics { get; } = new();

        public ObservableCollection<SurveyPopulationItem> SurveyPopulation { get; } = new();

        public IRelayCommand LoadShapefileCommand { get; }

        public System.Windows.Input.ICommand ComputeCommand { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    AutoSelectAttributeForCategory();
                    RefreshDiagnostics();
                }
            }
        }

        public string? SelectedAttribute
        {
            get => _selectedAttribute;
            set
            {
                if (SetProperty(ref _selectedAttribute, value))
                {
                    UpdateRefinementAttributeOptions();
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                    (ComputeCommand as RelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        public string SelectedRefinementAttribute
        {
            get => _selectedRefinementAttribute;
            set
            {
                if (SetProperty(ref _selectedRefinementAttribute, value))
                {
                    UpdateRefinementValueOptions();
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                }
            }
        }

        public string? SelectedRefinementValue
        {
            get => _selectedRefinementValue;
            set
            {
                if (SetProperty(ref _selectedRefinementValue, value))
                {
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                }
            }
        }

        public string SelectedStatisticalTest
        {
            get => _selectedStatisticalTest;
            set
            {
                if (SetProperty(ref _selectedStatisticalTest, value))
                {
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                }
            }
        }

        public string RecommendedStatisticalTest
        {
            get => _recommendedStatisticalTest;
            private set => SetProperty(ref _recommendedStatisticalTest, value);
        }

        public string NullHypothesisValue
        {
            get => _nullHypothesisValue;
            set
            {
                if (SetProperty(ref _nullHypothesisValue, value))
                {
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                }
            }
        }

        public bool IgnoreZeroValues
        {
            get => _ignoreZeroValues;
            set
            {
                if (SetProperty(ref _ignoreZeroValues, value))
                {
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                }
            }
        }

        public bool IgnoreNegativeValues
        {
            get => _ignoreNegativeValues;
            set
            {
                if (SetProperty(ref _ignoreNegativeValues, value))
                {
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                }
            }
        }

        public bool ExcludeIqrOutliers
        {
            get => _excludeIqrOutliers;
            set
            {
                if (SetProperty(ref _excludeIqrOutliers, value))
                {
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                }
            }
        }

        public string ShapefilePath
        {
            get => _shapefilePath;
            private set
            {
                if (SetProperty(ref _shapefilePath, value))
                {
                    RefreshDiagnostics();
                }
            }
        }

        public UncertaintyStatisticsViewModel()
        {
            RefinementAttributeOptions.Add(NoFilterOption);
            LoadShapefileCommand = new RelayCommand(LoadShapefile);
            ComputeCommand = new RelayCommand(ComputeStatistics, CanComputeStatistics);
            RefreshDiagnostics();
        }

        private bool CanComputeStatistics()
        {
            return !string.IsNullOrWhiteSpace(SelectedAttribute) && _records.Count > 0;
        }

        private void LoadShapefile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Shapefile (*.shp)|*.shp|All files (*.*)|*.*",
                Title = "Select shapefile for uncertainty analysis"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var dbfPath = Path.ChangeExtension(dialog.FileName, ".dbf");
                if (!File.Exists(dbfPath))
                {
                    StatusMessage = "The selected shapefile does not have a matching .dbf attribute table.";
                    return;
                }

                var loadedRecords = LoadRecordsFromDbf(dbfPath);
                _records.Clear();
                _records.AddRange(loadedRecords);

                RebuildNumericAttributeValues();
                AvailableAttributes.Clear();
                foreach (var field in _numericAttributeValues.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    AvailableAttributes.Add(field);
                }

                ShapefilePath = dialog.FileName;
                AutoSelectAttributeForCategory();

                if (AvailableAttributes.Count == 0)
                {
                    StatusMessage = "No numeric attributes were found in the shapefile table.";
                }
                else
                {
                    StatusMessage = $"Loaded {_records.Count} row(s) and {AvailableAttributes.Count} numeric attribute(s) from {Path.GetFileName(dialog.FileName)}.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load shapefile attributes: {ex.Message}";
            }
            finally
            {
                (ComputeCommand as RelayCommand)?.NotifyCanExecuteChanged();
                RefreshDiagnostics();
            }
        }

        private void RebuildNumericAttributeValues()
        {
            _numericAttributeValues.Clear();

            foreach (var record in _records)
            {
                foreach (var pair in record.NumericValues)
                {
                    if (!_numericAttributeValues.TryGetValue(pair.Key, out var values))
                    {
                        values = new List<double>();
                        _numericAttributeValues[pair.Key] = values;
                    }

                    values.Add(pair.Value);
                }
            }
        }

        private void AutoSelectAttributeForCategory()
        {
            if (AvailableAttributes.Count == 0)
            {
                SelectedAttribute = null;
                return;
            }

            var preferredTokens = SelectedCategory switch
            {
                "Structure Value" => new[] { "struct", "structure", "bldg", "value" },
                "Content Value" => new[] { "content", "cont", "value" },
                "First Floor Elevation" => new[] { "ffe", "first", "floor", "elev", "elevation" },
                _ => Array.Empty<string>()
            };

            var bestMatch = AvailableAttributes
                .Select(field => new
                {
                    Field = field,
                    Score = preferredTokens.Count(token => field.Contains(token, StringComparison.OrdinalIgnoreCase))
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Field, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            SelectedAttribute = bestMatch?.Field ?? AvailableAttributes.FirstOrDefault();
        }

        private void UpdateRefinementAttributeOptions()
        {
            RefinementAttributeOptions.Clear();
            RefinementAttributeOptions.Add(NoFilterOption);

            foreach (var candidate in _records.SelectMany(r => r.RawValues.Keys)
                         .Where(k => !string.Equals(k, SelectedAttribute, StringComparison.OrdinalIgnoreCase))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                RefinementAttributeOptions.Add(candidate);
            }

            if (!RefinementAttributeOptions.Contains(SelectedRefinementAttribute))
            {
                SelectedRefinementAttribute = NoFilterOption;
            }
            else
            {
                UpdateRefinementValueOptions();
            }
        }

        private void UpdateRefinementValueOptions()
        {
            RefinementValueOptions.Clear();
            SelectedRefinementValue = null;

            if (SelectedRefinementAttribute == NoFilterOption)
            {
                return;
            }

            var values = _records
                .Select(r => r.RawValues.TryGetValue(SelectedRefinementAttribute, out var raw) ? raw : string.Empty)
                .Where(raw => !string.IsNullOrWhiteSpace(raw))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(raw => raw, StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToList();

            foreach (var value in values)
            {
                RefinementValueOptions.Add(value);
            }

            SelectedRefinementValue = RefinementValueOptions.FirstOrDefault();
        }

        private void ComputeStatistics()
        {
            if (SelectedAttribute == null)
            {
                StatusMessage = "Select an attribute with numeric values before calculating.";
                return;
            }

            var candidateRecords = GetRecordsForCurrentSelection(SelectedAttribute).ToList();
            var candidateValues = candidateRecords
                .Select(r => r.NumericValues.TryGetValue(SelectedAttribute, out var value) ? (double?)value : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (candidateValues.Count == 0)
            {
                Statistics.Clear();
                SurveyPopulation.Clear();
                StatusMessage = "No numeric values remain after applying the selected attribute refinement.";
                RefreshDiagnostics();
                return;
            }

            var filteredValues = ApplyStatisticalFilters(candidateValues, out var excludedByFilter, out var outlierThresholdDescription);
            if (filteredValues.Count == 0)
            {
                Statistics.Clear();
                SurveyPopulation.Clear();
                StatusMessage = "All values were removed by the active statistical filters. Adjust options and try again.";
                RefreshDiagnostics();
                return;
            }

            var ordered = filteredValues.OrderBy(v => v).ToArray();
            var count = ordered.Length;
            var mean = ordered.Average();
            var min = ordered.First();
            var max = ordered.Last();
            var variancePopulation = ordered.Sum(value => Math.Pow(value - mean, 2)) / count;
            var stdPopulation = Math.Sqrt(variancePopulation);
            var varianceSample = count > 1 ? ordered.Sum(value => Math.Pow(value - mean, 2)) / (count - 1) : 0;
            var stdSample = Math.Sqrt(varianceSample);
            var coefficientOfVariation = Math.Abs(mean) > double.Epsilon ? stdPopulation / Math.Abs(mean) : 0;
            var median = Percentile(ordered, 0.5);
            var q1 = Percentile(ordered, 0.25);
            var q3 = Percentile(ordered, 0.75);
            var skewness = stdPopulation > 0 ? ordered.Sum(v => Math.Pow((v - mean) / stdPopulation, 3)) / count : 0;

            RecommendedStatisticalTest = RecommendTest(count, skewness);
            var chosenTest = SelectedStatisticalTest == "Auto (Recommended)" ? RecommendedStatisticalTest : SelectedStatisticalTest;
            var nullHypothesis = TryParseNumericText(NullHypothesisValue, out var parsedNull) ? parsedNull : 0;
            var (testStatistic, pValue, testInterpretation) = RunStatisticalTest(chosenTest, ordered, mean, median, stdSample, nullHypothesis);

            var representativeSampleSize = ComputeRepresentativeSampleSize(candidateRecords.Count);
            BuildSurveyPopulation(candidateRecords, representativeSampleSize);

            Statistics.Clear();
            AddStatistic("Selected Category", SelectedCategory, "The uncertainty category currently selected in the module. Statistics are computed only from records that belong to this category.");
            AddStatistic("Selected Attribute", SelectedAttribute, "The numeric DBF/shapefile attribute field used as input values for all calculations shown below.");
            AddStatistic("Refinement Attribute", SelectedRefinementAttribute, "Optional secondary attribute used to subset the shapefile records before calculating statistics.");
            AddStatistic("Refinement Value", SelectedRefinementValue ?? "(All)", "Specific value selected within the refinement attribute. Only matching records are retained.");
            AddStatistic("Observation Count", count.ToString(CultureInfo.InvariantCulture), "Number of values included in the final computation after all enabled filters are applied.");
            AddStatistic("Excluded by Filters", excludedByFilter.ToString(CultureInfo.InvariantCulture), "Count of candidate numeric values removed by active statistical filters (zero-value filter, negative-value filter, and/or IQR outlier filter).");
            AddStatistic("Ignore Zero Values", IgnoreZeroValues ? "Yes" : "No", "Indicates whether exact zero values were removed before computing summary statistics.");
            AddStatistic("Ignore Negative Values", IgnoreNegativeValues ? "Yes" : "No", "Indicates whether values less than 0 were removed before computing summary statistics.");
            AddStatistic("Exclude IQR Outliers", ExcludeIqrOutliers ? "Yes" : "No", "Indicates whether Tukey IQR outlier removal was applied using bounds [Q1 - 1.5*IQR, Q3 + 1.5*IQR].");
            if (!string.IsNullOrWhiteSpace(outlierThresholdDescription))
            {
                AddStatistic("IQR Outlier Threshold", outlierThresholdDescription, "Accepted range for outlier filtering computed as lower = Q1 - 1.5*IQR and upper = Q3 + 1.5*IQR, where IQR = Q3 - Q1.");
            }

            AddStatistic("Recommended Statistical Test", RecommendedStatisticalTest, "Module recommendation based on sample size and distribution skewness.");
            AddStatistic("Applied Statistical Test", chosenTest, "Test actually executed. When Auto is selected, the recommended test is applied.");
            AddStatistic("Null Hypothesis Value", FormatNumber(nullHypothesis), "Reference mean/median value used by the selected test.");
            AddStatistic("Test Statistic", FormatNumber(testStatistic), "Numeric test statistic produced by the selected test.");
            AddStatistic("Two-Tailed p-value", pValue.ToString("N6", CultureInfo.InvariantCulture), "Probability of observing a test statistic at least this extreme under the null hypothesis.");
            AddStatistic("Test Interpretation", testInterpretation, "Interpretation using α = 0.05. Reject H0 when p-value < 0.05.");
            AddStatistic("Representative Sample Size", representativeSampleSize.ToString(CultureInfo.InvariantCulture), "Suggested survey sample size using Cochran's finite-population correction (95% confidence, ±5% margin, p=0.5).");
            AddStatistic("Population Records for Survey", candidateRecords.Count.ToString(CultureInfo.InvariantCulture), "Count of records in the refined population from which random survey members are drawn.");

            AddStatistic("Minimum", FormatNumber(min), "Smallest value in the filtered dataset: min(x).");
            AddStatistic("Maximum", FormatNumber(max), "Largest value in the filtered dataset: max(x).");
            AddStatistic("Range", FormatNumber(max - min), "Overall spread of values computed as Range = Maximum - Minimum.");
            AddStatistic("Mean", FormatNumber(mean), "Arithmetic average computed as Mean = (Σxᵢ) / n.");
            AddStatistic("Median (P50)", FormatNumber(median), "50th percentile of sorted values (middle value, or average of the two middle positions via percentile interpolation).");
            AddStatistic("First Quartile (P25)", FormatNumber(q1), "25th percentile (Q1) of sorted values using linear interpolation between neighboring ranks.");
            AddStatistic("Third Quartile (P75)", FormatNumber(q3), "75th percentile (Q3) of sorted values using linear interpolation between neighboring ranks.");
            AddStatistic("Population Variance", FormatNumber(variancePopulation), "Second central moment for the filtered values treated as the full population: σ² = (Σ(xᵢ - μ)²) / n.");
            AddStatistic("Population Standard Deviation", FormatNumber(stdPopulation), "Square root of population variance: σ = √[(Σ(xᵢ - μ)²) / n].");
            AddStatistic("Sample Standard Deviation", FormatNumber(stdSample), "Unbiased sample spread estimate using Bessel's correction: s = √[(Σ(xᵢ - x̄)²) / (n - 1)].");
            AddStatistic("Coefficient of Variation", coefficientOfVariation.ToString("P2", CultureInfo.InvariantCulture), "Relative dispersion measured as CV = (Population Standard Deviation / |Mean|) × 100%. Returns 0 when mean is near zero.");
            AddStatistic("Skewness", FormatNumber(skewness), "Asymmetry of the distribution computed from the standardized third central moment. Positive values indicate right-skew; negative values indicate left-skew.");

            StatusMessage = $"Computed statistics/test results for '{SelectedAttribute}' using {count} record(s). Suggested survey sample: {representativeSampleSize}.";
            RefreshDiagnostics();
        }

        private IEnumerable<ShapefileRecord> GetRecordsForCurrentSelection(string numericAttribute)
        {
            return _records.Where(record =>
            {
                if (!record.NumericValues.ContainsKey(numericAttribute))
                {
                    return false;
                }

                if (SelectedRefinementAttribute == NoFilterOption || string.IsNullOrWhiteSpace(SelectedRefinementValue))
                {
                    return true;
                }

                return record.RawValues.TryGetValue(SelectedRefinementAttribute, out var value) &&
                       string.Equals(value, SelectedRefinementValue, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void BuildSurveyPopulation(IReadOnlyList<ShapefileRecord> candidateRecords, int representativeSampleSize)
        {
            SurveyPopulation.Clear();
            if (candidateRecords.Count == 0 || representativeSampleSize <= 0)
            {
                return;
            }

            var shuffled = candidateRecords.OrderBy(_ => Random.Shared.Next()).Take(representativeSampleSize).ToList();
            foreach (var record in shuffled)
            {
                SurveyPopulation.Add(new SurveyPopulationItem(record.RowNumber, record.Identifier));
            }
        }

        private static int ComputeRepresentativeSampleSize(int populationSize)
        {
            if (populationSize <= 0)
            {
                return 0;
            }

            const double z = 1.96;
            const double p = 0.5;
            const double e = 0.05;
            var n0 = (z * z * p * (1 - p)) / (e * e);
            var n = n0 / (1 + ((n0 - 1) / populationSize));
            return Math.Min(populationSize, (int)Math.Ceiling(n));
        }

        private static (double Statistic, double PValue, string Interpretation) RunStatisticalTest(string testName, IReadOnlyList<double> ordered, double mean, double median, double sampleStd, double nullHypothesis)
        {
            return testName switch
            {
                "One-Sample Sign Test (Median)" => RunSignTest(ordered, nullHypothesis),
                _ => RunZTest(mean, sampleStd, ordered.Count, nullHypothesis)
            };
        }

        private static (double Statistic, double PValue, string Interpretation) RunZTest(double mean, double sampleStd, int count, double nullHypothesis)
        {
            if (count <= 1 || sampleStd <= double.Epsilon)
            {
                return (0, 1, "Insufficient variability to evaluate the z-test.");
            }

            var z = (mean - nullHypothesis) / (sampleStd / Math.Sqrt(count));
            var p = 2 * (1 - NormalCdf(Math.Abs(z)));
            var interpretation = p < 0.05
                ? "Reject H0 at α=0.05 (mean differs from null value)."
                : "Fail to reject H0 at α=0.05.";
            return (z, p, interpretation);
        }

        private static (double Statistic, double PValue, string Interpretation) RunSignTest(IReadOnlyList<double> values, double nullHypothesis)
        {
            var positives = values.Count(v => v > nullHypothesis);
            var negatives = values.Count(v => v < nullHypothesis);
            var n = positives + negatives;
            if (n == 0)
            {
                return (0, 1, "All observations equal the null median; sign test is inconclusive.");
            }

            var k = Math.Min(positives, negatives);
            var cumulative = 0.0;
            for (var i = 0; i <= k; i++)
            {
                cumulative += BinomialProbability(n, i, 0.5);
            }

            var p = Math.Min(1.0, 2 * cumulative);
            var interpretation = p < 0.05
                ? "Reject H0 at α=0.05 (median differs from null value)."
                : "Fail to reject H0 at α=0.05.";
            return (positives - negatives, p, interpretation);
        }

        private static double BinomialProbability(int n, int k, double p)
        {
            var logCoeff = LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k);
            return Math.Exp(logCoeff + (k * Math.Log(p)) + ((n - k) * Math.Log(1 - p)));
        }

        private static double LogFactorial(int n)
        {
            double result = 0;
            for (var i = 2; i <= n; i++)
            {
                result += Math.Log(i);
            }

            return result;
        }

        private static double NormalCdf(double value)
        {
            return 0.5 * (1 + Erf(value / Math.Sqrt(2)));
        }

        private static double Erf(double x)
        {
            var sign = Math.Sign(x);
            x = Math.Abs(x);

            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;

            var t = 1.0 / (1.0 + (p * x));
            var y = 1.0 - (((((a5 * t) + a4) * t + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
            return sign * y;
        }

        private static string RecommendTest(int count, double skewness)
        {
            return count >= 30 && Math.Abs(skewness) <= 1
                ? "One-Sample Z-Test (Mean)"
                : "One-Sample Sign Test (Median)";
        }

        private void TryAutoComputeStatistics()
        {
            if (CanComputeStatistics())
            {
                ComputeStatistics();
            }
            else
            {
                Statistics.Clear();
                SurveyPopulation.Clear();
            }
        }

        private List<double> ApplyStatisticalFilters(List<double> values, out int excludedCount, out string outlierThresholdDescription)
        {
            var filtered = values.AsEnumerable();

            if (IgnoreZeroValues)
            {
                filtered = filtered.Where(v => Math.Abs(v) > double.Epsilon);
            }

            if (IgnoreNegativeValues)
            {
                filtered = filtered.Where(v => v >= 0);
            }

            outlierThresholdDescription = string.Empty;
            var postBasicFilters = filtered.ToList();
            if (ExcludeIqrOutliers && postBasicFilters.Count >= 4)
            {
                var sorted = postBasicFilters.OrderBy(v => v).ToArray();
                var q1 = Percentile(sorted, 0.25);
                var q3 = Percentile(sorted, 0.75);
                var iqr = q3 - q1;
                var lowerFence = q1 - (1.5 * iqr);
                var upperFence = q3 + (1.5 * iqr);
                outlierThresholdDescription = $"[{FormatNumber(lowerFence)}, {FormatNumber(upperFence)}]";
                postBasicFilters = postBasicFilters.Where(v => v >= lowerFence && v <= upperFence).ToList();
            }

            excludedCount = values.Count - postBasicFilters.Count;
            return postBasicFilters;
        }

        protected override IEnumerable<DiagnosticItem> BuildDiagnostics()
        {
            yield return new DiagnosticItem(
                DiagnosticLevel.Advisory,
                "EM 1110-2-1619 context",
                "Use Structure Value, Content Value, and First Floor Elevation attributes to characterize uncertainty in economic inputs for flood risk management studies.");

            yield return new DiagnosticItem(
                DiagnosticLevel.Info,
                "Input source",
                string.IsNullOrWhiteSpace(ShapefilePath) ? "No shapefile selected." : $"Loaded: {Path.GetFileName(ShapefilePath)}");

            yield return new DiagnosticItem(
                DiagnosticLevel.Info,
                "Selected attribute",
                string.IsNullOrWhiteSpace(SelectedAttribute) ? "Choose a numeric field from the shapefile table." : SelectedAttribute);

            yield return new DiagnosticItem(
                DiagnosticLevel.Info,
                "Refinement",
                SelectedRefinementAttribute == NoFilterOption ? "No additional attribute refinement selected." : $"{SelectedRefinementAttribute} = {SelectedRefinementValue ?? "(not set)"}");

            yield return new DiagnosticItem(
                DiagnosticLevel.Info,
                "Recommended test",
                RecommendedStatisticalTest);
        }

        private static List<ShapefileRecord> LoadRecordsFromDbf(string dbfPath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var stream = File.OpenRead(dbfPath);
            using var reader = new BinaryReader(stream);

            _ = reader.ReadByte();
            _ = reader.ReadBytes(3);
            _ = reader.ReadInt32();
            var headerLength = reader.ReadInt16();
            var recordLength = reader.ReadInt16();
            _ = reader.ReadBytes(12);
            var languageDriverId = reader.ReadByte();
            _ = reader.ReadBytes(7);

            var encoding = ResolveDbfEncoding(dbfPath, languageDriverId);

            var descriptors = new List<DbfFieldDescriptor>();
            while (true)
            {
                var first = reader.ReadByte();
                if (first == 0x0D)
                {
                    break;
                }

                var descriptorBytes = new byte[32];
                descriptorBytes[0] = first;
                var remaining = reader.ReadBytes(31);
                Array.Copy(remaining, 0, descriptorBytes, 1, remaining.Length);
                descriptors.Add(ParseDescriptor(descriptorBytes));
            }

            stream.Seek(headerLength, SeekOrigin.Begin);
            var records = new List<ShapefileRecord>();
            var rowNumber = 1;
            while (stream.Position + recordLength <= stream.Length)
            {
                var deletionFlag = reader.ReadByte();
                if (deletionFlag == 0x2A)
                {
                    _ = reader.ReadBytes(recordLength - 1);
                    continue;
                }

                var rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var numericValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                foreach (var descriptor in descriptors)
                {
                    var raw = reader.ReadBytes(descriptor.Length);
                    var text = encoding.GetString(raw).Trim();
                    rawValues[descriptor.Name] = text;

                    if (descriptor.IsNumeric && TryParseNumericText(text, out var value))
                    {
                        numericValues[descriptor.Name] = value;
                    }
                }

                var identifier = ResolveIdentifier(rawValues, rowNumber);
                records.Add(new ShapefileRecord(rowNumber, identifier, rawValues, numericValues));
                rowNumber++;
            }

            return records;
        }

        private static string ResolveIdentifier(IReadOnlyDictionary<string, string> values, int rowNumber)
        {
            var preferredFields = new[] { "id", "fid", "objectid", "name", "parcel", "address" };
            foreach (var field in values.Keys)
            {
                if (preferredFields.Any(token => field.Contains(token, StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(values[field]))
                {
                    return values[field];
                }
            }

            return $"Row {rowNumber}";
        }

        private static bool TryParseNumericText(string text, out double value)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                value = 0;
                return false;
            }

            var normalized = text.Trim();
            foreach (var culture in NumericCultures)
            {
                if (double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, culture, out value))
                {
                    return true;
                }
            }

            normalized = normalized.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
                .Replace("'", string.Empty, StringComparison.Ordinal);

            foreach (var candidate in BuildNumericCandidates(normalized))
            {
                if (double.TryParse(candidate, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private static IEnumerable<string> BuildNumericCandidates(string text)
        {
            yield return text;

            if (text.Contains(',', StringComparison.Ordinal) && !text.Contains('.', StringComparison.Ordinal))
            {
                yield return text.Replace(',', '.');
            }

            if (text.Contains('.', StringComparison.Ordinal) && text.Contains(',', StringComparison.Ordinal))
            {
                var commaAsDecimal = text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
                var dotAsDecimal = text.Replace(",", string.Empty, StringComparison.Ordinal);
                yield return commaAsDecimal;
                yield return dotAsDecimal;
            }
        }

        private static Encoding ResolveDbfEncoding(string dbfPath, byte languageDriverId)
        {
            var cpgEncoding = TryReadCpgEncoding(dbfPath);
            if (cpgEncoding != null)
            {
                return cpgEncoding;
            }

            var byLanguageDriver = TryMapLanguageDriver(languageDriverId);
            if (byLanguageDriver != null)
            {
                return byLanguageDriver;
            }

            return Encoding.GetEncoding(1252);
        }

        private static Encoding? TryReadCpgEncoding(string dbfPath)
        {
            var cpgPath = Path.ChangeExtension(dbfPath, ".cpg");
            if (!File.Exists(cpgPath))
            {
                return null;
            }

            var cpgText = File.ReadAllText(cpgPath).Trim();
            if (string.IsNullOrWhiteSpace(cpgText))
            {
                return null;
            }

            try
            {
                return Encoding.GetEncoding(cpgText);
            }
            catch (ArgumentException)
            {
                var digits = Regex.Match(cpgText, "\\d+").Value;
                if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var codePage))
                {
                    try
                    {
                        return Encoding.GetEncoding(codePage);
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                }

                return null;
            }
        }

        private static Encoding? TryMapLanguageDriver(byte languageDriverId)
        {
            return languageDriverId switch
            {
                0x01 => Encoding.GetEncoding(437),
                0x02 => Encoding.GetEncoding(850),
                0x03 => Encoding.GetEncoding(1252),
                0x57 => Encoding.GetEncoding(1252),
                0x64 => Encoding.GetEncoding(852),
                0x65 => Encoding.GetEncoding(866),
                0x66 => Encoding.GetEncoding(865),
                0x67 => Encoding.GetEncoding(861),
                0x6A => Encoding.GetEncoding(737),
                0x6B => Encoding.GetEncoding(857),
                0x78 => Encoding.GetEncoding(950),
                0x79 => Encoding.GetEncoding(949),
                0x7A => Encoding.GetEncoding(936),
                0x7B => Encoding.GetEncoding(932),
                0x7C => Encoding.GetEncoding(874),
                0x86 => Encoding.GetEncoding(737),
                0x87 => Encoding.GetEncoding(852),
                0x88 => Encoding.GetEncoding(857),
                0xC8 => Encoding.GetEncoding(1250),
                0xC9 => Encoding.GetEncoding(1251),
                0xCA => Encoding.GetEncoding(1254),
                0xCB => Encoding.GetEncoding(1253),
                0xCC => Encoding.GetEncoding(1257),
                0x00 => Encoding.GetEncoding(1252),
                _ => null
            };
        }

        private static DbfFieldDescriptor ParseDescriptor(byte[] bytes)
        {
            var name = Encoding.ASCII.GetString(bytes, 0, 11).TrimEnd('\0', ' ');
            var type = (char)bytes[11];
            var length = bytes[16];
            return new DbfFieldDescriptor(name, type, length);
        }

        private static double Percentile(IReadOnlyList<double> sorted, double percentile)
        {
            if (sorted.Count == 0)
            {
                return 0;
            }

            var position = (sorted.Count - 1) * percentile;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);

            if (lower == upper)
            {
                return sorted[lower];
            }

            var weight = position - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * weight;
        }

        private void AddStatistic(string metric, string value, string? valueTooltip = null)
        {
            Statistics.Add(new AttributeStatistic(metric, value, valueTooltip));
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("N4", CultureInfo.InvariantCulture);
        }

        private readonly record struct DbfFieldDescriptor(string Name, char Type, int Length)
        {
            public bool IsNumeric => Type is 'N' or 'F' or 'B' or 'I' or 'Y';
        }

        private readonly record struct ShapefileRecord(
            int RowNumber,
            string Identifier,
            IReadOnlyDictionary<string, string> RawValues,
            IReadOnlyDictionary<string, double> NumericValues);
    }
}
