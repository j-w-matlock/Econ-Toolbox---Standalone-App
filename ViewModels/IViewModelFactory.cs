using System;

namespace EconToolbox.Desktop.ViewModels
{
    public interface IViewModelFactory
    {
        BaseViewModel Create(Type viewModelType);
    }
}
