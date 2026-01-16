using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public sealed class BoolAndDoubleToGridLengthConverter : IMultiValueConverter
    {
        public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null)
            {
                return new GridLength(0);
            }

            var isVisible = values.Length > 0 && values[0] is bool value && value;
            if (!isVisible)
            {
                return new GridLength(0);
            }

            if (values.Length > 1)
            {
                if (values[1] is GridLength gridLength)
                {
                    return gridLength;
                }

                if (values[1] is double doubleValue)
                {
                    return new GridLength(doubleValue);
                }

                if (values[1] is string stringValue
                    && double.TryParse(stringValue, NumberStyles.Any, culture, out var parsedValue))
                {
                    return new GridLength(parsedValue);
                }
            }

            return new GridLength(0);
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
    }
}
