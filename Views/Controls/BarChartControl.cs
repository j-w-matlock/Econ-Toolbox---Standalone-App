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
    public class BarChartControl : FrameworkElement
    {
        public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
            nameof(Series),
            typeof(IEnumerable<ChartSeries>),
            typeof(BarChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSeriesChanged));

        public static readonly DependencyProperty EmptyMessageProperty = DependencyProperty.Register(
            nameof(EmptyMessage),
            typeof(string),
            typeof(BarChartControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable<ChartSeries>? Series
        {
            get => (IEnumerable<ChartSeries>?)GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }

        public string EmptyMessage
        {
            get => (string)GetValue(EmptyMessageProperty);
            set => SetValue(EmptyMessageProperty, value);
        }

        private INotifyCollectionChanged? _seriesCollection;

        private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not BarChartControl control)
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

            double marginLeft = 64;
            double marginRight = 18;
            double marginTop = 18;
            double marginBottom = 64;

            var allPoints = seriesList.SelectMany(s => s.Points).ToList();
            double maxY = Math.Max(0.1, allPoints.Max(p => p.Y));
            double plotWidth = Math.Max(0, width - marginLeft - marginRight);
            double plotHeight = Math.Max(0, height - marginTop - marginBottom);

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 160, 170)), 1);
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(210, 210, 220)), 1) { DashStyle = DashStyles.Dot };
            var labelBrush = new SolidColorBrush(Color.FromRgb(90, 90, 100));

            Point origin = new(marginLeft, marginTop + plotHeight);
            Point xEnd = new(marginLeft + plotWidth, marginTop + plotHeight);
            Point yEnd = new(marginLeft, marginTop);
            dc.DrawLine(axisPen, origin, xEnd);
            dc.DrawLine(axisPen, origin, yEnd);

            // Y axis labels
            int yTicks = 5;
            for (int i = 0; i <= yTicks; i++)
            {
                double fraction = i / (double)yTicks;
                double value = maxY * fraction;
                double y = marginTop + plotHeight - (plotHeight * fraction);
                var text = new FormattedText(value.ToString("N0", CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, labelBrush, dpi);
                dc.DrawText(text, new Point(marginLeft - 8 - text.Width, y - text.Height / 2));
                dc.DrawLine(gridPen, new Point(marginLeft, y), new Point(marginLeft + plotWidth, y));
            }

            double seriesSpacing = seriesList.Count > 1 ? 6 : 0;
            var colors = new[] { Color.FromRgb(45, 106, 142), Color.FromRgb(26, 188, 156), Color.FromRgb(127, 86, 217), Color.FromRgb(243, 156, 18) };

            int categoryCount = allPoints.Count;
            double barSlotWidth = categoryCount > 0 ? plotWidth / categoryCount : plotWidth;
            int colorIndex = 0;

            int categoryIndex = 0;
            foreach (var series in seriesList)
            {
                var barBrush = new SolidColorBrush(colors[colorIndex % colors.Length]);
                barBrush.Freeze();

                foreach (var point in series.Points)
                {
                    double barLeft = marginLeft + (barSlotWidth * categoryIndex) + (seriesSpacing / 2);
                    double barWidth = Math.Max(8, barSlotWidth - seriesSpacing);
                    double barHeight = Math.Min(plotHeight, (point.Y / maxY) * plotHeight);
                    double barTop = marginTop + plotHeight - barHeight;
                    dc.DrawRoundedRectangle(barBrush, null, new Rect(barLeft, barTop, barWidth, barHeight), 4, 4);

                    string label = string.IsNullOrWhiteSpace(point.Label) ? (categoryIndex + 1).ToString(CultureInfo.InvariantCulture) : point.Label;
                    var labelText = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, labelBrush, dpi);
                    double labelX = barLeft + (barWidth - labelText.Width) / 2;
                    double labelY = marginTop + plotHeight + 6;
                    dc.DrawText(labelText, new Point(labelX, labelY));

                    categoryIndex++;
                }

                colorIndex++;
            }
        }

        private void DrawEmpty(DrawingContext dc, double width, double height)
        {
            if (string.IsNullOrWhiteSpace(EmptyMessage))
            {
                return;
            }

            var message = new FormattedText(
                EmptyMessage,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                new SolidColorBrush(Color.FromRgb(90, 90, 100)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                TextAlignment = TextAlignment.Center,
                MaxTextWidth = width - 20
            };

            dc.DrawText(message, new Point((width - message.Width) / 2, (height - message.Height) / 2));
        }
    }
}
