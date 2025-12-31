using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using EconToolbox.Desktop.Models;
using Microsoft.VisualBasic.FileIO;

namespace EconToolbox.Desktop.ViewModels
{
    public class StageDamageOrganizerViewModel : BaseViewModel
    {
        private readonly ObservableCollection<StageDamageRecord> _records = new();

        private string _summaryName = "StageDamageSummary";
        private string _statusMessage = "Load one or more Stage Damage Functions_StructureStageDamageDetails.csv files to summarize damages by category.";
        private bool _isBusy;

        public ObservableCollection<StageDamageRecord> Records => _records;
        public ObservableCollection<StageDamageCategorySummary> CategorySummaries { get; } = new();
        public ObservableCollection<StageDamageHighlight> Highlights { get; } = new();
        public ObservableCollection<ChartSeries> ChartSeries { get; } = new();

        public string SummaryName
        {
            get => _summaryName;
            set
            {
                if (value == _summaryName) return;
                _summaryName = value;
                OnPropertyChanged();
                UpdateChartSeriesLabel();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool HasRecords => Records.Count > 0;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public IAsyncRelayCommand ImportCsvCommand { get; }
        public IRelayCommand ClearCommand { get; }
        public IAsyncRelayCommand SaveSummaryCommand { get; }

        public StageDamageOrganizerViewModel()
        {
            ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync);
            ClearCommand = new RelayCommand(ClearAll, () => Records.Count > 0);
            SaveSummaryCommand = new AsyncRelayCommand(SaveSummaryAsync, () => Records.Count > 0);

            Records.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasRecords));
                (ClearCommand as RelayCommand)?.NotifyCanExecuteChanged();
                (SaveSummaryCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            };
        }

        private void ClearAll()
        {
            Records.Clear();
            CategorySummaries.Clear();
            Highlights.Clear();
            ChartSeries.Clear();
            StatusMessage = "Cleared results. Load new CSV files to regenerate summaries.";
        }

        private async Task ImportCsvAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Multiselect = true,
                Title = "Select Stage Damage Functions_StructureStageDamageDetails.csv files"
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var imported = await Task.Run(() => LoadRecords(dialog.FileNames));

                Records.Clear();
                foreach (var record in imported)
                {
                    Records.Add(record);
                }

                ComputeSummaries();
                StatusMessage = $"Loaded {Records.Count} rows from {dialog.FileNames.Length} file(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load CSV data: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveSummaryAsync()
        {
            if (Records.Count == 0)
            {
                StatusMessage = "No data to export. Load a CSV first.";
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"{SummaryName}.csv",
                Title = "Save summarized stage damage results"
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return;
            }

            try
            {
                await Task.Run(() => WriteSummaryCsv(dialog.FileName));
                StatusMessage = $"Saved summary to \"{Path.GetFileName(dialog.FileName)}\".";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save summary: {ex.Message}";
            }
        }

