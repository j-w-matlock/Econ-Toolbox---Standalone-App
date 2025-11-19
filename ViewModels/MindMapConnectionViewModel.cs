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
        private Point _startAnchor;
        private Point _endAnchor;
        private Point _firstBend;
        private Point _secondBend;
        private Point _manualFirstBend;
        private Point _manualSecondBend;
        private bool _hasManualFirstBend;
        private bool _hasManualSecondBend;

        public double StartX => _startAnchor.X;
        public double StartY => _startAnchor.Y;
        public double EndX => _endAnchor.X;
        public double EndY => _endAnchor.Y;

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
            ConnectorPoints = CreateConnectorPoints();
            OnPropertyChanged(nameof(StartX));
            OnPropertyChanged(nameof(StartY));
            OnPropertyChanged(nameof(EndX));
            OnPropertyChanged(nameof(EndY));
        }

        private PointCollection CreateConnectorPoints()
        {
            var previousStart = _startAnchor;
            var previousEnd = _endAnchor;

            (_startAnchor, _endAnchor) = CalculateAnchors();

            if (_hasManualFirstBend)
            {
                _manualFirstBend = TranslateWithAnchorDelta(_manualFirstBend, previousStart, _startAnchor);
            }

            if (_hasManualSecondBend)
            {
                _manualSecondBend = TranslateWithAnchorDelta(_manualSecondBend, previousEnd, _endAnchor);
            }

            var (first, second) = GetBendPoints();

            FirstBend = first;
            SecondBend = second;

            return new PointCollection(new[]
            {
                _startAnchor,
                FirstBend,
                SecondBend,
                _endAnchor
            });
        }

        private (Point first, Point second) GetBendPoints()
        {
            var auto = CreateAutomaticBends(_startAnchor, _endAnchor);
            var first = _hasManualFirstBend ? _manualFirstBend : auto.first;
            var second = _hasManualSecondBend ? _manualSecondBend : auto.second;
            return (first, second);
        }

        public void SetManualBend(int index, Point point)
        {
            if (index == 1)
            {
                _hasManualFirstBend = true;
                _manualFirstBend = point;
            }
            else if (index == 2)
            {
                _hasManualSecondBend = true;
                _manualSecondBend = point;
            }

            ConnectorPoints = new PointCollection(new[]
            {
                _startAnchor,
                index == 1 ? point : FirstBend,
                index == 2 ? point : SecondBend,
                _endAnchor
            });

            if (index == 1)
                FirstBend = point;
            if (index == 2)
                SecondBend = point;
        }

        private (Point first, Point second) CreateAutomaticBends(Point start, Point end)
        {
            if (Math.Abs(start.X - end.X) < 0.01)
            {
                double y1 = start.Y + (end.Y - start.Y) / 3.0;
                double y2 = start.Y + 2 * (end.Y - start.Y) / 3.0;
                return (new Point(start.X, y1), new Point(start.X, y2));
            }

            if (Math.Abs(start.Y - end.Y) < 0.01)
            {
                double x1 = start.X + (end.X - start.X) / 3.0;
                double x2 = start.X + 2 * (end.X - start.X) / 3.0;
                return (new Point(x1, start.Y), new Point(x2, start.Y));
            }

            double midX = (start.X + end.X) / 2.0;
            return (new Point(midX, start.Y), new Point(midX, end.Y));
        }

        private (Point start, Point end) CalculateAnchors()
        {
            var sourceRect = new Rect(Source.X, Source.Y, Source.VisualWidth, Source.VisualHeight);
            var targetRect = new Rect(Target.X, Target.Y, Target.VisualWidth, Target.VisualHeight);

            var sourceCenter = new Point(sourceRect.Left + sourceRect.Width / 2, sourceRect.Top + sourceRect.Height / 2);
            var targetCenter = new Point(targetRect.Left + targetRect.Width / 2, targetRect.Top + targetRect.Height / 2);

            if (Math.Abs(sourceCenter.X - targetCenter.X) < 0.1 || Math.Abs(sourceCenter.Y - targetCenter.Y) < 0.1)
            {
                return (sourceCenter, targetCenter);
            }

            var sourceCorners = new[]
            {
                new Point(sourceRect.Left, sourceRect.Top),
                new Point(sourceRect.Left, sourceRect.Bottom),
                new Point(sourceRect.Right, sourceRect.Top),
                new Point(sourceRect.Right, sourceRect.Bottom)
            };

            var targetCorners = new[]
            {
                new Point(targetRect.Left, targetRect.Top),
                new Point(targetRect.Left, targetRect.Bottom),
                new Point(targetRect.Right, targetRect.Top),
                new Point(targetRect.Right, targetRect.Bottom)
            };

            double bestDistance = double.MaxValue;
            Point bestSource = sourceCorners[0];
            Point bestTarget = targetCorners[0];

            foreach (var source in sourceCorners)
            {
                foreach (var target in targetCorners)
                {
                    var distance = DistanceSquared(source, target);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestSource = source;
                        bestTarget = target;
                    }
                }
            }

            return (bestSource, bestTarget);
        }

        private static Point TranslateWithAnchorDelta(Point bend, Point previousAnchor, Point newAnchor)
        {
            var deltaX = newAnchor.X - previousAnchor.X;
            var deltaY = newAnchor.Y - previousAnchor.Y;
            return new Point(bend.X + deltaX, bend.Y + deltaY);
        }

        private static double DistanceSquared(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
