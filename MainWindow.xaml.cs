using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace EconToolbox.Desktop
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            if (IsInsideNestedScrollViewer(e.OriginalSource as DependencyObject, scrollViewer))
            {
                return;
            }

            var targetOffset = scrollViewer.VerticalOffset - e.Delta;
            targetOffset = Math.Clamp(targetOffset, 0, scrollViewer.ScrollableHeight);
            scrollViewer.ScrollToVerticalOffset(targetOffset);
            e.Handled = true;
        }

        private static bool IsInsideNestedScrollViewer(DependencyObject? source, ScrollViewer root)
        {
            var current = source;
            while (current != null && current != root)
            {
                if (current is ScrollViewer)
                {
                    return true;
                }

                current = GetParent(current);
            }

            return false;
        }

        private static DependencyObject? GetParent(DependencyObject? child)
        {
            if (child == null)
            {
                return null;
            }

            if (child is Visual || child is Visual3D)
            {
                var parent = VisualTreeHelper.GetParent(child);
                if (parent != null)
                {
                    return parent;
                }
            }

            return LogicalTreeHelper.GetParent(child);
        }
    }
}
