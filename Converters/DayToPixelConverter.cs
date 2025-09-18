using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public class DayToPixelConverter : IValueConverter
    {
        public double Scale { get; set; } = 18.0;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return 0;

            if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var numeric))
            {
                return numeric * Scale;
            }

            if (value is TimeSpan span)
            {
                return span.TotalDays * Scale;
            }

            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
