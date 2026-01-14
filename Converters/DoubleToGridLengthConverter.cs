using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public sealed class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GridLength gridLength)
            {
                return gridLength;
            }

            if (value is double doubleValue)
            {
                return new GridLength(doubleValue);
            }

            return new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GridLength gridLength)
            {
                return gridLength.Value;
            }

            if (value is double doubleValue)
            {
                return doubleValue;
            }

            return 0d;
        }
    }
}
