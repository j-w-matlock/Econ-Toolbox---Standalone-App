using System;
using System.Windows.Media;

namespace EconToolbox.Desktop.Models
{
    public enum ConceptualNodeShape
    {
        Circle,
        RoundedRectangle,
        Rectangle
    }

    public class ConceptualNode : ObservableObject
    {
        private string _name = "New Node";
        private double _x;
        private double _y;
        private double _width = 90;
        private double _height = 90;
        private Brush _fill = new SolidColorBrush(Color.FromRgb(210, 230, 246));
        private Brush _stroke = new SolidColorBrush(Color.FromRgb(54, 95, 160));
        private double _strokeThickness = 2;
        private string? _imagePath;
        private ConceptualNodeShape _shape = ConceptualNodeShape.Circle;
        private double _cornerRadius = 45;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                OnPropertyChanged();
            }
        }

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

        public double Width
        {
            get => _width;
            set
            {
                if (Math.Abs(_width - value) < 0.01)
                {
                    return;
                }

                _width = Math.Max(40, value);
                OnPropertyChanged();
                UpdateCornerRadius();
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                if (Math.Abs(_height - value) < 0.01)
                {
                    return;
                }

                _height = Math.Max(40, value);
                OnPropertyChanged();
                UpdateCornerRadius();
            }
        }

        public Brush Fill
        {
            get => _fill;
            set
            {
                if (Equals(_fill, value))
                {
                    return;
                }

                _fill = value;
                OnPropertyChanged();
            }
        }

        public Brush Stroke
        {
            get => _stroke;
            set
            {
                if (Equals(_stroke, value))
                {
                    return;
                }

                _stroke = value;
                OnPropertyChanged();
            }
        }

        public double StrokeThickness
        {
            get => _strokeThickness;
            set
            {
                if (Math.Abs(_strokeThickness - value) < 0.01)
                {
                    return;
                }

                _strokeThickness = Math.Max(1, value);
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

        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                if (_imagePath == value)
                {
                    return;
                }

                _imagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasImage));
            }
        }

        public bool HasImage => !string.IsNullOrWhiteSpace(_imagePath);

        public ConceptualNodeShape Shape
        {
            get => _shape;
            set
            {
                if (_shape == value)
                {
                    return;
                }

                _shape = value;
                OnPropertyChanged();
                UpdateCornerRadius();
            }
        }

        public double CornerRadius
        {
            get => _cornerRadius;
            private set
            {
                if (Math.Abs(_cornerRadius - value) < 0.01)
                {
                    return;
                }

                _cornerRadius = value;
                OnPropertyChanged();
            }
        }

        private void UpdateCornerRadius()
        {
            CornerRadius = Shape switch
            {
                ConceptualNodeShape.Circle => Math.Min(Width, Height) / 2,
                ConceptualNodeShape.RoundedRectangle => 14,
                _ => 0
            };
        }
    }
}
