using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.Views.Controls
{
    public class LineGraphControl : FrameworkElement
    {
        public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
            nameof(Series),
            typeof(IEnumerable<ChartSeries>),
            typeof(LineGraphControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSeriesChanged));

        public static readonly DependencyProperty IsStageAxisProperty = DependencyProperty.Register(
            nameof(IsStageAxis),
            typeof(bool),
            typeof(LineGraphControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnAxisChanged));

        public static readonly DependencyProperty EmptyMessageProperty = DependencyProperty.Register(
            nameof(EmptyMessage),
            typeof(string),
            typeof(LineGraphControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnEmptyMessageChanged));

        private INotifyCollectionChanged? _seriesCollection;

        public IEnumerable<ChartSeries>? Series
        {
            get => (IEnumerable<ChartSeries>?)GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }

        public bool IsStageAxis
        {
            get => (bool)GetValue(IsStageAxisProperty);
            set => SetValue(IsStageAxisProperty, value);
        }

        public string EmptyMessage
        {
            get => (string)GetValue(EmptyMessageProperty);
            set => SetValue(EmptyMessageProperty, value);
        }

        private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LineGraphControl control)
            {
                return;
            }

            if (control._seriesCollection != null)
            {
                control._seriesCollection.CollectionChanged -= control.SeriesCollectionChanged;
            }

            if (e.NewValue is INotifyCollectionChanged notifyCollection)
            {
                control._seriesCollection = notifyCollection;
                notifyCollection.CollectionChanged += control.SeriesCollectionChanged;
            }
            else
            {
                control._seriesCollection = null;
            }

            control.InvalidateVisual();
        }

        private static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LineGraphControl control)
            {
                control.InvalidateVisual();
            }
        }

        private static void OnEmptyMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LineGraphControl control)
            {
                control.InvalidateVisual();
            }
        }

        private void SeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 4 || height <= 4)
            {
                return;
            }

            var seriesList = Series?.Where(s => s != null && s.Points.Count > 0).ToList();
            if (seriesList == null || seriesList.Count == 0)
            {
                DrawEmpty(dc, width, height);
                return;
            }

            double marginLeft = 56;
            double marginRight = 18;
            double marginTop = 18;
            double marginBottom = 42;

            var allPoints = seriesList.SelectMany(s => s.Points).ToList();
            double minX = allPoints.Min(p => p.X);
            double maxX = allPoints.Max(p => p.X);
            double minY = Math.Min(0, allPoints.Min(p => p.Y));
            double maxY = allPoints.Max(p => p.Y);

            if (Math.Abs(maxX - minX) < 1e-9)
            {
                maxX = minX + 1;
            }

            if (Math.Abs(maxY - minY) < 1e-9)
            {
                maxY = minY + 1;
            }

            double plotWidth = Math.Max(0, width - marginLeft - marginRight);
            double plotHeight = Math.Max(0, height - marginTop - marginBottom);

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 160, 170)), 1);
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(210, 210, 220)), 1) { DashStyle = DashStyles.Dot };
            var labelBrush = new SolidColorBrush(Color.FromRgb(90, 90, 100));

            // Axes
            Point origin = new(marginLeft, marginTop + plotHeight);
            Point xEnd = new(marginLeft + plotWidth, marginTop + plotHeight);
            Point yEnd = new(marginLeft, marginTop);
            dc.DrawLine(axisPen, origin, xEnd);
            dc.DrawLine(axisPen, origin, yEnd);

            // Grid and labels
            const int tickCount = 4;
            for (int i = 0; i <= tickCount; i++)
            {
                double fraction = i / (double)tickCount;

                // X ticks
                double xValue = minX + (maxX - minX) * fraction;
                double x = marginLeft + plotWidth * fraction;
                dc.DrawLine(gridPen, new Point(x, marginTop), new Point(x, marginTop + plotHeight));
                var xLabel = CreateLabel(IsStageAxis ? $"Stage {xValue:N2}" : $"{xValue:P0}", dpi, labelBrush);
                dc.DrawText(xLabel, new Point(x - xLabel.Width / 2, marginTop + plotHeight + 6));

                // Y ticks
                double yValue = minY + (maxY - minY) * fraction;
                double y = marginTop + plotHeight - (plotHeight * fraction);
                dc.DrawLine(gridPen, new Point(marginLeft, y), new Point(marginLeft + plotWidth, y));
                var yLabel = CreateLabel($"{yValue:C0}", dpi, labelBrush);
                dc.DrawText(yLabel, new Point(Math.Max(2, marginLeft - yLabel.Width - 6), y - yLabel.Height / 2));
            }

            foreach (var series in seriesList)
            {
                if (series.Points.Count < 1)
                {
                    continue;
                }

                var stroke = series.Stroke ?? Brushes.SteelBlue;
                var polyPen = new Pen(stroke, 2.5);
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    for (int i = 0; i < series.Points.Count; i++)
                    {
                        double x = marginLeft + ((series.Points[i].X - minX) / (maxX - minX)) * plotWidth;
                        double y = marginTop + plotHeight - ((series.Points[i].Y - minY) / (maxY - minY)) * plotHeight;
                        var point = new Point(x, y);
                        if (i == 0)
                        {
                            context.BeginFigure(point, false, false);
                        }
                        else
                        {
                            context.LineTo(point, true, true);
                        }
                    }
                }

                geometry.Freeze();
                dc.DrawGeometry(null, polyPen, geometry);

                foreach (var pt in series.Points)
                {
                    double x = marginLeft + ((pt.X - minX) / (maxX - minX)) * plotWidth;
                    double y = marginTop + plotHeight - ((pt.Y - minY) / (maxY - minY)) * plotHeight;
                    dc.DrawEllipse(Brushes.White, new Pen(stroke, 1.5), new Point(x, y), 4, 4);
                }
            }
        }

        private void DrawEmpty(DrawingContext dc, double width, double height)
        {
            string message = string.IsNullOrWhiteSpace(EmptyMessage)
                ? "Add data to visualize the damage curves."
                : EmptyMessage;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var text = CreateLabel(message, dpi, new SolidColorBrush(Color.FromRgb(100, 100, 110)));
            dc.DrawText(text, new Point((width - text.Width) / 2, (height - text.Height) / 2));
        }

        private static FormattedText CreateLabel(string text, double dpi, Brush brush)
        {
            return new FormattedText(
#pragma warning disable CS0618 // FormattedText constructor obsolete in .NET 8, acceptable for WPF rendering
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                brush,
                dpi);
#pragma warning restore CS0618
        }
    }
}
