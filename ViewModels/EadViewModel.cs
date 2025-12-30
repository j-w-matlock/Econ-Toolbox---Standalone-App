using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using EconToolbox.Desktop.Behaviors;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;

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
        private readonly DispatcherTimer _computeDebounceTimer;
        private bool _suppressAutoCompute;
        private bool _isComputing;
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

        private ObservableCollection<EadResultRow> _results = new();
        public ObservableCollection<EadResultRow> Results
        {
            get => _results;
            set { _results = value; OnPropertyChanged(); }
        }

        private Graph _graph = new("eadGraph");
        public Graph Graph
        {
            get => _graph;
            private set { _graph = value; OnPropertyChanged(); }
        }

        public ObservableCollection<LegendItem> LegendItems { get; } = new();

        private string _chartTitle = "Expected Annual Damage";
        public string ChartTitle
        {
            get => _chartTitle;
            set
            {
                _chartTitle = value;
                OnPropertyChanged();
                Graph.Attr.Label = value;
                OnPropertyChanged(nameof(Graph));
            }
        }

        public IRelayCommand AddDamageColumnCommand { get; }
        public IRelayCommand RemoveDamageColumnCommand => _removeDamageColumnCommand;
        public IRelayCommand ComputeCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }

        private readonly RelayCommand _removeDamageColumnCommand;
        private readonly IExcelExportService _excelExportService;

        public EadViewModel(IExcelExportService excelExportService)
        {
            _excelExportService = excelExportService;
            _computeDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _graph.Attr.Label = ChartTitle;
            _computeDebounceTimer.Tick += (_, _) =>
            {
                _computeDebounceTimer.Stop();
                Compute();
            };
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

                    AttachRow(row);
                }
            }

            if (e.OldItems != null)
            {
                foreach (EadRow row in e.OldItems)
                {
                    DetachRow(row);
                }
            }

            if (!_suppressAutoCompute)
            {
                ScheduleCompute();
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
            if (_isComputing)
            {
                return;
            }

            _computeDebounceTimer.Stop();
            _isComputing = true;
            try
            {
                if (Rows.Count == 0 || DamageColumns.Count == 0)
                {
                    Results = new ObservableCollection<EadResultRow>
                    {
                        new() { Label = "Status", Result = "No data" }
                    };
                    LegendItems.Clear();
                    Graph = CreateEmptyGraph("Enter frequency and damage inputs to build the MSAGL chart.");
                    return;
                }

                if (Rows.Any(r => double.IsNaN(r.Probability) || r.Probability < 0 || r.Probability > 1))
                {
                    Results = new ObservableCollection<EadResultRow>
                    {
                        new() { Label = "Status", Result = "Probabilities must be between 0 and 1." }
                    };
                    LegendItems.Clear();
                    Graph = CreateEmptyGraph("Probabilities must be between 0 and 1.");
                    return;
                }

                // Sort rows by probability in descending order to enforce monotonicity
                var sortedRows = Rows.OrderByDescending(r => r.Probability).ToList();
                if (!sortedRows.SequenceEqual(Rows))
                {
                    _suppressAutoCompute = true;
                    Rows.Clear();
                    foreach (var r in sortedRows)
                    {
                        Rows.Add(r);
                    }
                    _suppressAutoCompute = false;
                }

                var probabilities = Rows.Select(r => r.Probability).ToArray();
                var results = new System.Collections.Generic.List<EadResultRow>();
                for (int i = 0; i < DamageColumns.Count; i++)
                {
                    var damages = Rows.Select(r => r.Damages[i]).ToArray();
                    double ead = EadModel.Compute(probabilities, damages);
                    results.Add(new EadResultRow
                    {
                        Label = DamageColumns[i].Name,
                        Result = ead.ToString("C2")
                    });
                }
                Results = new ObservableCollection<EadResultRow>(results);
                UpdateGraph();
            }
            catch (Exception ex)
            {
                Results = new ObservableCollection<EadResultRow>
                {
                    new() { Label = "Error", Result = ex.Message }
                };
                LegendItems.Clear();
                Graph = CreateEmptyGraph("Unable to build graph. Check inputs and try again.");
            }
            finally
            {
                _isComputing = false;
            }
        }

        private void UpdateGraph()
        {
            LegendItems.Clear();
            bool hasStageData = UseStage && Rows.Any(r => r.Stage.HasValue && r.Damages.Count > 0);

            var seriesData = new System.Collections.Generic.List<(string Name, System.Collections.Generic.List<(double X, double Y)> Points)>();

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

                seriesData.Add((DamageColumns[i].Name, data));
            }

            if (seriesData.Count == 0)
            {
                Graph = CreateEmptyGraph("Add at least one probability/stage row to see the graph.");
                return;
            }

            var graph = new Graph("eadGraph")
            {
                Attr =
                {
                    LayerDirection = LayerDirection.LR,
                    Label = ChartTitle
                },
                LayoutAlgorithmSettings = new SugiyamaLayoutSettings
                {
                    EdgeRoutingSettings = new EdgeRoutingSettings
                    {
                        EdgeRoutingMode = EdgeRoutingMode.SplineBundling
                    },
                    LayerDirection = LayerDirection.LR
                }
            };

            for (int i = 0; i < seriesData.Count; i++)
            {
                var brush = GetSeriesBrush(i);
                var color = ToMsaglColor(brush);
                LegendItems.Add(new LegendItem
                {
                    Name = seriesData[i].Name,
                    Color = brush
                });

                Node? previousNode = null;
                bool labelPlaced = false;
                for (int pointIndex = 0; pointIndex < seriesData[i].Points.Count; pointIndex++)
                {
                    var point = seriesData[i].Points[pointIndex];
                    string nodeId = $"{seriesData[i].Name}_{pointIndex}";
                    var node = graph.AddNode(nodeId);
                    node.LabelText = hasStageData
                        ? $"Stage {point.X:N2}\n{point.Y:C0}"
                        : $"Prob {point.X:P2}\n{point.Y:C0}";
                    node.Attr.FillColor = color;
                    node.Attr.Color = color;
                    node.Attr.Shape = Shape.Circle;
                    node.Attr.LineWidth = 1.5;
                    node.Attr.Fontcolor = Microsoft.Msagl.Drawing.Color.Black;
                    node.Label.FontSize = 10;

                    if (previousNode != null)
                    {
                        var edge = graph.AddEdge(previousNode.Id, node.Id);
                        edge.Attr.ArrowheadAtTarget = ArrowStyle.None;
                        edge.Attr.Color = color;
                        edge.Attr.LineWidth = 2.5;
                        if (!labelPlaced)
                        {
                            edge.LabelText = seriesData[i].Name;
                            labelPlaced = true;
                        }
                    }

                    previousNode = node;
                }
            }

            Graph = graph;
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
                    ForceCompute();
                    string combined = string.Join(" | ", Results.Select(r => $"{r.Label}: {r.Result}"));
                    await Task.Run(() => _excelExportService.ExportEad(
                        Rows,
                        DamageColumns.Select(c => c.Name),
                        UseStage,
                        combined,
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

        private Graph CreateEmptyGraph(string label)
        {
            return new Graph("eadGraph")
            {
                Attr =
                {
                    LayerDirection = LayerDirection.LR,
                    Label = label
                }
            };
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

        private Microsoft.Msagl.Drawing.Color ToMsaglColor(Brush brush)
        {
            if (brush is SolidColorBrush scb)
            {
                return new Microsoft.Msagl.Drawing.Color(scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B);
            }

            return Microsoft.Msagl.Drawing.Color.Black;
        }

        private void DamageColumn_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DamageColumn.Name))
            {
                ScheduleCompute();
            }
        }

        private void AttachRow(EadRow row)
        {
            row.PropertyChanged += Row_PropertyChanged;
            row.Damages.CollectionChanged += RowDamages_CollectionChanged;
        }

        private void DetachRow(EadRow row)
        {
            row.PropertyChanged -= Row_PropertyChanged;
            row.Damages.CollectionChanged -= RowDamages_CollectionChanged;
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EadRow.Probability) || e.PropertyName == nameof(EadRow.Stage))
            {
                ScheduleCompute();
            }
        }

        private void RowDamages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ScheduleCompute();
        }

        private void ScheduleCompute()
        {
            if (_suppressAutoCompute)
            {
                return;
            }

            _computeDebounceTimer.Stop();
            _computeDebounceTimer.Start();
        }

        public void ForceCompute()
        {
            Compute();
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

        public class LegendItem
        {
            public string Name { get; set; } = string.Empty;
            public Brush Color { get; set; } = Brushes.Transparent;
        }
    }
}
