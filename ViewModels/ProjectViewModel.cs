using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EconToolbox.Desktop.Helpers;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.ViewModels
{
    public class ProjectViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<ProjectTask> _tasks;
        private PriorityWeights _weights;
        private string _newTaskName = string.Empty;
        private int _newTaskUrgency;
        private int _newTaskImportance;
        private int _newTaskComplexity;

        public ProjectViewModel()
        {
            _tasks = new ObservableCollection<ProjectTask>();
            _weights = new PriorityWeights();

            AddTaskCommand = new RelayCommand(AddTask);
            RecalculatePrioritiesCommand = new RelayCommand(RecalculateAndSort);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ProjectTask> Tasks
        {
            get => _tasks;
            set
            {
                if (!ReferenceEquals(_tasks, value))
                {
                    _tasks = value;
                    OnPropertyChanged();
                }
            }
        }

        public PriorityWeights Weights
        {
            get => _weights;
            set
            {
                if (!ReferenceEquals(_weights, value))
                {
                    _weights = value;
                    OnPropertyChanged();
                }
            }
        }

        public string NewTaskName
        {
            get => _newTaskName;
            set
            {
                if (_newTaskName != value)
                {
                    _newTaskName = value;
                    OnPropertyChanged();
                }
            }
        }

        public int NewTaskUrgency
        {
            get => _newTaskUrgency;
            set
            {
                if (_newTaskUrgency != value)
                {
                    _newTaskUrgency = value;
                    OnPropertyChanged();
                }
            }
        }

        public int NewTaskImportance
        {
            get => _newTaskImportance;
            set
            {
                if (_newTaskImportance != value)
                {
                    _newTaskImportance = value;
                    OnPropertyChanged();
                }
            }
        }

        public int NewTaskComplexity
        {
            get => _newTaskComplexity;
            set
            {
                if (_newTaskComplexity != value)
                {
                    _newTaskComplexity = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand AddTaskCommand { get; }

        public ICommand RecalculatePrioritiesCommand { get; }

        public void RecalculateAndSort()
        {
            foreach (var task in Tasks)
            {
                var score = (task.Urgency * Weights.UrgencyWeight)
                    + (task.Importance * Weights.ImportanceWeight)
                    + (task.Complexity * Weights.ComplexityWeight);

                task.SetPriorityScore(score);
            }

            Tasks = new ObservableCollection<ProjectTask>(Tasks
                .OrderByDescending(task => task.PriorityScore));
        }

        private void AddTask()
        {
            var task = new ProjectTask
            {
                TaskName = NewTaskName,
                Urgency = NewTaskUrgency,
                Importance = NewTaskImportance,
                Complexity = NewTaskComplexity
            };

            Tasks.Add(task);
            NewTaskName = string.Empty;
            NewTaskUrgency = 0;
            NewTaskImportance = 0;
            NewTaskComplexity = 0;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
