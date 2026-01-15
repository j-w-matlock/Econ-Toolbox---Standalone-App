namespace EconToolbox.Desktop.Services
{
    public interface IThemeService
    {
        ThemeVariant CurrentTheme { get; }

        void ApplyTheme(ThemeVariant theme);
    }
}
