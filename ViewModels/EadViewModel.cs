using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
        public ObservableCollection<string> DamageColumns { get; } = new();

        private bool _useStage;
        public bool UseStage
        {
            get => _useStage;
            set { _useStage = value; OnPropertyChanged(); }
        }

        private ObservableCollection<string> _results = new();
        public ObservableCollection<string> Results
        {
            get => _results;
            set { _results = value; OnPropertyChanged(); }
        }

        private PointCollection _stageDamagePoints = new();
        public PointCollection StageDamagePoints
        {
            get => _stageDamagePoints;
            set { _stageDamagePoints = value; OnPropertyChanged(); }
        }

        private PointCollection _frequencyDamagePoints = new();
        public PointCollection FrequencyDamagePoints
        {
            get => _frequencyDamagePoints;
            set { _frequencyDamagePoints = value; OnPropertyChanged(); }
        }

        public ICommand AddDamageColumnCommand { get; }
        public ICommand RemoveDamageColumnCommand => _removeDamageColumnCommand;
        public ICommand ComputeCommand { get; }
        public ICommand ExportCommand { get; }

        private readonly RelayCommand _removeDamageColumnCommand;

        public EadViewModel()
        {
            Rows.CollectionChanged += Rows_CollectionChanged;
            AddDamageColumnCommand = new RelayCommand(AddDamageColumn);
            _removeDamageColumnCommand = new RelayCommand(RemoveDamageColumn, () => DamageColumns.Count > 1);
            ComputeCommand = new RelayCommand(Compute);
            ExportCommand = new RelayCommand(Export);
            DamageColumns.CollectionChanged += DamageColumns_CollectionChanged;
            AddDamageColumn(); // start with one damage column
        }

        private void DamageColumns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            _removeDamageColumnCommand.RaiseCanExecuteChanged();
            if (e.Action == NotifyCollectionChangedAction.Replace)
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
            DamageColumns.Add($"Damage {DamageColumns.Count + 1}");
            foreach (var row in Rows)
                row.Damages.Add(0);
            _removeDamageColumnCommand.RaiseCanExecuteChanged();
        }

        private void RemoveDamageColumn()
        {
            if (DamageColumns.Count == 0) return;
            int idx = DamageColumns.Count - 1;
            DamageColumns.RemoveAt(idx);
            foreach (var row in Rows)
            {
                if (row.Damages.Count > idx)
                    row.Damages.RemoveAt(idx);
            }
            _removeDamageColumnCommand.RaiseCanExecuteChanged();
        }

        private void Compute()
        {
            try
            {
                if (Rows.Count == 0 || DamageColumns.Count == 0)
                {
                    Results = new ObservableCollection<string> { "No data" };
                    StageDamagePoints = new PointCollection();
                    FrequencyDamagePoints = new PointCollection();
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
                    results.Add($"{DamageColumns[i]}: {ead:F2}");
                }
                Results = new ObservableCollection<string>(results);
                UpdateCharts();
            }
            catch (Exception ex)
            {
                Results = new ObservableCollection<string> { $"Error: {ex.Message}" };
                StageDamagePoints = new PointCollection();
                FrequencyDamagePoints = new PointCollection();
            }
        }

        private void UpdateCharts()
        {
            if (Rows.Count == 0) return;

            var freqData = Rows
                .Where(r => r.Damages.Count > 0)
                .Select(r => (X: r.Probability, Y: r.Damages[0]))
                .ToList();
            FrequencyDamagePoints = CreatePointCollection(freqData);

            if (UseStage)
            {
                var stageData = Rows
                    .Where(r => r.Stage.HasValue && r.Damages.Count > 0)
                    .Select(r => (X: r.Stage!.Value, Y: r.Damages[0]))
                    .ToList();
                StageDamagePoints = CreatePointCollection(stageData);
            }
            else
            {
                StageDamagePoints = new PointCollection();
            }
        }

        private static PointCollection CreatePointCollection(System.Collections.Generic.List<(double X, double Y)> data)
        {
            PointCollection points = new();
            if (data.Count == 0) return points;

            double minX = data.Min(p => p.X);
            double maxX = data.Max(p => p.X);
            double minY = data.Min(p => p.Y);
            double maxY = data.Max(p => p.Y);

            double width = 300;
            double height = 150;
            double xRange = maxX - minX;
            if (xRange == 0) xRange = 1;
            double yRange = maxY - minY;
            if (yRange == 0) yRange = 1;

            foreach (var p in data)
            {
                double x = (p.X - minX) / xRange * width;
                double y = height - (p.Y - minY) / yRange * height;
                points.Add(new System.Windows.Point(x, y));
            }

            return points;
        }

        private void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "ead.xlsx"
            };
            if (dlg.ShowDialog() == true)
            {
                string combined = string.Join(" | ", Results);
                Services.ExcelExporter.ExportEad(Rows, DamageColumns, UseStage, combined, dlg.FileName);
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
    }
}
