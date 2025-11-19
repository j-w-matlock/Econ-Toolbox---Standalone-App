using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        private readonly List<Point> _manualBends = new();
        private IReadOnlyList<Point> _currentBends = Array.Empty<Point>();
        private AnchorSnap? _sourceAnchorSnap;
        private AnchorSnap? _targetAnchorSnap;

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

        public IReadOnlyList<Point> BendPoints => _currentBends;

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
            OnPropertyChanged(nameof(BendPoints));
        }

        private PointCollection CreateConnectorPoints()
        {
            var previousStart = _startAnchor;
            var previousEnd = _endAnchor;

            (_startAnchor, _endAnchor) = CalculateAnchors();

            TranslateManualBends(previousStart, previousEnd);

            var bends = GetBendPoints();
            _currentBends = bends;

            var points = new List<Point>(bends.Count + 2) { _startAnchor };
            points.AddRange(bends);
            points.Add(_endAnchor);
            return new PointCollection(points);
        }

        private void TranslateManualBends(Point previousStart, Point previousEnd)
        {
            if (_manualBends.Count == 0)
                return;

            var deltaStart = new Vector(_startAnchor.X - previousStart.X, _startAnchor.Y - previousStart.Y);
            var deltaEnd = new Vector(_endAnchor.X - previousEnd.X, _endAnchor.Y - previousEnd.Y);

            for (int i = 0; i < _manualBends.Count; i++)
            {
                var bend = _manualBends[i];
                var distanceToStart = DistanceSquared(bend, previousStart);
                var distanceToEnd = DistanceSquared(bend, previousEnd);
                var delta = distanceToStart <= distanceToEnd ? deltaStart : deltaEnd;
                _manualBends[i] = new Point(bend.X + delta.X, bend.Y + delta.Y);
            }
        }

        private IReadOnlyList<Point> GetBendPoints()
        {
            if (_manualBends.Count > 0)
                return _manualBends.ToList();

            var auto = CreateAutomaticBends(_startAnchor, _endAnchor);
            return new List<Point> { auto.first, auto.second };
        }

        public void SetManualBend(int index, Point point)
        {
            EnsureManualBendCapacity(index + 1);
            _manualBends[index] = point;
            RaisePositionChanges();
        }

        public void AddManualVertex(Point point)
        {
            if (_manualBends.Count == 0)
            {
                var auto = CreateAutomaticBends(_startAnchor, _endAnchor);
                _manualBends.Add(auto.first);
                _manualBends.Add(auto.second);
            }

            _manualBends.Add(point);
            RaisePositionChanges();
        }

        public void RemoveNearestVertex(Point point, double thresholdSquared)
        {
            if (_manualBends.Count == 0)
                return;

            int bestIndex = -1;
            double bestDistance = thresholdSquared;

            for (int i = 0; i < _manualBends.Count; i++)
            {
                var distance = DistanceSquared(_manualBends[i], point);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                _manualBends.RemoveAt(bestIndex);
                RaisePositionChanges();
            }
        }

        public void ResetManualVertices()
        {
            if (_manualBends.Count == 0 && _sourceAnchorSnap == null && _targetAnchorSnap == null)
                return;

            _manualBends.Clear();
            _sourceAnchorSnap = null;
            _targetAnchorSnap = null;
            RaisePositionChanges();
        }

        public void SetAnchor(ConnectionEnd end, Point draggedPosition)
        {
            var rect = end == ConnectionEnd.Source
                ? new Rect(Source.X, Source.Y, Source.VisualWidth, Source.VisualHeight)
                : new Rect(Target.X, Target.Y, Target.VisualWidth, Target.VisualHeight);

            var closest = FindClosestAnchor(rect, draggedPosition);

            if (end == ConnectionEnd.Source)
                _sourceAnchorSnap = closest;
            else
                _targetAnchorSnap = closest;

            RaisePositionChanges();
        }

        private static AnchorSnap FindClosestAnchor(Rect rect, Point to)
        {
            var anchors = new Dictionary<AnchorSnap, Point>
            {
                { AnchorSnap.Center, new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2) },
                { AnchorSnap.TopLeft, new Point(rect.Left, rect.Top) },
                { AnchorSnap.TopRight, new Point(rect.Right, rect.Top) },
                { AnchorSnap.BottomLeft, new Point(rect.Left, rect.Bottom) },
                { AnchorSnap.BottomRight, new Point(rect.Right, rect.Bottom) }
            };

            AnchorSnap best = AnchorSnap.Center;
            double bestDistance = double.MaxValue;

            foreach (var kvp in anchors)
            {
                var distance = DistanceSquared(kvp.Value, to);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = kvp.Key;
                }
            }

            return best;
        }

        private void EnsureManualBendCapacity(int size)
        {
            if (_manualBends.Count >= size)
                return;

            if (_manualBends.Count == 0)
            {
                var auto = CreateAutomaticBends(_startAnchor, _endAnchor);
                _manualBends.Add(auto.first);
                _manualBends.Add(auto.second);
            }

            while (_manualBends.Count < size)
            {
                _manualBends.Add(_manualBends[^1]);
            }
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

            if (_sourceAnchorSnap is AnchorSnap sourceSnap)
                sourceCenter = GetAnchorPoint(sourceRect, sourceSnap);
            if (_targetAnchorSnap is AnchorSnap targetSnap)
                targetCenter = GetAnchorPoint(targetRect, targetSnap);

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

        private static Point GetAnchorPoint(Rect rect, AnchorSnap snap) => snap switch
        {
            AnchorSnap.Center => new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2),
            AnchorSnap.TopLeft => new Point(rect.Left, rect.Top),
            AnchorSnap.TopRight => new Point(rect.Right, rect.Top),
            AnchorSnap.BottomLeft => new Point(rect.Left, rect.Bottom),
            AnchorSnap.BottomRight => new Point(rect.Right, rect.Bottom),
            _ => new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2)
        };

        private static double DistanceSquared(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }

    public enum ConnectionEnd
    {
        Source,
        Target
    }

    public enum AnchorSnap
    {
        Center,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
