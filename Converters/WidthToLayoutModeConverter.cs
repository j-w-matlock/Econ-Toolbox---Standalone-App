using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public class WidthToLayoutModeConverter : IValueConverter
    {
        public double Threshold { get; set; } = 800;

        private const string WideMode = "Wide";
        private const string NarrowMode = "Narrow";

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double width && !double.IsNaN(width))
            {
                return width >= Threshold ? WideMode : NarrowMode;
            }

            return NarrowMode;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
