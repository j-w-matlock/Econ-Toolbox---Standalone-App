using System;
using System.Globalization;
using System.Windows.Data;

namespace EconToolbox.Desktop.Converters
{
    public class PercentToOpacityConverter : IValueConverter
    {
        public double MinimumOpacity { get; set; } = 0.35;
        public double MaximumOpacity { get; set; } = 1.0;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return MinimumOpacity;

            if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var percent))
            {
                percent = Math.Clamp(percent / 100.0, 0, 1);
                return MinimumOpacity + (MaximumOpacity - MinimumOpacity) * percent;
            }

            return MaximumOpacity;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
