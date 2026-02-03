using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EconToolbox.Desktop.Behaviors
{
    public static class ChartCopyBehavior
    {
        private const string CopyMenuItemTag = "ChartCopyBehavior.CopyMenuItem";

        public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(ChartCopyBehavior),
            new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
            {
                return;
            }

            if (e.NewValue is true)
            {
                AttachContextMenu(element);
            }
        }

        private static void AttachContextMenu(FrameworkElement element)
        {
            var contextMenu = element.ContextMenu ?? new ContextMenu();

            if (contextMenu.Items.OfType<MenuItem>().Any(item => Equals(item.Tag, CopyMenuItemTag)))
            {
                element.ContextMenu ??= contextMenu;
                return;
            }

            var menuItem = new MenuItem
            {
                Header = "Copy",
                Tag = CopyMenuItemTag
            };

            menuItem.Click += (_, _) => CopyElementToClipboard(element);
            contextMenu.Items.Insert(0, menuItem);

            element.ContextMenu ??= contextMenu;
        }

        private static void CopyElementToClipboard(FrameworkElement element)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return;
            }

            try
            {
                var dpi = VisualTreeHelper.GetDpi(element);
                int pixelWidth = (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX);
                int pixelHeight = (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY);

                var renderTarget = new RenderTargetBitmap(
                    pixelWidth,
                    pixelHeight,
                    dpi.PixelsPerInchX,
                    dpi.PixelsPerInchY,
                    PixelFormats.Pbgra32);

                var drawingVisual = new DrawingVisual();
                using (var context = drawingVisual.RenderOpen())
                {
                    context.DrawRectangle(
                        new VisualBrush(element),
                        null,
                        new Rect(new Point(), new Size(element.ActualWidth, element.ActualHeight)));
                }

                renderTarget.Render(drawingVisual);
                Clipboard.SetImage(renderTarget);
            }
            catch (Exception)
            {
                // Ignore clipboard failures (e.g., clipboard is locked by another process).
            }
        }
    }
}
