using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public class NullToBoolConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var isNull = value == null;
            return Invert ? isNull : !isNull;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
