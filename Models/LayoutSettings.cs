namespace EconToolbox.Desktop.Models
{
    public sealed class LayoutSettings
    {
        public double ExplorerPaneWidth { get; set; } = 280;

        public double DetailsPaneWidth { get; set; } = 340;

        public bool IsExplorerPaneVisible { get; set; } = true;

        public bool IsDetailsPaneVisible { get; set; } = true;

        public bool IsDarkTheme { get; set; }
    }
}
