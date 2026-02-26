using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels
{
    public sealed class UncertaintyStatisticsViewModel : DiagnosticViewModelBase, IComputeModule
    {
        private readonly Dictionary<string, List<double>> _numericAttributeValues = new(StringComparer.OrdinalIgnoreCase);
        private string _statusMessage = "Load a shapefile to analyze uncertainty statistics for structure-related attributes.";
        private string _selectedCategory = "Structure Value";
        private string? _selectedAttribute;
        private string _shapefilePath = string.Empty;

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
                    RefreshDiagnostics();
                    (ComputeCommand as RelayCommand)?.NotifyCanExecuteChanged();
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

            var ordered = values.OrderBy(v => v).ToArray();
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
            AddStatistic("Selected Category", SelectedCategory);
            AddStatistic("Selected Attribute", SelectedAttribute);
            AddStatistic("Observation Count", count.ToString(CultureInfo.InvariantCulture));
            AddStatistic("Minimum", FormatNumber(min));
            AddStatistic("Maximum", FormatNumber(max));
            AddStatistic("Range", FormatNumber(max - min));
            AddStatistic("Mean", FormatNumber(mean));
            AddStatistic("Median (P50)", FormatNumber(median));
            AddStatistic("First Quartile (P25)", FormatNumber(q1));
            AddStatistic("Third Quartile (P75)", FormatNumber(q3));
            AddStatistic("Population Variance", FormatNumber(variancePopulation));
            AddStatistic("Population Standard Deviation", FormatNumber(stdPopulation));
            AddStatistic("Sample Standard Deviation", FormatNumber(stdSample));
            AddStatistic("Coefficient of Variation", coefficientOfVariation.ToString("P2", CultureInfo.InvariantCulture));
            AddStatistic("Skewness", FormatNumber(skewness));

            StatusMessage = $"Computed uncertainty statistics for '{SelectedAttribute}' using {count} record(s).";
            RefreshDiagnostics();
        }

        protected override IEnumerable<DiagnosticItem> BuildDiagnostics()
        {
            yield return new DiagnosticItem(
                "EM 1110-2-1619 context",
                "Use Structure Value, Content Value, and First Floor Elevation attributes to characterize uncertainty in economic inputs for flood risk management studies.");

            yield return new DiagnosticItem(
                "Input source",
                string.IsNullOrWhiteSpace(ShapefilePath)
                    ? "No shapefile selected."
                    : $"Loaded: {Path.GetFileName(ShapefilePath)}");

            yield return new DiagnosticItem(
                "Selected attribute",
                string.IsNullOrWhiteSpace(SelectedAttribute)
                    ? "Choose a numeric field from the shapefile table."
                    : SelectedAttribute);

            if (Statistics.Count > 0)
            {
                var cv = Statistics.FirstOrDefault(item => item.Metric == "Coefficient of Variation")?.Value ?? "n/a";
                yield return new DiagnosticItem("Key uncertainty indicator", $"Coefficient of variation: {cv}");
            }
        }

        private static Dictionary<string, List<double>> LoadNumericAttributesFromDbf(string dbfPath)
        {
            using var stream = File.OpenRead(dbfPath);
            using var reader = new BinaryReader(stream);

            _ = reader.ReadByte();
            _ = reader.ReadBytes(3);
            _ = reader.ReadInt32();
            var headerLength = reader.ReadInt16();
            var recordLength = reader.ReadInt16();
            _ = reader.ReadBytes(20);

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
            var encoding = Encoding.GetEncoding(1252);

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
                    if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value) ||
                        double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value))
                    {
                        results[descriptor.Name].Add(value);
                    }
                }
            }

            return results.Where(item => item.Value.Count > 0)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
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

        private void AddStatistic(string metric, string value)
        {
            Statistics.Add(new AttributeStatistic(metric, value));
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
