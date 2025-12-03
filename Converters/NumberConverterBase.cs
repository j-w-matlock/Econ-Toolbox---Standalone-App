using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters;

public abstract class NumberConverterBase : IValueConverter
{
    public string FormatString { get; set; } = "G";

    public NumberStyles ParsingStyles { get; set; } = NumberStyles.Any;

    public object? NullValue { get; set; } = string.Empty;

    public bool TreatEmptyStringAsZero { get; set; } = false;
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return NullValue;
        }

        if (value is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return NullValue;
            }

            if (TryParse(stringValue, culture, out var parsedFromString))
            {
                return Format(parsedFromString, culture);
            }
        }
        else if (TryParse(value, culture, out var parsed))
        {
            return Format(parsed, culture);
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(FormatString, culture);
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
                return TreatEmptyStringAsZero ? ConvertParsed(0d, targetType) : Binding.DoNothing;
            }

            if (TryParse(stringValue, culture, out var parsedFromString))
            {
                return ConvertParsed(parsedFromString, targetType);
            }

            return Binding.DoNothing;
        }

        if (TryParse(value, culture, out var parsed))
        {
            return ConvertParsed(parsed, targetType);
        }

        if (value is IConvertible convertible)
        {
            try
            {
                var numeric = convertible.ToDouble(culture);
                return ConvertParsed(numeric, targetType);
            }
            catch (FormatException)
            {
                return Binding.DoNothing;
            }
        }

        return Binding.DoNothing;
    }

    protected virtual object Format(double value, CultureInfo culture) => value.ToString(FormatString, culture);

    protected virtual object ConvertParsed(double value, Type targetType)
    {
        if (targetType == typeof(decimal) || targetType == typeof(decimal?))
        {
            return (decimal)value;
        }

        if (targetType == typeof(int) || targetType == typeof(int?))
        {
            return (int)Math.Round(value);
        }

        if (targetType == typeof(double) || targetType == typeof(double?))
        {
            return value;
        }

        return Binding.DoNothing;
    }

    private bool TryParse(object value, CultureInfo culture, out double result)
    {
        if (value is string stringValue)
        {
            return double.TryParse(stringValue, ParsingStyles, culture, out result);
        }

        var stringified = System.Convert.ToString(value, culture);
        return double.TryParse(stringified, ParsingStyles, culture, out result);
    }
}
