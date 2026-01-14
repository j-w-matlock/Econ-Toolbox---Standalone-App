using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public sealed class BoolToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool isVisible || !isVisible)
            {
                return 0d;
            }

            if (parameter is double doubleParameter)
            {
                return doubleParameter;
            }

            if (parameter is string stringParameter
                && double.TryParse(stringParameter, NumberStyles.Any, culture, out var parsedValue))
            {
                return parsedValue;
            }

            return 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
