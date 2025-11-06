using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EconToolbox.Desktop.Services;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<IExcelExportService, ExcelExportService>();

                    services.AddSingleton<ReadMeViewModel>();
                    services.AddSingleton<EadViewModel>();
                    services.AddSingleton<AgricultureDepthDamageViewModel>();
                    services.AddSingleton<UpdatedCostViewModel>();
                    services.AddSingleton<AnnualizerViewModel>();
                    services.AddSingleton<UdvViewModel>();
                    services.AddSingleton<WaterDemandViewModel>();
                    services.AddSingleton<RecreationCapacityViewModel>();
                    services.AddSingleton<MindMapViewModel>();
                    services.AddSingleton<GanttViewModel>();
                    services.AddSingleton<DrawingViewModel>();

                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();

            base.OnExit(e);
        }
    }
}
