using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public sealed class PercentToScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
            {
                return percent / 100d;
            }

            return 1d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double scale)
            {
                return scale * 100d;
            }

            return 100d;
        }
    }
}
