using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;

namespace EconToolbox.Desktop.ViewModels
{
    public class DrawingViewModel : BaseViewModel
    {
        private readonly RelayCommand _clearCommand;
        private readonly RelayCommand _undoCommand;
        private Color _selectedColor = Colors.SteelBlue;
        private double _penThickness = 3;
        private bool _suppressStrokeNotification;

        public DrawingViewModel()
        {
            Palette = new ObservableCollection<ColorOption>
            {
                new("Steel", Colors.SteelBlue),
                new("Crimson", Colors.Crimson),
                new("Emerald", Color.FromRgb(52, 168, 83)),
                new("Amber", Color.FromRgb(255, 191, 0)),
                new("Graphite", Color.FromRgb(66, 66, 66)),
                new("Violet", Color.FromRgb(123, 97, 255))
            };

            DrawingAttributes = new DrawingAttributes
            {
                Color = _selectedColor,
                Width = _penThickness,
                Height = _penThickness,
                FitToCurve = true
            };

            Strokes = new StrokeCollection();
            Strokes.StrokesChanged += (_, _) => UpdateStrokeState();

            _clearCommand = new RelayCommand(Clear, () => HasStrokes);
            _undoCommand = new RelayCommand(Undo, () => HasStrokes);
        }

        public ObservableCollection<ColorOption> Palette { get; }

        public StrokeCollection Strokes { get; }

        public DrawingAttributes DrawingAttributes { get; }

        public ICommand ClearCommand => _clearCommand;
        public ICommand UndoCommand => _undoCommand;

        public double CanvasWidth { get; set; } = 820;
        public double CanvasHeight { get; set; } = 520;

        public bool HasStrokes => Strokes.Count > 0;

        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (_selectedColor == value)
                    return;
                _selectedColor = value;
                DrawingAttributes.Color = value;
                OnPropertyChanged();
            }
        }

        public double PenThickness
        {
            get => _penThickness;
            set
            {
                var clamped = Math.Clamp(value, 1, 24);
                if (Math.Abs(_penThickness - clamped) < 0.001)
                    return;
                _penThickness = clamped;
                DrawingAttributes.Width = _penThickness;
                DrawingAttributes.Height = _penThickness;
                OnPropertyChanged();
            }
        }

        private void Clear()
        {
            _suppressStrokeNotification = true;
            Strokes.Clear();
            _suppressStrokeNotification = false;
            UpdateStrokeState();
        }

        private void Undo()
        {
            if (Strokes.Count == 0)
                return;
            _suppressStrokeNotification = true;
            Strokes.RemoveAt(Strokes.Count - 1);
            _suppressStrokeNotification = false;
            UpdateStrokeState();
        }

        private void UpdateStrokeState()
        {
            if (_suppressStrokeNotification)
                return;

            OnPropertyChanged(nameof(HasStrokes));
            _clearCommand.NotifyCanExecuteChanged();
            _undoCommand.NotifyCanExecuteChanged();
        }

        public IEnumerable<IReadOnlyList<Point>> ExportStrokes()
        {
            return Strokes
                .Select(stroke => stroke.StylusPoints.Select(p => (Point)p).ToList())
                .ToList();
        }

        public record ColorOption(string Name, Color Color)
        {
            public Brush Swatch => new SolidColorBrush(Color);
        }
    }
}
