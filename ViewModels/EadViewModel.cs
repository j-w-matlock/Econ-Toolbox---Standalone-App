using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using EconToolbox.Desktop.Behaviors;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{
    /// <summary>
    /// View model for Expected Annual Damage calculations. Users enter
    /// frequency, optional stage data and any number of damage columns
    /// using a grid. Results can be charted against stage and frequency.
    /// </summary>
    public class EadViewModel : BaseViewModel
    {
        public ObservableCollection<EadRow> Rows { get; } = new();
        public ObservableCollection<DamageColumn> DamageColumns { get; } = new();
        public ObservableCollection<DataGridColumnDescriptor> ColumnDefinitions { get; } = new();

        private bool _useStage;
        public bool UseStage
        {
            get => _useStage;
            set
            {
                if (_useStage == value)
                {
                    return;
                }

                _useStage = value;
                OnPropertyChanged();
                UpdateColumnDefinitions();
                Compute();
            }
        }

        private ObservableCollection<string> _results = new();
        public ObservableCollection<string> Results
        {
            get => _results;
            set { _results = value; OnPropertyChanged(); }
        }

        private PointCollection _damageCurvePoints = new();
        public PointCollection DamageCurvePoints
        {
            get => _damageCurvePoints;
            set { _damageCurvePoints = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ChartSeries> DamageSeries { get; } = new();

        private string _xAxisMinLabel = string.Empty;
        public string XAxisMinLabel
        {
            get => _xAxisMinLabel;
            private set { _xAxisMinLabel = value; OnPropertyChanged(); }
        }

        private string _xAxisMidLabel = string.Empty;
        public string XAxisMidLabel
        {
            get => _xAxisMidLabel;
            private set { _xAxisMidLabel = value; OnPropertyChanged(); }
        }

        private string _xAxisMaxLabel = string.Empty;
        public string XAxisMaxLabel
        {
            get => _xAxisMaxLabel;
            private set { _xAxisMaxLabel = value; OnPropertyChanged(); }
        }

        private string _xAxisTitle = string.Empty;
        public string XAxisTitle
        {
            get => _xAxisTitle;
            private set { _xAxisTitle = value; OnPropertyChanged(); }
        }

        private string _customXAxisTitle = string.Empty;
        public string CustomXAxisTitle
        {
            get => _customXAxisTitle;
            set
            {
                _customXAxisTitle = value;
                OnPropertyChanged();
                RefreshAxisTitles();
            }
        }

        private string _yAxisMinLabel = string.Empty;
        public string YAxisMinLabel
        {
            get => _yAxisMinLabel;
            private set { _yAxisMinLabel = value; OnPropertyChanged(); }
        }

        private string _yAxisMidLabel = string.Empty;
        public string YAxisMidLabel
        {
            get => _yAxisMidLabel;
            private set { _yAxisMidLabel = value; OnPropertyChanged(); }
        }

        private string _yAxisMaxLabel = string.Empty;
        public string YAxisMaxLabel
        {
            get => _yAxisMaxLabel;
            private set { _yAxisMaxLabel = value; OnPropertyChanged(); }
        }

        private string _yAxisTitle = string.Empty;
        public string YAxisTitle
        {
            get => _yAxisTitle;
            private set { _yAxisTitle = value; OnPropertyChanged(); }
        }

        private string _customYAxisTitle = string.Empty;
        public string CustomYAxisTitle
        {
            get => _customYAxisTitle;
            set
            {
                _customYAxisTitle = value;
                OnPropertyChanged();
                RefreshAxisTitles();
            }
        }

        private string _chartTitle = "Expected Annual Damage";
        public string ChartTitle
        {
            get => _chartTitle;
            set
            {
                _chartTitle = value;
                OnPropertyChanged();
            }
        }

        private const double ChartWidth = 420;
        private const double ChartHeight = 220;

        private string _defaultXAxisTitle = string.Empty;
        private string _defaultYAxisTitle = string.Empty;

        public IRelayCommand AddDamageColumnCommand { get; }
        public IRelayCommand RemoveDamageColumnCommand => _removeDamageColumnCommand;
        public IRelayCommand ComputeCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }

        private readonly RelayCommand _removeDamageColumnCommand;
        private readonly IExcelExportService _excelExportService;

        public EadViewModel(IExcelExportService excelExportService)
        {
            _excelExportService = excelExportService;
            Rows.CollectionChanged += Rows_CollectionChanged;
            AddDamageColumnCommand = new RelayCommand(AddDamageColumn);
            _removeDamageColumnCommand = new RelayCommand(RemoveDamageColumn, () => DamageColumns.Count > 1);
            ComputeCommand = new RelayCommand(Compute);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            DamageColumns.CollectionChanged += DamageColumns_CollectionChanged;
            AddDamageColumn(); // start with one damage column
            InitializeDefaultRows();
            UpdateColumnDefinitions();
            Compute();
        }

        private void InitializeDefaultRows()
        {
            if (Rows.Count > 0)
            {
                return;
            }

            double[] defaultProbabilities = new[] { 0.002, 0.005, 0.01, 0.02, 0.04, 0.1, 0.2, 0.5 };
            double[] defaultDamages = new[] { 1_000_000d, 500_000d, 250_000d, 100_000d, 50_000d, 10_000d, 5_000d, 0d };

            for (int i = 0; i < defaultProbabilities.Length; i++)
            {
                var row = new EadRow
                {
                    Probability = defaultProbabilities[i]
                };

                if (DamageColumns.Count > 0)
                {
                    row.Damages.Add(defaultDamages[i]);
                }

                Rows.Add(row);
            }
        }

        private void DamageColumns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (DamageColumn column in e.OldItems)
                {
                    column.PropertyChanged -= DamageColumn_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (DamageColumn column in e.NewItems)
                {
                    column.PropertyChanged += DamageColumn_PropertyChanged;
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var _ in e.NewItems)
                {
                    foreach (var row in Rows)
                    {
                        row.Damages.Add(0);
                    }
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var row in Rows)
                {
                    while (row.Damages.Count > DamageColumns.Count)
                    {
                        row.Damages.RemoveAt(row.Damages.Count - 1);
                    }
                }
            }

            _removeDamageColumnCommand.NotifyCanExecuteChanged();
            UpdateColumnDefinitions();
            Compute();
        }

        private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (EadRow row in e.NewItems)
                {
                    while (row.Damages.Count < DamageColumns.Count)
                        row.Damages.Add(0);
                }
            }
        }

        private void AddDamageColumn()
        {
            DamageColumns.Add(new DamageColumn { Name = $"Damage {DamageColumns.Count + 1}" });
            _removeDamageColumnCommand.NotifyCanExecuteChanged();
        }

        private void RemoveDamageColumn()
        {
            if (DamageColumns.Count == 0) return;
            DamageColumns.RemoveAt(DamageColumns.Count - 1);
            _removeDamageColumnCommand.NotifyCanExecuteChanged();
        }

        private void Compute()
        {
            try
            {
                if (Rows.Count == 0 || DamageColumns.Count == 0)
                {
                    Results = new ObservableCollection<string> { "No data" };
                    DamageSeries.Clear();
                    DamageCurvePoints = new PointCollection();
                    SetAxisLabels(null, false);
                    return;
                }

                if (Rows.Any(r => double.IsNaN(r.Probability) || r.Probability < 0 || r.Probability > 1))
                {
                    Results = new ObservableCollection<string> { "Probabilities must be between 0 and 1." };
                    DamageSeries.Clear();
                    DamageCurvePoints = new PointCollection();
                    SetAxisLabels(null, false);
                    return;
                }

                // Sort rows by probability in descending order to enforce monotonicity
                var sortedRows = Rows.OrderByDescending(r => r.Probability).ToList();
                if (!sortedRows.SequenceEqual(Rows))
                {
                    Rows.Clear();
                    foreach (var r in sortedRows)
                        Rows.Add(r);
                }

                var probabilities = Rows.Select(r => r.Probability).ToArray();
                var results = new System.Collections.Generic.List<string>();
                for (int i = 0; i < DamageColumns.Count; i++)
                {
                    var damages = Rows.Select(r => r.Damages[i]).ToArray();
                    double ead = EadModel.Compute(probabilities, damages);
                    results.Add($"{DamageColumns[i].Name}: {ead:F2}");
                }
                Results = new ObservableCollection<string>(results);
                UpdateCharts();
            }
            catch (Exception ex)
            {
                Results = new ObservableCollection<string> { $"Error: {ex.Message}" };
                DamageCurvePoints = new PointCollection();
                DamageSeries.Clear();
                SetAxisLabels(null, false);
            }
        }

        private void UpdateCharts()
        {
            if (Rows.Count == 0) return;

            bool hasStageData = UseStage && Rows.Any(r => r.Stage.HasValue && r.Damages.Count > 0);

            var seriesData = new System.Collections.Generic.List<(string Name, System.Collections.Generic.List<(double X, double Y)> Points)>();
            double? minX = null, maxX = null, minY = null, maxY = null;

            for (int i = 0; i < DamageColumns.Count; i++)
            {
                var data = hasStageData
                    ? Rows.Where(r => r.Stage.HasValue && r.Damages.Count > i)
                          .OrderBy(r => r.Stage!.Value)
                          .Select(r => (X: r.Stage!.Value, Y: r.Damages[i]))
                          .ToList()
                    : Rows.Where(r => r.Damages.Count > i)
                          .OrderBy(r => r.Probability)
                          .Select(r => (X: r.Probability, Y: r.Damages[i]))
                          .ToList();

                if (data.Count == 0)
                {
                    continue;
                }

                minX = minX.HasValue ? Math.Min(minX.Value, data.Min(p => p.X)) : data.Min(p => p.X);
                maxX = maxX.HasValue ? Math.Max(maxX.Value, data.Max(p => p.X)) : data.Max(p => p.X);
                minY = minY.HasValue ? Math.Min(minY.Value, data.Min(p => p.Y)) : data.Min(p => p.Y);
                maxY = maxY.HasValue ? Math.Max(maxY.Value, data.Max(p => p.Y)) : data.Max(p => p.Y);

                seriesData.Add((DamageColumns[i].Name, data));
            }

            DamageSeries.Clear();
            if (seriesData.Count == 0 || !minX.HasValue || !maxX.HasValue || !minY.HasValue || !maxY.HasValue)
            {
                DamageCurvePoints = new PointCollection();
                SetAxisLabels(null, hasStageData);
                return;
            }

            var baselineMinY = Math.Min(0, minY.Value);
            var range = new ChartRange(minX.Value, maxX.Value, baselineMinY, maxY.Value);

            double xPadding = (range.MaxX - range.MinX) * 0.05;
            if (xPadding <= 0)
            {
                xPadding = hasStageData ? Math.Max(Math.Abs(range.MaxX) * 0.05, 1) : 0.05;
            }

            double paddedMinX = hasStageData ? range.MinX - xPadding : Math.Max(0, range.MinX - xPadding);
            double paddedMaxX = hasStageData ? range.MaxX + xPadding : Math.Min(1, range.MaxX + xPadding);

            double yPadding = (range.MaxY - range.MinY) * 0.15;
            if (yPadding <= 0)
            {
                yPadding = Math.Max(Math.Abs(range.MaxY) * 0.15, 1);
            }

            double paddedMinY = range.MinY - yPadding;
            double paddedMaxY = range.MaxY + yPadding;
            var paddedRange = new ChartRange(paddedMinX, paddedMaxX, paddedMinY, paddedMaxY);
            for (int i = 0; i < seriesData.Count; i++)
            {
                var scaledPoints = CreatePointCollection(seriesData[i].Points, paddedRange);
                var series = new ChartSeries(seriesData[i].Name, scaledPoints, GetSeriesBrush(i));
                DamageSeries.Add(series);

                if (i == 0)
                {
                    DamageCurvePoints = new PointCollection(scaledPoints);
                }
            }

            SetAxisLabels(range, hasStageData);
        }

        private static PointCollection CreatePointCollection(System.Collections.Generic.List<(double X, double Y)> data, ChartRange range)
        {
            PointCollection points = new();
            if (data.Count == 0) return points;

            double xRange = range.MaxX - range.MinX;
            if (xRange == 0) xRange = 1;
            double yRange = range.MaxY - range.MinY;
            if (yRange == 0) yRange = 1;

            foreach (var p in data)
            {
                double x = (p.X - range.MinX) / xRange * ChartWidth;
                double y = ChartHeight - (p.Y - range.MinY) / yRange * ChartHeight;
                points.Add(new System.Windows.Point(x, y));
            }

            return points;
        }

        private void SetAxisLabels(ChartRange? range, bool hasStageData)
        {
            if (range == null)
            {
                XAxisMinLabel = string.Empty;
                XAxisMidLabel = string.Empty;
                XAxisMaxLabel = string.Empty;
                _defaultXAxisTitle = string.Empty;
                YAxisMinLabel = string.Empty;
                YAxisMidLabel = string.Empty;
                YAxisMaxLabel = string.Empty;
                _defaultYAxisTitle = string.Empty;
                RefreshAxisTitles();
                return;
            }

            XAxisMinLabel = FormatXAxisValue(range.Value.MinX, hasStageData);
            XAxisMidLabel = FormatXAxisValue((range.Value.MinX + range.Value.MaxX) / 2, hasStageData);
            XAxisMaxLabel = FormatXAxisValue(range.Value.MaxX, hasStageData);
            _defaultXAxisTitle = hasStageData ? "Stage / Water Surface" : "Exceedance Probability (annual)";
            YAxisMinLabel = FormatYAxisValue(range.Value.MinY);
            YAxisMidLabel = FormatYAxisValue((range.Value.MinY + range.Value.MaxY) / 2);
            YAxisMaxLabel = FormatYAxisValue(range.Value.MaxY);
            _defaultYAxisTitle = "Damage (USD)";
            RefreshAxisTitles();
        }

        private void RefreshAxisTitles()
        {
            XAxisTitle = string.IsNullOrWhiteSpace(CustomXAxisTitle)
                ? _defaultXAxisTitle
                : CustomXAxisTitle;
            YAxisTitle = string.IsNullOrWhiteSpace(CustomYAxisTitle)
                ? _defaultYAxisTitle
                : CustomYAxisTitle;
        }

        private string FormatXAxisValue(double value, bool hasStageData)
        {
            return hasStageData
                ? value.ToString("N2")
                : value.ToString("P1");
        }

        private string FormatYAxisValue(double value)
        {
            if (Math.Abs(value) >= 1_000_000)
            {
                return $"${value / 1_000_000d:0.##}M";
            }

            if (Math.Abs(value) >= 1_000)
            {
                return $"${value / 1_000d:0.##}K";
            }

            return value.ToString("C2");
        }

        private Brush GetSeriesBrush(int index)
        {
            Brush[] palette =
            {
                Brushes.SteelBlue,
                Brushes.OrangeRed,
                Brushes.SeaGreen,
                Brushes.MediumPurple,
                Brushes.Goldenrod,
                Brushes.Firebrick
            };

            return palette[index % palette.Length];
        }

        private async Task ExportAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "ead.xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string combined = string.Join(" | ", Results);
                    await Task.Run(() => _excelExportService.ExportEad(
                        Rows,
                        DamageColumns.Select(c => c.Name),
                        UseStage,
                        combined,
                        DamageCurvePoints.Select(p => new Point(p.X, p.Y)).ToList(),
                        dlg.FileName));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateColumnDefinitions()
        {
            ColumnDefinitions.Clear();

            ColumnDefinitions.Add(new DataGridColumnDescriptor(nameof(EadRow.Probability))
            {
                HeaderText = "Probability",
                MinWidth = 120,
                ToolTip = "Exceedance probability for the row of damage and stage inputs."
            });

            if (UseStage)
            {
                ColumnDefinitions.Add(new DataGridColumnDescriptor(nameof(EadRow.Stage))
                {
                    HeaderText = "Stage",
                    MinWidth = 120,
                    ToolTip = "Water surface elevation or stage aligned with the probability."
                });
            }

            for (int i = 0; i < DamageColumns.Count; i++)
            {
                ColumnDefinitions.Add(new DataGridColumnDescriptor($"Damages[{i}]")
                {
                    HeaderContext = DamageColumns[i],
                    HeaderBindingPath = nameof(DamageColumn.Name),
                    IsHeaderEditable = true,
                    MinWidth = 140,
                    ToolTip = "Damage amount for this category at the selected probability."
                });
            }
        }

        private void DamageColumn_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DamageColumn.Name))
            {
                Compute();
            }
        }

        public class EadRow : BaseViewModel
        {
            private double _probability;
            private double? _stage;

            public double Probability
            {
                get => _probability;
                set { _probability = value; OnPropertyChanged(); }
            }

            public double? Stage
            {
                get => _stage;
                set { _stage = value; OnPropertyChanged(); }
            }

            public ObservableCollection<double> Damages { get; } = new();
        }

        public class DamageColumn : BaseViewModel
        {
            private string _name = string.Empty;

            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value)
                    {
                        return;
                    }

                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public readonly struct ChartRange
        {
            public ChartRange(double minX, double maxX, double minY, double maxY)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
            }

            public double MinX { get; }
            public double MaxX { get; }
            public double MinY { get; }
            public double MaxY { get; }
        }

        public class ChartSeries
        {
            public ChartSeries(string name, PointCollection points, Brush stroke)
            {
                Name = name;
                Points = points;
                Stroke = stroke;
            }

            public string Name { get; }
            public PointCollection Points { get; }
            public Brush Stroke { get; }
        }
    }
}
