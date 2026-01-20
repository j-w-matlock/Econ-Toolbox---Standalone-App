using System;
using EconToolbox.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EconToolbox.Desktop.Services
{
    public interface IViewModelFactory
    {
        BaseViewModel Create(Type viewModelType);
        T Create<T>() where T : BaseViewModel;
    }

    public sealed class ViewModelFactory : IViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ViewModelFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public BaseViewModel Create(Type viewModelType)
        {
            if (!typeof(BaseViewModel).IsAssignableFrom(viewModelType))
            {
                throw new ArgumentException("Requested type must derive from BaseViewModel.", nameof(viewModelType));
            }

            return (BaseViewModel)ActivatorUtilities.CreateInstance(_serviceProvider, viewModelType);
        }

        public T Create<T>() where T : BaseViewModel
        {
            return (T)Create(typeof(T));
        }
    }
}
