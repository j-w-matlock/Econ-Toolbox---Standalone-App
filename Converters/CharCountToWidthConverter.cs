using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EconToolbox.Desktop.Converters
{
    public sealed class CharCountToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var charCount = 120;
            if (parameter is string parameterString && int.TryParse(parameterString, out var parsed))
            {
                charCount = parsed;
            }

            var fontFamily = values.Length > 0 && values[0] is FontFamily family
                ? family
                : new FontFamily("Segoe UI");
            var fontSize = values.Length > 1 && values[1] is double size ? size : 12d;
            var fontStyle = values.Length > 2 && values[2] is FontStyle style ? style : FontStyles.Normal;
            var fontWeight = values.Length > 3 && values[3] is FontWeight weight ? weight : FontWeights.Normal;
            var fontStretch = values.Length > 4 && values[4] is FontStretch stretch ? stretch : FontStretches.Normal;

            var typeface = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);
            var sample = new string('0', Math.Max(1, charCount));
            var formatted = new FormattedText(
                sample,
                culture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                1.0);

            return Math.Ceiling(formatted.WidthIncludingTrailingWhitespace);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return Array.Empty<object>();
        }
    }
}
