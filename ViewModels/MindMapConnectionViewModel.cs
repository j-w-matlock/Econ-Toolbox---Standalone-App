using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace EconToolbox.Desktop.ViewModels
{
    public class MindMapConnectionViewModel : BaseViewModel, IDisposable
    {
        public MindMapConnectionViewModel(MindMapNodeViewModel source, MindMapNodeViewModel target)
        {
            Source = source;
            Target = target;

            Source.PropertyChanged += OnNodePropertyChanged;
            Target.PropertyChanged += OnNodePropertyChanged;

            RaisePositionChanges();
        }

        public MindMapNodeViewModel Source { get; }
        public MindMapNodeViewModel Target { get; }

        private PointCollection _connectorPoints = new();
        private Point _firstBend;
        private Point _secondBend;

        public double StartX => Source.X + Source.VisualWidth / 2;
        public double StartY => Source.Y + Source.VisualHeight / 2;
        public double EndX => Target.X + Target.VisualWidth / 2;
        public double EndY => Target.Y + Target.VisualHeight / 2;

        public PointCollection ConnectorPoints
        {
            get => _connectorPoints;
            private set
            {
                if (!Equals(_connectorPoints, value))
                {
                    _connectorPoints = value;
                    OnPropertyChanged();
                }
            }
        }

        public Point FirstBend
        {
            get => _firstBend;
            private set
            {
                if (_firstBend != value)
                {
                    _firstBend = value;
                    OnPropertyChanged();
                }
            }
        }

        public Point SecondBend
        {
            get => _secondBend;
            private set
            {
                if (_secondBend != value)
                {
                    _secondBend = value;
                    OnPropertyChanged();
                }
            }
        }

        public void Dispose()
        {
            Source.PropertyChanged -= OnNodePropertyChanged;
            Target.PropertyChanged -= OnNodePropertyChanged;
        }

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MindMapNodeViewModel.X) ||
                e.PropertyName == nameof(MindMapNodeViewModel.Y) ||
                e.PropertyName == nameof(MindMapNodeViewModel.VisualWidth) ||
                e.PropertyName == nameof(MindMapNodeViewModel.VisualHeight))
            {
                RaisePositionChanges();
            }
        }

        private void RaisePositionChanges()
        {
            OnPropertyChanged(nameof(StartX));
            OnPropertyChanged(nameof(StartY));
            OnPropertyChanged(nameof(EndX));
            OnPropertyChanged(nameof(EndY));
            ConnectorPoints = CreateConnectorPoints();
        }

        private PointCollection CreateConnectorPoints()
        {
            var sourceRight = Source.X + Source.VisualWidth;
            var sourceLeft = Source.X;
            var sourceMidY = Source.Y + Source.VisualHeight / 2;

            var targetLeft = Target.X;
            var targetRight = Target.X + Target.VisualWidth;
            var targetMidY = Target.Y + Target.VisualHeight / 2;

            bool targetToRight = targetLeft >= sourceRight;

            double startX = targetToRight ? sourceRight : sourceLeft;
            double endX = targetToRight ? targetLeft : targetRight;

            var start = new Point(startX, sourceMidY);
            var end = new Point(endX, targetMidY);

            double midX = Math.Abs(start.X - end.X) < 0.1
                ? start.X
                : (start.X + end.X) / 2.0;

            var points = new List<Point>
            {
                start,
                new Point(midX, start.Y)
            };

            var zigSource = new Point(midX, start.Y);
            var zigTarget = new Point(midX, end.Y);
            var zigPoints = CreateZigZagPoints(zigSource, zigTarget, targetToRight ? 1 : -1);
            points.AddRange(zigPoints);
            points.Add(zigTarget);
            points.Add(end);

            if (points.Count >= 3)
            {
                FirstBend = points[1];
                SecondBend = points[^2];
            }

            return new PointCollection(points);
        }

        private static IEnumerable<Point> CreateZigZagPoints(Point start, Point end, int directionSign)
        {
            var results = new List<Point>();
            double deltaY = end.Y - start.Y;
            double amplitude = 24 * directionSign;

            if (Math.Abs(deltaY) < 12)
            {
                results.Add(new Point(start.X + amplitude, start.Y - 18));
                results.Add(new Point(start.X - amplitude, start.Y + 18));
                return results;
            }

            int segments = Math.Max(2, (int)(Math.Abs(deltaY) / 50));
            if (segments % 2 != 0)
                segments++;

            double step = deltaY / (segments + 1);
            double currentY = start.Y;

            for (int i = 0; i < segments; i++)
            {
                currentY += step;
                double offset = (i % 2 == 0 ? amplitude : -amplitude);
                results.Add(new Point(start.X + offset, currentY));
            }

            return results;
        }
    }
}
