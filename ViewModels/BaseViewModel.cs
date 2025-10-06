using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.ViewModels
{
    public abstract class BaseViewModel : ObservableObject
    {
        protected new void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            base.OnPropertyChanged(name);
        }
    }
}
