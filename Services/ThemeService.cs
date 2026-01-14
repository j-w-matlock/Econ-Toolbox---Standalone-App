using System;
using System.Linq;
using System.Windows;

namespace EconToolbox.Desktop.Services
{
    public sealed class ThemeService : IThemeService
    {
        private const string LightThemePath = "Themes/Design.xaml";
        private const string DarkThemePath = "Themes/Design.Dark.xaml";

        public void ApplyTheme(bool isDark)
        {
            var uri = new Uri(isDark ? DarkThemePath : LightThemePath, UriKind.Relative);
            var themeDictionary = new ResourceDictionary { Source = uri };
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            var existingTheme = mergedDictionaries.FirstOrDefault(dictionary =>
                dictionary.Source != null &&
                (dictionary.Source.OriginalString.EndsWith("Design.xaml", StringComparison.OrdinalIgnoreCase) ||
                 dictionary.Source.OriginalString.EndsWith("Design.Dark.xaml", StringComparison.OrdinalIgnoreCase)));

            if (existingTheme != null)
            {
                var themeIndex = mergedDictionaries.IndexOf(existingTheme);
                mergedDictionaries[themeIndex] = themeDictionary;
            }
            else
            {
                mergedDictionaries.Insert(0, themeDictionary);
            }
        }
    }
}
