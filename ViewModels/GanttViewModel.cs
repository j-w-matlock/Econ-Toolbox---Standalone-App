using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels
{
    public class GanttViewModel : BaseViewModel, IComputeModule
    {
        private readonly RelayCommand _addTaskCommand;
        private readonly RelayCommand _removeTaskCommand;
        private readonly RelayCommand _clearTasksCommand;
        private readonly RelayCommand _computeCommand;
        private GanttTask? _selectedTask;
        private DateTime _projectStart = DateTime.Today;
        private DateTime _projectFinish = DateTime.Today;
        private string _scheduleSummary = string.Empty;
        private int _colorSequence;

        public ObservableCollection<GanttTask> Tasks { get; } = new();
        public ObservableCollection<GanttBar> Bars { get; } = new();
        public ObservableCollection<GanttLink> Links { get; } = new();

        public GanttTask? SelectedTask
        {
            get => _selectedTask;
            set
            {
                if (_selectedTask == value)
                    return;
                _selectedTask = value;
                OnPropertyChanged();
                _removeTaskCommand.NotifyCanExecuteChanged();
            }
        }

        public DateTime ProjectStart
        {
            get => _projectStart;
            private set
            {
                if (_projectStart == value)
                    return;
                _projectStart = value;
                OnPropertyChanged();
            }
        }

        public DateTime ProjectFinish
        {
            get => _projectFinish;
            private set
            {
                if (_projectFinish == value)
                    return;
                _projectFinish = value;
                OnPropertyChanged();
            }
        }

        public string ScheduleSummary
        {
            get => _scheduleSummary;
            private set
            {
                if (_scheduleSummary == value)
                    return;
                _scheduleSummary = value;
                OnPropertyChanged();
            }
        }

        public double TotalDurationDays => Math.Max(1, (ProjectFinish - ProjectStart).TotalDays);

        public ICommand AddTaskCommand => _addTaskCommand;
        public ICommand RemoveTaskCommand => _removeTaskCommand;
        public ICommand ClearTasksCommand => _clearTasksCommand;
        public ICommand ComputeCommand => _computeCommand;

        public double TotalLaborCost => Tasks.Sum(t => t.TotalCost);

        public GanttViewModel()
        {
            _addTaskCommand = new RelayCommand(AddTask);
            _removeTaskCommand = new RelayCommand(RemoveSelectedTask, () => SelectedTask != null);
            _clearTasksCommand = new RelayCommand(ClearTasks);
            _computeCommand = new RelayCommand(ComputeSchedule);

            Tasks.CollectionChanged += OnTasksCollectionChanged;
            SeedDefaultTasks();
            ComputeSchedule();
            OnPropertyChanged(nameof(TotalLaborCost));
        }

        private void SeedDefaultTasks()
        {
            if (Tasks.Count > 0)
                return;

            var kickoff = new GanttTask
            {
                Name = "Project Kickoff",
                Workstream = "Initiation",
                StartDate = DateTime.Today,
                DurationDays = 2,
                PercentComplete = 100,
                LaborCostPerDay = 1200
            };
            kickoff.EndDate = kickoff.StartDate.AddDays(kickoff.DurationDays);
            AssignColor(kickoff);

            var planning = new GanttTask
            {
                Name = "Planning Workshops",
                Workstream = "Planning",
                StartDate = kickoff.EndDate,
                DurationDays = 5,
                Dependencies = kickoff.Name,
                PercentComplete = 60,
                LaborCostPerDay = 950
            };
            planning.EndDate = planning.StartDate.AddDays(planning.DurationDays);
            AssignColor(planning);

            var baseline = new GanttTask
            {
                Name = "Baseline Cost Estimate",
                Workstream = "Analysis",
                StartDate = planning.EndDate,
                DurationDays = 7,
                Dependencies = planning.Name,
                PercentComplete = 25,
                LaborCostPerDay = 1100
            };
            baseline.EndDate = baseline.StartDate.AddDays(baseline.DurationDays);
            AssignColor(baseline);

            Tasks.Add(kickoff);
            Tasks.Add(planning);
            Tasks.Add(baseline);
        }

        private void AddTask()
        {
            var task = new GanttTask
            {
                Name = $"Task {Tasks.Count + 1}",
                Workstream = "General",
                StartDate = ProjectFinish == DateTime.MinValue ? DateTime.Today : ProjectFinish,
                DurationDays = 5
            };
            task.EndDate = task.StartDate.AddDays(task.DurationDays);
            AssignColor(task);
            Tasks.Add(task);
            SelectedTask = task;
        }

        private void RemoveSelectedTask()
        {
            if (SelectedTask == null)
                return;
            var index = Tasks.IndexOf(SelectedTask);
            Tasks.Remove(SelectedTask);
            if (index >= 0 && index < Tasks.Count)
                SelectedTask = Tasks[index];
            else
                SelectedTask = Tasks.LastOrDefault();
        }

        private void ClearTasks()
        {
            foreach (var task in Tasks)
            {
                task.PropertyChanged -= OnTaskPropertyChanged;
            }
            Tasks.Clear();
            SelectedTask = null;
            Bars.Clear();
            Links.Clear();
            ScheduleSummary = string.Empty;
            ProjectStart = DateTime.Today;
            ProjectFinish = DateTime.Today;
            _colorSequence = 0;
            OnPropertyChanged(nameof(TotalDurationDays));
            OnPropertyChanged(nameof(TotalLaborCost));
        }

        public void ComputeSchedule()
        {
            if (Tasks.Count == 0)
            {
                Bars.Clear();
                ScheduleSummary = "Add tasks to build your schedule.";
                return;
            }

            var lookup = Tasks
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .GroupBy(t => t.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            foreach (var task in Tasks)
            {
                if (task.DurationDays == 0 && !task.IsMilestone)
                {
                    task.DurationDays = 1;
                }

                var dependencyNames = ParseDependencies(task.Dependencies);
                if (dependencyNames.Count > 0)
                {
                    DateTime? latestDependencyFinish = null;
                    foreach (var depName in dependencyNames)
                    {
                        if (!lookup.TryGetValue(depName, out var dependency))
                            continue;
                        if (latestDependencyFinish == null || dependency.EndDate > latestDependencyFinish)
                            latestDependencyFinish = dependency.EndDate;
                    }

                    if (latestDependencyFinish.HasValue && latestDependencyFinish.Value > task.StartDate)
                        task.StartDate = latestDependencyFinish.Value;
                }

                task.EndDate = task.IsMilestone
                    ? task.StartDate
                    : task.StartDate.AddDays(Math.Max(1, task.DurationDays));
            }

            ProjectStart = Tasks.Min(t => t.StartDate);
            ProjectFinish = Tasks.Max(t => t.EndDate);
            OnPropertyChanged(nameof(TotalDurationDays));

            Bars.Clear();
            Links.Clear();
            int row = 0;
            var orderedTasks = Tasks.OrderBy(t => t.StartDate).ToList();
            var barLookup = new Dictionary<GanttTask, GanttBar>();
            foreach (var task in orderedTasks)
            {
                double offset = (task.StartDate - ProjectStart).TotalDays;
                double length = task.IsMilestone ? 0.5 : Math.Max(1, task.DurationDays);
                var bar = new GanttBar(task, row++, offset, length);
                Bars.Add(bar);
                barLookup[task] = bar;
            }

            foreach (var targetTask in orderedTasks)
            {
                if (!barLookup.TryGetValue(targetTask, out var targetBar))
                    continue;

                foreach (var dependencyName in ParseDependencies(targetTask.Dependencies))
                {
                    if (!lookup.TryGetValue(dependencyName, out var dependencyTask))
                        continue;

                    if (!barLookup.TryGetValue(dependencyTask, out var dependencyBar))
                        continue;

                    Links.Add(new GanttLink(dependencyBar, targetBar));
                }
            }

            double totalDays = (ProjectFinish - ProjectStart).TotalDays;
            ScheduleSummary = totalDays <= 0
                ? "Schedule contains a single-day milestone sequence."
                : $"Project spans {(int)Math.Ceiling(totalDays)} days across {Tasks.Count} activities.";
            OnPropertyChanged(nameof(TotalLaborCost));
        }

        private static List<string> ParseDependencies(string dependencies)
        {
            return dependencies
                .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToList();
        }

        private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var task in Tasks)
                {
                    task.PropertyChanged -= OnTaskPropertyChanged;
                    task.PropertyChanged += OnTaskPropertyChanged;
                }
                OnPropertyChanged(nameof(TotalLaborCost));
                return;
            }

            if (e.OldItems != null)
            {
                foreach (GanttTask task in e.OldItems)
                    task.PropertyChanged -= OnTaskPropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (GanttTask task in e.NewItems)
                {
                    AssignColor(task);
                    task.PropertyChanged += OnTaskPropertyChanged;
                }
            }

            OnPropertyChanged(nameof(TotalLaborCost));
        }

        private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GanttTask.TotalCost) || e.PropertyName == nameof(GanttTask.DurationDays))
            {
                OnPropertyChanged(nameof(TotalLaborCost));
            }
        }

        public record GanttBar(GanttTask Task, int RowIndex, double OffsetDays, double DurationDays)
        {
            public bool IsMilestone => Task.IsMilestone;
            public double PercentComplete => Task.PercentComplete;
            public string Workstream => Task.Workstream;
            public double CanvasOffsetDays => IsMilestone ? Math.Max(0, OffsetDays - 0.6) : OffsetDays;
            public double CanvasDurationDays => IsMilestone ? 1.2 : DurationDays;
            public double CenterLineY => RowIndex * 44 + 14;
            public double EndCapDays => CanvasOffsetDays + CanvasDurationDays;
            public Brush StrokeBrush => Task.BorderBrush;
        }

        public record GanttLink(GanttBar From, GanttBar To)
        {
            public int FromRow => From.RowIndex;
            public int ToRow => To.RowIndex;
            public double FromDay => From.EndCapDays;
            public double ToDay => To.CanvasOffsetDays;
            public Brush Stroke => From.StrokeBrush;
            public string Caption => $"{From.Task.Name} → {To.Task.Name}";
        }

        private void AssignColor(GanttTask task)
        {
            if (task.Color.A != 0)
                return;
            task.Color = GenerateColor(_colorSequence++);
        }

        private static Color GenerateColor(int index)
        {
            const double goldenRatioConjugate = 0.618033988749895;
            double hue = (index * goldenRatioConjugate) % 1.0;
            return FromHsl(hue, 0.62, 0.58);
        }

        private static Color FromHsl(double h, double s, double l)
        {
            double r, g, b;

            if (Math.Abs(s) < 0.0001)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h + 1.0 / 3.0);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3.0);
            }

            return Color.FromRgb(
                (byte)Math.Round(Math.Clamp(r, 0, 1) * 255),
                (byte)Math.Round(Math.Clamp(g, 0, 1) * 255),
                (byte)Math.Round(Math.Clamp(b, 0, 1) * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0)
                t += 1;
            if (t > 1)
                t -= 1;
            if (t < 1.0 / 6.0)
                return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0)
                return q;
            if (t < 2.0 / 3.0)
                return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}
