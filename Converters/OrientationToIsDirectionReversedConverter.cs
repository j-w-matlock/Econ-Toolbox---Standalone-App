using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public class OrientationToIsDirectionReversedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Orientation orientation)
            {
                return orientation == Orientation.Vertical;
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
