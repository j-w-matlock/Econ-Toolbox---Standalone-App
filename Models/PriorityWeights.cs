using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EconToolbox.Desktop.Models
{
    public class PriorityWeights : INotifyPropertyChanged
    {
        private double _urgencyWeight = 1.0;
        private double _importanceWeight = 1.0;
        private double _complexityWeight = 1.0;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double UrgencyWeight
        {
            get => _urgencyWeight;
            set
            {
                if (System.Math.Abs(_urgencyWeight - value) > double.Epsilon)
                {
                    _urgencyWeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ImportanceWeight
        {
            get => _importanceWeight;
            set
            {
                if (System.Math.Abs(_importanceWeight - value) > double.Epsilon)
                {
                    _importanceWeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ComplexityWeight
        {
            get => _complexityWeight;
            set
            {
                if (System.Math.Abs(_complexityWeight - value) > double.Epsilon)
                {
                    _complexityWeight = value;
                    OnPropertyChanged();
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
