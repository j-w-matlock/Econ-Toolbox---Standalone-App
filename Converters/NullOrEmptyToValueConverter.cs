using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    /// <summary>
    /// Converts null or empty values into a configurable placeholder while letting non-empty values pass through.
    /// </summary>
    public class NullOrEmptyToValueConverter : IValueConverter
    {
        /// <summary>
        /// Value returned when the input is null, empty, or whitespace.
        /// </summary>
        public object? EmptyValue { get; set; } = string.Empty;

        /// <summary>
        /// Optional value to return when the input is non-empty. When unset, the original value is returned.
        /// </summary>
        public object? NonEmptyValue { get; set; } = null;

        public bool TrimWhitespace { get; set; } = true;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var text = value as string ?? value?.ToString();
            var isEmpty = text is null || (TrimWhitespace ? string.IsNullOrWhiteSpace(text) : text.Length == 0);

            if (isEmpty)
            {
                return parameter ?? EmptyValue;
            }

            return NonEmptyValue ?? value;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
    }
}
