using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.Services
{
    public sealed class LayoutSettingsService : ILayoutSettingsService
    {
        private const string SettingsFileName = "layout.json";
        private readonly string _settingsPath;

        public LayoutSettingsService(string? baseDirectory = null)
        {
            var settingsDirectory = baseDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EconToolbox");

            _settingsPath = Path.Combine(settingsDirectory, SettingsFileName);
        }

        public LayoutSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return new LayoutSettings();
                }

                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<LayoutSettings>(json) ?? new LayoutSettings();
                Normalize(settings);
                return settings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load layout settings: {ex}");
                return new LayoutSettings();
            }
        }

        public void Save(LayoutSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save layout settings: {ex}");
            }
        }

        private static void Normalize(LayoutSettings settings)
        {
            if (!IsValidWidth(settings.ExplorerPaneWidth))
            {
                settings.ExplorerPaneWidth = new LayoutSettings().ExplorerPaneWidth;
            }

            if (!IsValidWidth(settings.DetailsPaneWidth))
            {
                settings.DetailsPaneWidth = new LayoutSettings().DetailsPaneWidth;
            }
        }

        private static bool IsValidWidth(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        }
    }
}
