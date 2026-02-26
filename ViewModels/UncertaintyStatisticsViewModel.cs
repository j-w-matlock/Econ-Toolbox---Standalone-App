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
        private static readonly CultureInfo[] NumericCultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.CurrentCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("en-GB"),
            CultureInfo.GetCultureInfo("fr-FR"),
            CultureInfo.GetCultureInfo("de-DE")
        };

        private readonly Dictionary<string, List<double>> _numericAttributeValues = new(StringComparer.OrdinalIgnoreCase);
        private string _statusMessage = "Load a shapefile to analyze uncertainty statistics for structure-related attributes.";
        private string _selectedCategory = "Structure Value";
        private string? _selectedAttribute;
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

        public ObservableCollection<AttributeStatistic> Statistics { get; } = new();

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
                    TryAutoComputeStatistics();
                    RefreshDiagnostics();
                    (ComputeCommand as RelayCommand)?.NotifyCanExecuteChanged();
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
            LoadShapefileCommand = new RelayCommand(LoadShapefile);
            ComputeCommand = new RelayCommand(ComputeStatistics, CanComputeStatistics);
            RefreshDiagnostics();
        }

        private bool CanComputeStatistics()
        {
            return !string.IsNullOrWhiteSpace(SelectedAttribute) &&
                   _numericAttributeValues.TryGetValue(SelectedAttribute!, out var values) &&
                   values.Count > 0;
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

                var parsedAttributes = LoadNumericAttributesFromDbf(dbfPath);
                _numericAttributeValues.Clear();
                AvailableAttributes.Clear();
                Statistics.Clear();

                foreach (var pair in parsedAttributes.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    _numericAttributeValues[pair.Key] = pair.Value;
                    AvailableAttributes.Add(pair.Key);
                }

                ShapefilePath = dialog.FileName;
                AutoSelectAttributeForCategory();

                if (AvailableAttributes.Count == 0)
                {
                    StatusMessage = "No numeric attributes were found in the shapefile table.";
                }
                else
                {
                    StatusMessage = $"Loaded {AvailableAttributes.Count} numeric attribute(s) from {Path.GetFileName(dialog.FileName)}.";
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

        private void ComputeStatistics()
        {
            if (SelectedAttribute == null ||
                !_numericAttributeValues.TryGetValue(SelectedAttribute, out var values) ||
                values.Count == 0)
            {
                StatusMessage = "Select an attribute with numeric values before calculating.";
                return;
            }

            var filteredValues = ApplyStatisticalFilters(values, out var excludedByFilter, out var outlierThresholdDescription);
            if (filteredValues.Count == 0)
            {
                Statistics.Clear();
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
            var varianceSample = count > 1
                ? ordered.Sum(value => Math.Pow(value - mean, 2)) / (count - 1)
                : 0;
            var stdSample = Math.Sqrt(varianceSample);
            var coefficientOfVariation = Math.Abs(mean) > double.Epsilon
                ? stdPopulation / Math.Abs(mean)
                : 0;
            var median = Percentile(ordered, 0.5);
            var q1 = Percentile(ordered, 0.25);
            var q3 = Percentile(ordered, 0.75);
            var skewness = stdPopulation > 0
                ? ordered.Sum(v => Math.Pow((v - mean) / stdPopulation, 3)) / count
                : 0;

            Statistics.Clear();
            AddStatistic(
                "Selected Category",
                SelectedCategory,
                "The uncertainty category currently selected in the module. Statistics are computed only from records that belong to this category.");
            AddStatistic(
                "Selected Attribute",
                SelectedAttribute,
                "The numeric DBF/shapefile attribute field used as input values for all calculations shown below.");
            AddStatistic(
                "Observation Count",
                count.ToString(CultureInfo.InvariantCulture),
                "Number of values included in the final computation after all enabled filters are applied.");
            AddStatistic(
                "Excluded by Filters",
                excludedByFilter.ToString(CultureInfo.InvariantCulture),
                "Count of candidate numeric values removed by active statistical filters (zero-value filter, negative-value filter, and/or IQR outlier filter).");
            AddStatistic(
                "Ignore Zero Values",
                IgnoreZeroValues ? "Yes" : "No",
                "Indicates whether exact zero values were removed before computing summary statistics.");
            AddStatistic(
                "Ignore Negative Values",
                IgnoreNegativeValues ? "Yes" : "No",
                "Indicates whether values less than 0 were removed before computing summary statistics.");
            AddStatistic(
                "Exclude IQR Outliers",
                ExcludeIqrOutliers ? "Yes" : "No",
                "Indicates whether Tukey IQR outlier removal was applied using bounds [Q1 - 1.5*IQR, Q3 + 1.5*IQR].");
            if (!string.IsNullOrWhiteSpace(outlierThresholdDescription))
            {
                AddStatistic(
                    "IQR Outlier Threshold",
                    outlierThresholdDescription,
                    "Accepted range for outlier filtering computed as lower = Q1 - 1.5*IQR and upper = Q3 + 1.5*IQR, where IQR = Q3 - Q1.");
            }
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
            AddStatistic("Coefficient of Variation", coefficientOfVariation.ToString("P2", CultureInfo.InvariantCulture), "Relative dispersion measured as CV = (Sample Standard Deviation / Mean) × 100%. Returns 0 when mean is near zero.");
            AddStatistic("Skewness", FormatNumber(skewness), "Asymmetry of the distribution computed from the standardized third central moment. Positive values indicate right-skew; negative values indicate left-skew.");

            StatusMessage = $"Computed uncertainty statistics for '{SelectedAttribute}' using {count} record(s) after filtering {excludedByFilter} record(s).";
            RefreshDiagnostics();
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
                string.IsNullOrWhiteSpace(ShapefilePath)
                    ? "No shapefile selected."
                    : $"Loaded: {Path.GetFileName(ShapefilePath)}");

            yield return new DiagnosticItem(
                DiagnosticLevel.Info,
                "Selected attribute",
                string.IsNullOrWhiteSpace(SelectedAttribute)
                    ? "Choose a numeric field from the shapefile table."
                    : SelectedAttribute);

            yield return new DiagnosticItem(
                DiagnosticLevel.Info,
                "Statistical filters",
                $"Ignore zeros: {(IgnoreZeroValues ? "On" : "Off")}, Ignore negatives: {(IgnoreNegativeValues ? "On" : "Off")}, Exclude IQR outliers: {(ExcludeIqrOutliers ? "On" : "Off")}");

            if (Statistics.Count > 0)
            {
                var cv = Statistics.FirstOrDefault(item => item.Metric == "Coefficient of Variation")?.Value ?? "n/a";
                yield return new DiagnosticItem(
                    DiagnosticLevel.Info,
                    "Key uncertainty indicator",
                    $"Coefficient of variation: {cv}");
            }
        }

        private static Dictionary<string, List<double>> LoadNumericAttributesFromDbf(string dbfPath)
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
            var results = descriptors.ToDictionary(d => d.Name, _ => new List<double>(), StringComparer.OrdinalIgnoreCase);

            while (stream.Position + recordLength <= stream.Length)
            {
                var deletionFlag = reader.ReadByte();
                if (deletionFlag == 0x2A)
                {
                    _ = reader.ReadBytes(recordLength - 1);
                    continue;
                }

                foreach (var descriptor in descriptors)
                {
                    var raw = reader.ReadBytes(descriptor.Length);
                    if (!descriptor.IsNumeric)
                    {
                        continue;
                    }

                    var text = encoding.GetString(raw).Trim();
                    if (TryParseNumericText(text, out var value))
                    {
                        results[descriptor.Name].Add(value);
                    }
                }
            }

            return results.Where(item => item.Value.Count > 0)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
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
    }
}
