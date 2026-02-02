using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.Converters
{
    public class ConceptualNodeClipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 4)
            {
                return Geometry.Empty;
            }

            if (values[0] is not double width || values[1] is not double height)
            {
                return Geometry.Empty;
            }

            if (values[2] is not ConceptualNodeShape shape)
            {
                return Geometry.Empty;
            }

            if (values[3] is not double cornerRadius)
            {
                cornerRadius = 0;
            }

            var rect = new Rect(0, 0, Math.Max(0, width), Math.Max(0, height));

            return shape switch
            {
                ConceptualNodeShape.Circle => new EllipseGeometry(rect),
                ConceptualNodeShape.RoundedRectangle => new RectangleGeometry(rect, cornerRadius, cornerRadius),
                _ => new RectangleGeometry(rect)
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