        private void WriteSummaryCsv(string filePath)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine($"{SummaryName} - Damage totals by category");
            writer.WriteLine("Damage Category,Structures,Structure Damage @0.493 AEP,Structure Damage @0.224 AEP,Structure Damage @0.034 AEP,Structure Damage @0.011 AEP,Structure Damage @0.003 AEP,Frequent AEP Sum,Peak Frequent AEP");
            foreach (var summary in CategorySummaries)
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    Escape(summary.DamageCategory),
                    summary.StructureCount.ToString(CultureInfo.InvariantCulture),
                    summary.StructureDamage0493.ToString("0.##", CultureInfo.InvariantCulture),
                    summary.StructureDamage0224.ToString("0.##", CultureInfo.InvariantCulture),
                    summary.StructureDamage0034.ToString("0.##", CultureInfo.InvariantCulture),
                    summary.StructureDamage0011.ToString("0.##", CultureInfo.InvariantCulture),
                    summary.StructureDamage0003.ToString("0.##", CultureInfo.InvariantCulture),
                    summary.FrequentSumDamage.ToString("0.##", CultureInfo.InvariantCulture),
                    summary.PeakStructureDamage.ToString("0.##", CultureInfo.InvariantCulture)
                }));
            }

            writer.WriteLine();
            writer.WriteLine("Top structures by frequent AEP structure damage");
            writer.WriteLine("Structure FID,Damage Category,Impact Area,Description,Highest AEP,Structure Damage");
            foreach (var highlight in Highlights)
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    Escape(highlight.StructureFid),
                    Escape(highlight.DamageCategory),
                    Escape(highlight.ImpactArea),
                    Escape(highlight.Description),
                    Escape(highlight.HighestAepLabel),
                    highlight.HighestStructureDamage.ToString("0.##", CultureInfo.InvariantCulture)
                }));
            }
        }

        private static string Escape(string value)
        {
            if (value.Contains(',') || value.Contains('"'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private void ComputeSummaries()
        {
            CategorySummaries.Clear();
            Highlights.Clear();
            ChartSeries.Clear();

            if (Records.Count == 0)
            {
                StatusMessage = "No rows available. Load CSV files to generate results.";
                return;
            }

            var summaries = Records
                .GroupBy(r => string.IsNullOrWhiteSpace(r.DamageCategory) ? "Uncategorized" : r.DamageCategory.Trim())
                .Select(g => new StageDamageCategorySummary
                {
                    DamageCategory = g.Key,
                    StructureCount = g.Count(),
                    StructureDamage0493 = g.Sum(r => r.StructureDamage0493),
                    StructureDamage0224 = g.Sum(r => r.StructureDamage0224),
                    StructureDamage0034 = g.Sum(r => r.StructureDamage0034),
                    StructureDamage0011 = g.Sum(r => r.StructureDamage0011),
                    StructureDamage0003 = g.Sum(r => r.StructureDamage0003),
                    FrequentSumDamage = g.Sum(r => r.FrequentSumDamage),
                    PeakStructureDamage = g.Max(r => r.FrequentPeakDamage)
                })
                .OrderByDescending(s => s.FrequentSumDamage)
                .ToList();

            foreach (var summary in summaries)
            {
                CategorySummaries.Add(summary);
            }

            var topStructures = Records
                .OrderByDescending(r => r.FrequentPeakDamage)
                .ThenBy(r => r.StructureFid)
                .Take(10)
                .Select(r => new StageDamageHighlight
                {
                    StructureFid = r.StructureFid,
                    DamageCategory = r.DamageCategory,
                    Description = r.Description,
                    ImpactArea = r.ImpactArea,
                    HighestAepLabel = r.FrequentPeakAepLabel,
                    HighestStructureDamage = r.FrequentPeakDamage
                });

            foreach (var highlight in topStructures)
            {
                Highlights.Add(highlight);
            }

            var chartSeries = new ChartSeries
            {
                Name = SummaryName,
                Stroke = new SolidColorBrush(Color.FromRgb(45, 106, 142)),
                Points = summaries.Select((s, index) => new ChartDataPoint
                {
                    X = index,
                    Y = s.FrequentSumDamage,
                    Label = s.DamageCategory
                }).ToList()
            };

            ChartSeries.Add(chartSeries);
        }

        private void UpdateChartSeriesLabel()
        {
            if (ChartSeries.Count == 0)
            {
                return;
            }

            foreach (var series in ChartSeries)
            {
                series.Name = SummaryName;
            }

            OnPropertyChanged(nameof(ChartSeries));
        }

        private static List<StageDamageRecord> LoadRecords(IEnumerable<string> filePaths)
        {
            var results = new List<StageDamageRecord>();
            foreach (var path in filePaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                using var parser = new TextFieldParser(path)
                {
                    Delimiters = new[] { "," },
                    HasFieldsEnclosedInQuotes = true,
                    TrimWhiteSpace = true,
                    TextFieldType = FieldType.Delimited
                };

                if (!TryReadHeader(parser, out var header))
                {
                    continue;
                }

                var headerMap = BuildHeaderMap(header!);
                while (!parser.EndOfData)
                {
                    var row = parser.ReadFields();
                    if (row == null || row.Length == 0)
                    {
                        continue;
                    }

                    var record = ParseRow(row, headerMap);
                    if (record != null)
                    {
                        results.Add(record);
                    }
                }
            }

            return results;
        }

        private static Dictionary<string, int> BuildHeaderMap(string[] header)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                var normalized = NormalizeHeader(header[i]);
                if (!map.ContainsKey(normalized))
                {
                    map[normalized] = i;
                }
            }

            return map;
        }

        private static StageDamageRecord? ParseRow(string[] row, IReadOnlyDictionary<string, int> map)
        {
            string structureFid = ReadString(row, map, "structurefid", "structureid");
            string damageCategory = ReadString(row, map, "damagecatagory", "damagecategory");

            if (string.IsNullOrWhiteSpace(structureFid) && string.IsNullOrWhiteSpace(damageCategory))
            {
                return null;
            }

            double s0493 = ReadDouble(row, map, "structuredamageat0493aep", "structuredamage0493aep", "structuredamage0493");
            double s0224 = ReadDouble(row, map, "structuredamageat0224aep", "structuredamage0224aep", "structuredamage0224");
            double s0034 = ReadDouble(row, map, "structuredamageat0034aep", "structuredamage0034aep", "structuredamage0034");
            double s0011 = ReadDouble(row, map, "structuredamageat0011aep", "structuredamage0011aep", "structuredamage0011");
            double s0003 = ReadDouble(row, map, "structuredamageat0003aep", "structuredamage0003aep", "structuredamage0003");

            bool hasDamage = s0493 > 0 || s0224 > 0 || s0034 > 0 || s0011 > 0 || s0003 > 0;
            if (!hasDamage)
            {
                return null;
            }

            return new StageDamageRecord
            {
                StructureFid = string.IsNullOrWhiteSpace(structureFid) ? "Unknown" : structureFid.Trim(),
                DamageCategory = string.IsNullOrWhiteSpace(damageCategory) ? "Uncategorized" : damageCategory.Trim(),
                Description = ReadString(row, map, "description"),
                ImpactArea = ReadString(row, map, "impactarearownumberinimpactareaset", "impactarea"),
                OccTypeName = ReadString(row, map, "occtypename", "occupancytype"),
                StructureDamage0493 = s0493,
                StructureDamage0224 = s0224,
                StructureDamage0034 = s0034,
                StructureDamage0011 = s0011,
                StructureDamage0003 = s0003
            };
        }

        private static bool TryReadHeader(TextFieldParser parser, out string[]? header)
        {
            header = null;

            while (!parser.EndOfData)
            {
                var candidate = parser.ReadFields();
                if (candidate == null)
                {
                    break;
                }

                // FDA exports typically start with a title row. Walk until we find a row that
                // looks like the actual header instead of skipping only a single line.
                if (candidate.Any(f => f.Contains("Structure", StringComparison.OrdinalIgnoreCase)))
                {
                    header = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeHeader(string header)
        {
            var filtered = header.Where(char.IsLetterOrDigit);
            return new string(filtered.ToArray()).ToLowerInvariant();
        }

        private static string ReadString(string[] row, IReadOnlyDictionary<string, int> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out int index) && index < row.Length)
                {
                    return row[index];
                }
            }

            return string.Empty;
        }

        private static double ReadDouble(string[] row, IReadOnlyDictionary<string, int> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out int index) && index < row.Length)
                {
                    var raw = row[index];
                    if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double value) ||
                        double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                    {
                        if (double.IsNaN(value) || double.IsInfinity(value))
                        {
                            return 0d;
                        }

                        return value;
                    }
                }
            }

            return 0d;
        }
    }
}
