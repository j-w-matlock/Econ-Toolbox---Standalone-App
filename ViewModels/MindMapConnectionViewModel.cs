using System;
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

            double midX;
            if (Math.Abs(start.X - end.X) < 0.1)
            {
                midX = start.X;
            }
            else
            {
                midX = (start.X + end.X) / 2;
            }

            return new PointCollection
            {
                start,
                new Point(midX, start.Y),
                new Point(midX, end.Y),
                end
            };
        }
    }
}
