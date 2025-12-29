using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EconToolbox.Desktop.Views
{
    public partial class EadView : UserControl
    {
        private bool _isPanning;
        private Point _lastPanPoint;
        private const double MinZoom = 0.5;
        private const double MaxZoom = 3.0;

        public EadView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ChartTransform.Matrix = Matrix.Identity;
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
        }

        private void Zoom(double zoomFactor, Point center)
        {
            var matrix = ChartTransform.Matrix;
            double currentScale = matrix.M11;
            double targetScale = Math.Clamp(currentScale * zoomFactor, MinZoom, MaxZoom);
            zoomFactor = targetScale / currentScale;

            matrix.ScaleAt(zoomFactor, zoomFactor, center.X, center.Y);
            ChartTransform.Matrix = matrix;
        }
    }
}
