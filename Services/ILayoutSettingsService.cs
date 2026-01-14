using EconToolbox.Desktop.Models;

namespace EconToolbox.Desktop.Services
{
    public interface ILayoutSettingsService
    {
        LayoutSettings Load();

        void Save(LayoutSettings settings);
    }
}
