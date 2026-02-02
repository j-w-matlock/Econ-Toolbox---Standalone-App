using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class ConceptualModelView : UserControl
    {
        private bool _isDragging;
        private Point _dragStart;
        private Point _nodeStart;
        private ConceptualNode? _dragNode;

        public ConceptualModelView()
        {
            InitializeComponent();
        }

        private void OnNodeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ConceptualNode node)
            {
                return;
            }

            _dragNode = node;
            _dragStart = e.GetPosition(ConceptualCanvas);
            _nodeStart = new Point(node.X, node.Y);
            _isDragging = true;
            element.CaptureMouse();

            if (DataContext is ConceptualModelViewModel viewModel)
            {
                viewModel.SelectedNode = node;
            }

            e.Handled = true;
        }

        private void OnNodeMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _dragNode == null)
            {
                return;
            }

            var currentPosition = e.GetPosition(ConceptualCanvas);
            var delta = currentPosition - _dragStart;
            _dragNode.X = _nodeStart.X + delta.X;
            _dragNode.Y = _nodeStart.Y + delta.Y;
            e.Handled = true;
        }

        private void OnNodeMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            if (sender is UIElement element)
            {
                element.ReleaseMouseCapture();
            }

            _isDragging = false;
            _dragNode = null;
            e.Handled = true;
        }

        private void OnLinkRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ConceptualLink link)
            {
                return;
            }

            if (DataContext is not ConceptualModelViewModel viewModel)
            {
                return;
            }

            var position = e.GetPosition(ConceptualCanvas);
            viewModel.AddVertexAt(link, position);
            e.Handled = true;
        }

        private void OnLinkMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ConceptualLink link)
            {
                return;
            }

            if (DataContext is ConceptualModelViewModel viewModel)
            {
                viewModel.SelectedLink = link;
            }

            e.Handled = true;
        }
    }
}
