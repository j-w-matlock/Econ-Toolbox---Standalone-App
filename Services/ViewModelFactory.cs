using System;
using EconToolbox.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EconToolbox.Desktop.Services
{
    public sealed class ViewModelFactory : IViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ViewModelFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public BaseViewModel Create(Type viewModelType)
        {
            return (BaseViewModel)_serviceProvider.GetRequiredService(viewModelType);
        }
    }
}
