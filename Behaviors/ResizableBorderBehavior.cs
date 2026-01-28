using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace EconToolbox.Desktop.Behaviors;

public static class ResizableBorderBehavior
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(ResizableBorderBehavior),
        new PropertyMetadata(false, OnEnableChanged));

    private static readonly DependencyProperty AdornerProperty = DependencyProperty.RegisterAttached(
        "Adorner",
        typeof(ResizableBorderAdorner),
        typeof(ResizableBorderBehavior),
        new PropertyMetadata(null));

    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;
        }
        else
        {
            element.Loaded -= OnLoaded;
            element.Unloaded -= OnUnloaded;
            RemoveAdorner(element);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AddAdorner(element);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            RemoveAdorner(element);
        }
    }

    private static void AddAdorner(FrameworkElement element)
    {
        if (element.GetValue(AdornerProperty) is ResizableBorderAdorner)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(element);
        if (layer is null)
        {
            return;
        }

        var adorner = new ResizableBorderAdorner(element);
        layer.Add(adorner);
        element.SetValue(AdornerProperty, adorner);
    }

    private static void RemoveAdorner(FrameworkElement element)
    {
        if (element.GetValue(AdornerProperty) is not ResizableBorderAdorner adorner)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(element);
        layer?.Remove(adorner);
        element.ClearValue(AdornerProperty);
    }

    private sealed class ResizableBorderAdorner : Adorner
    {
        private readonly VisualCollection visuals;
        private readonly Thumb rightThumb;
        private readonly Thumb bottomThumb;

        public ResizableBorderAdorner(FrameworkElement adornedElement)
            : base(adornedElement)
        {
            visuals = new VisualCollection(this);
            rightThumb = CreateThumb(Cursors.SizeWE);
            bottomThumb = CreateThumb(Cursors.SizeNS);
            rightThumb.DragDelta += OnRightDragDelta;
            bottomThumb.DragDelta += OnBottomDragDelta;

            visuals.Add(rightThumb);
            visuals.Add(bottomThumb);
        }

        protected override int VisualChildrenCount => visuals.Count;

        protected override Visual GetVisualChild(int index) => visuals[index];

        protected override Size ArrangeOverride(Size finalSize)
        {
            var handleSize = Math.Max(0, GetHandleSize());
            var safeWidth = Math.Max(0, finalSize.Width);
            var safeHeight = Math.Max(0, finalSize.Height);

            rightThumb.Arrange(new Rect(Math.Max(0, safeWidth - handleSize), 0, handleSize, safeHeight));
            bottomThumb.Arrange(new Rect(0, Math.Max(0, safeHeight - handleSize), safeWidth, handleSize));

            return finalSize;
        }

        private Thumb CreateThumb(Cursor cursor)
        {
            var styleKey = cursor == Cursors.SizeWE
                ? "Thumb.ResizeHandle.Vertical"
                : "Thumb.ResizeHandle.Horizontal";

            var thumb = new Thumb
            {
                Cursor = cursor,
                Style = (Style?)FindResource(styleKey)
            };

            return thumb;
        }

        private void OnRightDragDelta(object sender, DragDeltaEventArgs e)
        {
            ResizeHorizontally(e.HorizontalChange);
        }

        private void OnBottomDragDelta(object sender, DragDeltaEventArgs e)
        {
            ResizeVertically(e.VerticalChange);
        }

        private void ResizeHorizontally(double horizontalChange)
        {
            if (AdornedElement is not FrameworkElement element)
            {
                return;
            }

            var minWidth = GetMinDimension(element.MinWidth, "Layout.CardMinWidth");
            if (double.IsNaN(element.Width))
            {
                element.Width = element.ActualWidth;
            }

            var maxWidth = GetMaxDimension(element.MaxWidth);
            var nextWidth = Math.Max(minWidth, element.Width + horizontalChange);
            element.Width = Math.Min(maxWidth, nextWidth);
        }

        private void ResizeVertically(double verticalChange)
        {
            if (AdornedElement is not FrameworkElement element)
            {
                return;
            }

            var minHeight = GetMinDimension(element.MinHeight, "Layout.CardMinHeight");
            if (double.IsNaN(element.Height))
            {
                element.Height = element.ActualHeight;
            }

            var maxHeight = GetMaxDimension(element.MaxHeight);
            var nextHeight = Math.Max(minHeight, element.Height + verticalChange);
            element.Height = Math.Min(maxHeight, nextHeight);
        }

        private double GetMinDimension(double fallback, string resourceKey)
        {
            if (!double.IsNaN(fallback) && fallback > 0)
            {
                return fallback;
            }

            if (FindResource(resourceKey) is double value)
            {
                return value;
            }

            return 0;
        }

        private double GetMaxDimension(double maxValue)
        {
            if (!double.IsNaN(maxValue) && !double.IsInfinity(maxValue) && maxValue > 0)
            {
                return maxValue;
            }

            return double.PositiveInfinity;
        }

        private double GetHandleSize()
        {
            if (FindResource("Layout.CardResizeHandleSize") is double value)
            {
                return value;
            }

            return 12;
        }
    }
}
