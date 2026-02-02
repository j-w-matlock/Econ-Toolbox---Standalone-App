using System;

namespace EconToolbox.Desktop.Models
{
    public class ConceptualVertex : ObservableObject
    {
        private double _x;
        private double _y;
        private bool _isSelected;

        public double X
        {
            get => _x;
            set
            {
                if (Math.Abs(_x - value) < 0.01)
                {
                    return;
                }

                _x = value;
                OnPropertyChanged();
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                if (Math.Abs(_y - value) < 0.01)
                {
                    return;
                }

                _y = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }
}
