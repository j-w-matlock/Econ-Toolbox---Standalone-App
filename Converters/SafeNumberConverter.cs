using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public class SafeNumberConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return Binding.DoNothing;
            }

            var stringValue = value as string ?? System.Convert.ToString(value, culture);
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return Binding.DoNothing;
            }

            if (!double.TryParse(stringValue, NumberStyles.Any, culture, out var parsed))
            {
                return Binding.DoNothing;
            }

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return (int)Math.Round(parsed);
            }

            if (targetType == typeof(double) || targetType == typeof(double?))
            {
                return parsed;
            }

            return Binding.DoNothing;
        }
    }
}
