using System;

namespace EconToolbox.Desktop.Models
{
    public class ConceptualVertex : ObservableObject
    {
        private double _x;
        private double _y;

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
    }
}
