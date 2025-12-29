using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class EadView : UserControl
    {
        private bool _isPanning;
        private Point _lastPanPoint;
        private const double MinZoom = 0.5;
        private const double MaxZoom = 3.0;
        private EadViewModel? _viewModel;

        public EadView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ChartTransform.Matrix = Matrix.Identity;
            UpdateAxisFromTransform();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is EadViewModel oldViewModel)
            {
                oldViewModel.DamageSeries.CollectionChanged -= DamageSeries_CollectionChanged;
            }

            if (e.NewValue is EadViewModel newViewModel)
            {
                _viewModel = newViewModel;
                newViewModel.DamageSeries.CollectionChanged += DamageSeries_CollectionChanged;
            }
            else
            {
                _viewModel = null;
            }

            UpdateAxisFromTransform();
        }

        private void ChartInteractionLayer_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _isPanning = true;
            _lastPanPoint = e.GetPosition(ChartInteractionLayer);
            ChartInteractionLayer.CaptureMouse();
        }

        private void ChartInteractionLayer_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning)
            {
                return;
            }

            var currentPoint = e.GetPosition(ChartInteractionLayer);
            var delta = currentPoint - _lastPanPoint;
            Translate(new Vector(delta.X, delta.Y));
            _lastPanPoint = currentPoint;
        }

        private void ChartInteractionLayer_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _isPanning = false;
            ChartInteractionLayer.ReleaseMouseCapture();
        }

        private void ChartInteractionLayer_OnMouseLeave(object sender, MouseEventArgs e)
        {
            _isPanning = false;
            ChartInteractionLayer.ReleaseMouseCapture();
            UpdateAxisFromTransform();
        }

        private void ChartInteractionLayer_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var position = e.GetPosition(ChartInteractionLayer);
            double zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            Zoom(zoomFactor, position);
            e.Handled = true;
        }

        private void Translate(Vector delta)
        {
            var matrix = ChartTransform.Matrix;
            matrix.Translate(delta.X, delta.Y);
            ChartTransform.Matrix = matrix;
            UpdateAxisFromTransform();
        }

        private void Zoom(double zoomFactor, Point center)
        {
            var matrix = ChartTransform.Matrix;
            double currentScale = matrix.M11;
            double targetScale = Math.Clamp(currentScale * zoomFactor, MinZoom, MaxZoom);
            zoomFactor = targetScale / currentScale;

            matrix.ScaleAt(zoomFactor, zoomFactor, center.X, center.Y);
            ChartTransform.Matrix = matrix;
            UpdateAxisFromTransform();
        }

        private void DamageSeries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(UpdateAxisFromTransform);
        }

        private void UpdateAxisFromTransform()
        {
            _viewModel?.UpdateAxisForTransform(ChartTransform.Matrix);
        }
    }
}
