using System;

namespace EconToolbox.Desktop.Models
{
    public class GanttTask : ObservableObject
    {
        private string _name = string.Empty;
        private string _workstream = string.Empty;
        private DateTime _startDate = DateTime.Today;
        private int _durationDays = 1;
        private DateTime _endDate = DateTime.Today;
        private string _dependencies = string.Empty;
        private double _percentComplete;
        private bool _isMilestone;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Workstream
        {
            get => _workstream;
            set
            {
                if (_workstream == value)
                    return;
                _workstream = value;
                OnPropertyChanged();
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate == value)
                    return;
                _startDate = value;
                OnPropertyChanged();
            }
        }

        public int DurationDays
        {
            get => _durationDays;
            set
            {
                if (_durationDays == value)
                    return;
                _durationDays = Math.Max(0, value);
                OnPropertyChanged();
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate == value)
                    return;
                _endDate = value;
                OnPropertyChanged();
            }
        }

        public string Dependencies
        {
            get => _dependencies;
            set
            {
                if (_dependencies == value)
                    return;
                _dependencies = value;
                OnPropertyChanged();
            }
        }

        public double PercentComplete
        {
            get => _percentComplete;
            set
            {
                var clamped = Math.Clamp(value, 0, 100);
                if (Math.Abs(_percentComplete - clamped) < 0.0001)
                    return;
                _percentComplete = clamped;
                OnPropertyChanged();
            }
        }

        public bool IsMilestone
        {
            get => _isMilestone;
            set
            {
                if (_isMilestone == value)
                    return;
                _isMilestone = value;
                OnPropertyChanged();
            }
        }
    }
}
