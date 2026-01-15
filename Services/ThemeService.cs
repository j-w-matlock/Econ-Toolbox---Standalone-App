using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace EconToolbox.Desktop.Services
{
    public sealed class ThemeService : IThemeService
    {
        private const string LightPalettePath = "Themes/Palette.Light.xaml";
        private const string DarkPalettePath = "Themes/Palette.Dark.xaml";
        private const string SettingsFileName = "theme.json";
        private readonly string _settingsPath;

        public ThemeVariant CurrentTheme { get; private set; }

        public ThemeService(string? baseDirectory = null)
        {
            var settingsDirectory = baseDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EconToolbox");

            _settingsPath = Path.Combine(settingsDirectory, SettingsFileName);
            CurrentTheme = LoadThemePreference();
        }

        public void ApplyTheme(ThemeVariant theme)
        {
            CurrentTheme = theme;

            if (Application.Current == null)
            {
                return;
            }

            var palettePath = theme == ThemeVariant.Dark ? DarkPalettePath : LightPalettePath;
            var paletteDictionary = new ResourceDictionary { Source = new Uri(palettePath, UriKind.Relative) };
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            var existingPalette = mergedDictionaries.FirstOrDefault(dictionary =>
                dictionary.Source != null &&
                (dictionary.Source.OriginalString.EndsWith("Palette.Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                 dictionary.Source.OriginalString.EndsWith("Palette.Dark.xaml", StringComparison.OrdinalIgnoreCase)));

            if (existingPalette != null)
            {
                var paletteIndex = mergedDictionaries.IndexOf(existingPalette);
                mergedDictionaries[paletteIndex] = paletteDictionary;
            }
            else
            {
                mergedDictionaries.Insert(0, paletteDictionary);
            }

            SaveThemePreference(theme);
        }

        private ThemeVariant LoadThemePreference()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return ThemeVariant.Light;
                }

                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
                return settings?.Theme == ThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load theme preference: {ex}");
                return ThemeVariant.Light;
            }
        }

        private void SaveThemePreference(ThemeVariant theme)
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(new ThemeSettings { Theme = theme }, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save theme preference: {ex}");
            }
        }

        private sealed class ThemeSettings
        {
            public ThemeVariant Theme { get; set; }
        }
    }
}
