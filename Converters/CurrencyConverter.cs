using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public class CurrencyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return string.Empty;
            }

            if (value is string stringValue && double.TryParse(stringValue, NumberStyles.Currency | NumberStyles.AllowThousands, culture, out var parsedFromString))
            {
                return parsedFromString.ToString("C2", culture);
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString("C2", culture);
            }

            if (double.TryParse(System.Convert.ToString(value, culture), NumberStyles.Any, culture, out var parsed))
            {
                return parsed.ToString("C2", culture);
            }

            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return Binding.DoNothing;
            }

            if (value is string stringValue)
            {
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return 0d;
                }

                if (double.TryParse(stringValue, NumberStyles.Currency, culture, out var parsed))
                {
                    if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                    {
                        return (decimal)parsed;
                    }

                    return parsed;
                }

                return Binding.DoNothing;
            }

            if (value is IConvertible convertible)
            {
                try
                {
                    var doubleValue = convertible.ToDouble(culture);
                    if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                    {
                        return (decimal)doubleValue;
                    }

                    return doubleValue;
                }
                catch (FormatException)
                {
                    return Binding.DoNothing;
                }
            }

            return Binding.DoNothing;
        }
    }
}
