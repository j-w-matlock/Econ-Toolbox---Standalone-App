using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace EconToolbox.Desktop.Behaviors
{
    public static class MouseWheelRoutingBehavior
    {
        public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(MouseWheelRoutingBehavior),
            new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableProperty);
        }

        public static void SetEnable(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableProperty, value);
        }

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                element.PreviewMouseWheel += OnPreviewMouseWheel;
            }
            else
            {
                element.PreviewMouseWheel -= OnPreviewMouseWheel;
            }
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null)
            {
                return;
            }

            var scrollViewer = FindScrollViewer(originalSource);
            if (scrollViewer == null)
            {
                return;
            }

            if (CanScroll(scrollViewer, e.Delta))
            {
                return;
            }

            var parentScrollViewer = FindParentScrollViewer(scrollViewer);
            if (parentScrollViewer == null)
            {
                return;
            }

            e.Handled = true;
            var routedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = scrollViewer
            };
            parentScrollViewer.RaiseEvent(routedEvent);
        }

        private static bool CanScroll(ScrollViewer scrollViewer, int delta)
        {
            if (scrollViewer.ScrollableHeight <= 0)
            {
                return false;
            }

            if (delta < 0)
            {
                return scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
            }

            if (delta > 0)
            {
                return scrollViewer.VerticalOffset > 0;
            }

            return false;
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject? current)
        {
            while (current != null)
            {
                if (current is ScrollViewer viewer)
                {
                    return viewer;
                }

                current = GetParent(current);
            }

            return null;
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject current)
        {
            var parent = GetParent(current);
            while (parent != null)
            {
                if (parent is ScrollViewer viewer)
                {
                    return viewer;
                }

                parent = GetParent(parent);
            }

            return null;
        }

        private static DependencyObject? GetParent(DependencyObject current)
        {
            if (current is Visual || current is Visual3D)
            {
                var visualParent = VisualTreeHelper.GetParent(current);
                if (visualParent != null)
                {
                    return visualParent;
                }
            }

            return LogicalTreeHelper.GetParent(current);
        }
    }
}
