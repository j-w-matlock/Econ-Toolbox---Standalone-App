using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EconToolbox.Desktop.Models
{
    public class ProjectTask : INotifyPropertyChanged
    {
        private string _taskName = string.Empty;
        private int _urgency;
        private int _importance;
        private int _complexity;
        private double _priorityScore;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string TaskName
        {
            get => _taskName;
            set
            {
                if (_taskName != value)
                {
                    _taskName = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Urgency
        {
            get => _urgency;
            set
            {
                if (_urgency != value)
                {
                    _urgency = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Importance
        {
            get => _importance;
            set
            {
                if (_importance != value)
                {
                    _importance = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Complexity
        {
            get => _complexity;
            set
            {
                if (_complexity != value)
                {
                    _complexity = value;
                    OnPropertyChanged();
                }
            }
        }

        public double PriorityScore
        {
            get => _priorityScore;
            private set
            {
                if (System.Math.Abs(_priorityScore - value) > double.Epsilon)
                {
                    _priorityScore = value;
                    OnPropertyChanged();
                }
            }
        }

        public void SetPriorityScore(double score)
        {
            PriorityScore = score;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
