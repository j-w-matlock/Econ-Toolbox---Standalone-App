using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EconToolbox.Desktop.Behaviors
{
    public static class ChartCopyBehavior
    {
        private const string CopyDarkMenuItemTag = "ChartCopyBehavior.CopyMenuItem.Dark";
        private const string CopyLightMenuItemTag = "ChartCopyBehavior.CopyMenuItem.Light";

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
                element.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;
                element.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            }
        }

        private static void AttachContextMenu(FrameworkElement element)
        {
            var contextMenu = element.ContextMenu ?? new ContextMenu();

            if (contextMenu.Items.OfType<MenuItem>().Any(item => Equals(item.Tag, CopyDarkMenuItemTag) || Equals(item.Tag, CopyLightMenuItemTag)))
            {
                element.ContextMenu ??= contextMenu;
                return;
            }

            var darkMenuItem = new MenuItem
            {
                Header = "Copy dark-mode",
                Tag = CopyDarkMenuItemTag
            };
            darkMenuItem.Click += (_, _) => CopyElementToClipboard(element, ChartCopyMode.Dark);

            var lightMenuItem = new MenuItem
            {
                Header = "Copy light-mode",
                Tag = CopyLightMenuItemTag
            };
            lightMenuItem.Click += (_, _) => CopyElementToClipboard(element, ChartCopyMode.Light);

            contextMenu.Items.Insert(0, lightMenuItem);
            contextMenu.Items.Insert(0, darkMenuItem);

            element.ContextMenu ??= contextMenu;
        }

        private static void OnPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            var contextMenu = element.ContextMenu;
            if (contextMenu == null)
            {
                return;
            }

            contextMenu.PlacementTarget = element;
            contextMenu.Placement = PlacementMode.MousePoint;
            if (!contextMenu.IsOpen)
            {
                contextMenu.IsOpen = true;
            }
        }

        private static void CopyElementToClipboard(FrameworkElement element, ChartCopyMode mode)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return;
            }

            try
            {
                var bitmap = RenderElementBitmap(element);
                var outputBitmap = mode == ChartCopyMode.Light
                    ? CreateLightModeBitmap(bitmap)
                    : bitmap;

                Clipboard.SetImage(outputBitmap);
            }
            catch (Exception)
            {
                // Ignore clipboard failures (e.g., clipboard is locked by another process).
            }
        }

        private static RenderTargetBitmap RenderElementBitmap(FrameworkElement element)
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
            return renderTarget;
        }

        private static BitmapSource CreateLightModeBitmap(BitmapSource source)
        {
            var convertedSource = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = convertedSource.PixelWidth;
            int height = convertedSource.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[height * stride];
            convertedSource.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = (byte)(255 - pixels[i]);
                pixels[i + 1] = (byte)(255 - pixels[i + 1]);
                pixels[i + 2] = (byte)(255 - pixels[i + 2]);
            }

            return BitmapSource.Create(
                width,
                height,
                convertedSource.DpiX,
                convertedSource.DpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);
        }

        private enum ChartCopyMode
        {
            Dark,
            Light
        }
    }
}
