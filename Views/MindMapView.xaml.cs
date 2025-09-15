using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class MindMapView : UserControl
    {
        private Canvas? _nodeCanvas;
        private Point _lastCanvasContextPosition = new(320, 240);
        private MindMapNodeViewModel? _draggingNode;
        private FrameworkElement? _draggingElement;
        private Point _dragOffset;

        public MindMapView()
        {
            InitializeComponent();
        }

        private MindMapViewModel? ViewModel => DataContext as MindMapViewModel;

        private void OnNodesLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                _nodeCanvas = itemsControl.ItemsPanelRoot as Canvas;
            }
        }

        private void OnCanvasRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var reference = (IInputElement?)_nodeCanvas ?? (IInputElement)sender;
            _lastCanvasContextPosition = e.GetPosition(reference);
        }

        private void OnCanvasContextMenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu)
                return;

            var vm = ViewModel;
            bool hasSelection = vm?.SelectedNode != null;

            if (menu.Items.Count > 1 && menu.Items[1] is MenuItem child)
                child.IsEnabled = hasSelection;
            if (menu.Items.Count > 2 && menu.Items[2] is MenuItem sibling)
                sibling.IsEnabled = hasSelection;
        }

        private void OnCanvasAddRoot(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddRootAt(_lastCanvasContextPosition);
        }

        private void OnCanvasAddChild(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedNode is MindMapNodeViewModel selected)
            {
                ViewModel.AddChildAt(selected, _lastCanvasContextPosition);
            }
        }

        private void OnCanvasAddSibling(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedNode is MindMapNodeViewModel selected)
            {
                ViewModel.AddSiblingAt(selected, _lastCanvasContextPosition);
            }
        }

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not MindMapNodeViewModel node)
                return;

            if (ViewModel != null)
                ViewModel.SelectedNode = node;

            var reference = (IInputElement?)_nodeCanvas ?? element;
            var position = e.GetPosition(reference);
            _dragOffset = new Point(position.X - node.X, position.Y - node.Y);

            _draggingNode = node;
            _draggingElement = element;
            element.CaptureMouse();
            e.Handled = true;
        }

        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingNode == null || _draggingElement == null)
                return;

            if (!_draggingElement.IsMouseCaptured)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndDrag();
                return;
            }

            var reference = (IInputElement?)_nodeCanvas ?? _draggingElement;
            var position = e.GetPosition(reference);
            var newX = position.X - _dragOffset.X;
            var newY = position.Y - _dragOffset.Y;

            _draggingNode.X = Math.Max(0, newX);
            _draggingNode.Y = Math.Max(0, newY);
            e.Handled = true;
        }

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
            e.Handled = true;
        }

        private void EndDrag()
        {
            if (_draggingElement != null && _draggingElement.IsMouseCaptured)
            {
                _draggingElement.ReleaseMouseCapture();
            }

            _draggingElement = null;
            _draggingNode = null;
        }

        private void Node_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MindMapNodeViewModel node)
            {
                if (ViewModel != null)
                    ViewModel.SelectedNode = node;

                var reference = (IInputElement?)_nodeCanvas ?? element;
                _lastCanvasContextPosition = e.GetPosition(reference);
            }
        }

        private void OnNodeContextMenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu { DataContext: MindMapNodeViewModel node })
            {
                if (ViewModel != null)
                    ViewModel.SelectedNode = node;
            }
        }

        private void OnNodeAddChild(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: MindMapNodeViewModel node })
            {
                ViewModel?.AddChildAt(node, null);
            }
        }

        private void OnNodeAddSibling(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: MindMapNodeViewModel node })
            {
                ViewModel?.AddSiblingAt(node, null);
            }
        }

        private void OnNodeRemove(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: MindMapNodeViewModel node } && ViewModel != null)
            {
                ViewModel.SelectedNode = node;
                if (ViewModel.RemoveNodeCommand.CanExecute(null))
                    ViewModel.RemoveNodeCommand.Execute(null);
            }
        }

        private void Node_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MindMapNodeViewModel node)
            {
                if (Math.Abs(node.VisualWidth - e.NewSize.Width) > 0.1)
                    node.VisualWidth = e.NewSize.Width;
                if (Math.Abs(node.VisualHeight - e.NewSize.Height) > 0.1)
                    node.VisualHeight = e.NewSize.Height;
            }
        }
    }
}
