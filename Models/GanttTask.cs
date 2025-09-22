using System;
using System.Windows.Media;

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
        private double _laborCostPerDay;
        private Color _color = Colors.Transparent;
        private SolidColorBrush _colorBrush = CreateFrozenBrush(Colors.Transparent);
        private SolidColorBrush _borderBrush = CreateFrozenBrush(Colors.Transparent);

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
                OnPropertyChanged(nameof(TotalCost));
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

        public double LaborCostPerDay
        {
            get => _laborCostPerDay;
            set
            {
                var sanitized = Math.Max(0, value);
                if (Math.Abs(_laborCostPerDay - sanitized) < 0.0001)
                    return;
                _laborCostPerDay = sanitized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalCost));
            }
        }

        public double TotalCost => Math.Max(0, _durationDays) * _laborCostPerDay;

        public Color Color
        {
            get => _color;
            set
            {
                if (_color == value)
                    return;
                _color = value;
                _colorBrush = CreateFrozenBrush(value);
                _borderBrush = CreateFrozenBrush(DarkenColor(value, 0.25));
                OnPropertyChanged();
                OnPropertyChanged(nameof(ColorBrush));
                OnPropertyChanged(nameof(BorderBrush));
            }
        }

        public Brush ColorBrush => _colorBrush;

        public Brush BorderBrush => _borderBrush;

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            if (brush.CanFreeze)
                brush.Freeze();
            return brush;
        }

        private static Color DarkenColor(Color color, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            byte r = (byte)Math.Clamp(color.R * (1 - amount), 0, 255);
            byte g = (byte)Math.Clamp(color.G * (1 - amount), 0, 255);
            byte b = (byte)Math.Clamp(color.B * (1 - amount), 0, 255);
            return Color.FromRgb(r, g, b);
        }
    }
}
