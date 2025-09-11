using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EconToolbox.Desktop.Models;

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

        private string _result = string.Empty;
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
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

        private readonly RelayCommand _removeDamageColumnCommand;

        public EadViewModel()
        {
            Rows.CollectionChanged += Rows_CollectionChanged;
            AddDamageColumnCommand = new RelayCommand(AddDamageColumn);
            _removeDamageColumnCommand = new RelayCommand(RemoveDamageColumn, () => DamageColumns.Count > 1);
            ComputeCommand = new RelayCommand(Compute);
            AddDamageColumn(); // start with one damage column
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
                    Result = "No data";
                    StageDamagePoints = new PointCollection();
                    FrequencyDamagePoints = new PointCollection();
                    return;
                }

                var probabilities = Rows.Select(r => 1.0 / r.Frequency).ToArray();
                var results = new System.Collections.Generic.List<string>();
                for (int i = 0; i < DamageColumns.Count; i++)
                {
                    var damages = Rows.Select(r => r.Damages[i]).ToArray();
                    double ead = EadModel.Compute(probabilities, damages);
                    results.Add($"{DamageColumns[i]}: {ead:F2}");
                }
                Result = string.Join(" | ", results);
                UpdateCharts();
            }
            catch (Exception ex)
            {
                Result = $"Error: {ex.Message}";
                StageDamagePoints = new PointCollection();
                FrequencyDamagePoints = new PointCollection();
            }
        }

        private void UpdateCharts()
        {
            if (Rows.Count == 0) return;

            var freqData = Rows
                .Where(r => r.Damages.Count > 0)
                .Select(r => (X: r.Frequency, Y: r.Damages[0]))
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

        public class EadRow : BaseViewModel
        {
            private double _frequency;
            private double? _stage;

            public double Frequency
            {
                get => _frequency;
                set { _frequency = value; OnPropertyChanged(); }
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
