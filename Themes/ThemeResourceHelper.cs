using System.Windows;
using System.Windows.Media;

namespace EconToolbox.Desktop.Themes
{
    public static class ThemeResourceHelper
    {
        public static Brush GetBrush(string key, Brush fallback)
        {
            if (Application.Current?.Resources[key] is Brush brush)
            {
                return brush;
            }

            return fallback;
        }

        public static Color GetColor(string key, Color fallback)
        {
            if (Application.Current?.Resources[key] is Color color)
            {
                return color;
            }

            if (Application.Current?.Resources[key] is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return fallback;
        }

        public static SolidColorBrush GetSolidColorBrush(string key, Color fallback)
        {
            if (Application.Current?.Resources[key] is SolidColorBrush brush)
            {
                return brush;
            }

            return new SolidColorBrush(fallback);
        }
    }
}
