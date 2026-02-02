using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace EconToolbox.Desktop.Models
{
    public class ConceptualLink : ObservableObject
    {
        private ConceptualNode? _startNode;
        private ConceptualNode? _endNode;
        private Brush _stroke = new SolidColorBrush(Color.FromRgb(67, 146, 100));
        private double _strokeThickness = 2;
        private DoubleCollection _dashArray = new();
        private PointCollection _points = new();
        private string _label = string.Empty;
        private Brush _labelBrush = new SolidColorBrush(Color.FromRgb(33, 33, 33));
        private double _labelFontSize = 11;
        private bool _labelIsBold;
        private Point _labelPosition;
        private bool _isUpdatingPoints;
        private bool _isSelected;

        public ConceptualLink(ConceptualNode? startNode, ConceptualNode? endNode)
        {
            Vertices = new ObservableCollection<ConceptualVertex>();
            Vertices.CollectionChanged += OnVerticesCollectionChanged;
            StartNode = startNode;
            EndNode = endNode;
            UpdatePoints();
        }

        public ConceptualNode? StartNode
        {
            get => _startNode;
            set
            {
                if (ReferenceEquals(_startNode, value))
                {
                    return;
                }

                if (_startNode != null)
                {
                    _startNode.PropertyChanged -= OnNodePropertyChanged;
                }

                _startNode = value;
                if (_startNode != null)
                {
                    _startNode.PropertyChanged += OnNodePropertyChanged;
                }

                OnPropertyChanged();
                UpdatePoints();
            }
        }

        public ConceptualNode? EndNode
        {
            get => _endNode;
            set
            {
                if (ReferenceEquals(_endNode, value))
                {
                    return;
                }

                if (_endNode != null)
                {
                    _endNode.PropertyChanged -= OnNodePropertyChanged;
                }

                _endNode = value;
                if (_endNode != null)
                {
                    _endNode.PropertyChanged += OnNodePropertyChanged;
                }

                OnPropertyChanged();
                UpdatePoints();
            }
        }

        public ObservableCollection<ConceptualVertex> Vertices { get; }

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
                if (System.Math.Abs(_strokeThickness - value) < 0.01)
                {
                    return;
                }

                _strokeThickness = System.Math.Max(1, value);
                OnPropertyChanged();
            }
        }

        public DoubleCollection DashArray
        {
            get => _dashArray;
            set
            {
                if (Equals(_dashArray, value))
                {
                    return;
                }

                _dashArray = value ?? new DoubleCollection();
                OnPropertyChanged();
            }
        }

        public PointCollection Points
        {
            get => _points;
            private set
            {
                if (Equals(_points, value))
                {
                    return;
                }

                _points = value;
                OnPropertyChanged();
            }
        }

        public string Label
        {
            get => _label;
            set
            {
                if (_label == value)
                {
                    return;
                }

                _label = value;
                OnPropertyChanged();
            }
        }

        public Brush LabelBrush
        {
            get => _labelBrush;
            set
            {
                if (Equals(_labelBrush, value))
                {
                    return;
                }

                _labelBrush = value;
                OnPropertyChanged();
            }
        }

        public double LabelFontSize
        {
            get => _labelFontSize;
            set
            {
                if (System.Math.Abs(_labelFontSize - value) < 0.01)
                {
                    return;
                }

                _labelFontSize = System.Math.Max(8, value);
                OnPropertyChanged();
            }
        }

        public bool LabelIsBold
        {
            get => _labelIsBold;
            set
            {
                if (_labelIsBold == value)
                {
                    return;
                }

                _labelIsBold = value;
                OnPropertyChanged();
            }
        }

        public Point LabelPosition
        {
            get => _labelPosition;
            private set
            {
                if (_labelPosition.Equals(value))
                {
                    return;
                }

                _labelPosition = value;
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

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ConceptualNode.X) or nameof(ConceptualNode.Y) or nameof(ConceptualNode.Width) or nameof(ConceptualNode.Height))
            {
                UpdatePoints();
            }
        }

        private void OnVerticesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is ConceptualVertex vertex)
                    {
                        vertex.PropertyChanged -= OnVertexPropertyChanged;
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is ConceptualVertex vertex)
                    {
                        vertex.PropertyChanged += OnVertexPropertyChanged;
                    }
                }
            }

            UpdatePoints();
        }

        private void OnVertexPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ConceptualVertex.X) or nameof(ConceptualVertex.Y))
            {
                UpdatePoints();
            }
        }

        public void UpdatePoints()
        {
            if (_isUpdatingPoints)
            {
                return;
            }

            _isUpdatingPoints = true;
            try
            {
                var points = new PointCollection();
                if (StartNode != null)
                {
                    points.Add(GetCenter(StartNode));
                }

                foreach (var vertex in Vertices)
                {
                    points.Add(new Point(vertex.X, vertex.Y));
                }

                if (EndNode != null)
                {
                    points.Add(GetCenter(EndNode));
                }

                if (!ArePointsEqual(_points, points))
                {
                    Points = points;
                }

                var labelPosition = CalculateLabelPosition(points);
                if (!_labelPosition.Equals(labelPosition))
                {
                    LabelPosition = labelPosition;
                }
            }
            finally
            {
                _isUpdatingPoints = false;
            }
        }

        private static Point GetCenter(ConceptualNode node)
        {
            return new Point(node.X + node.Width / 2, node.Y + node.Height / 2);
        }

        private static Point CalculateLabelPosition(PointCollection points)
        {
            if (points.Count == 0)
            {
                return new Point();
            }

            var totalX = 0d;
            var totalY = 0d;
            foreach (var point in points)
            {
                totalX += point.X;
                totalY += point.Y;
            }

            return new Point(totalX / points.Count, totalY / points.Count);
        }

        private static bool ArePointsEqual(PointCollection? left, PointCollection? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (!left[i].Equals(right[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
