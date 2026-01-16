using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EconToolbox.Desktop.Converters
{
    public class GanttLinkGeometryConverter : IMultiValueConverter
    {
        public double DayScale { get; set; } = 18.0;

        public object? Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 4 ||
                !TryParse(values[0], out double fromDay) ||
                !TryParse(values[1], out double toDay) ||
                !TryParse(values[2], out double fromY) ||
                !TryParse(values[3], out double toY))
            {
                return Geometry.Empty;
            }

            double scale = DayScale > 0 ? DayScale : 18.0;
            double startX = Math.Max(0, fromDay * scale);
            double endX = Math.Max(startX + 6, toDay * scale);

            double startY = fromY;
            double endY = toY;

            double span = Math.Max(24, endX - startX);
            double controlOffset = Math.Max(20, span * 0.35);

            var figure = new PathFigure
            {
                StartPoint = new Point(startX, startY),
                IsFilled = false,
                IsClosed = false
            };

            figure.Segments.Add(new BezierSegment(
                new Point(startX + controlOffset, startY),
                new Point(endX - controlOffset, endY),
                new Point(endX, endY),
                true));

            return new PathGeometry(new[] { figure });
        }

        public object[] ConvertBack(object? value, Type[]? targetTypes, object? parameter, CultureInfo culture)
        {
            if (targetTypes is null || targetTypes.Length == 0)
            {
                return Array.Empty<object>();
            }

            var results = new object[targetTypes.Length];
            for (var i = 0; i < targetTypes.Length; i++)
            {
                results[i] = Binding.DoNothing;
            }

            return results;
        }

        private static bool TryParse(object? value, out double result)
        {
            if (value is double d)
            {
                result = d;
                return true;
            }

            if (double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                result = parsed;
                return true;
            }

            result = 0;
            return false;
        }
    }
}
