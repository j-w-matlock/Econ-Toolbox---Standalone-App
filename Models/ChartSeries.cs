using System.Collections.Generic;
using System.Windows.Media;
using EconToolbox.Desktop.Themes;

namespace EconToolbox.Desktop.Models
{
    public class ChartSeries
    {
        public string Name { get; set; } = string.Empty;
        public Brush Stroke { get; set; } = ThemeResourceHelper.GetBrush("App.Chart.Series1", Brushes.SteelBlue);
        public List<ChartDataPoint> Points { get; set; } = new();
    }

    public class ChartDataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
