using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EconToolbox.Desktop.Services;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop
{
    public partial class App : Application
    {
        private readonly IHost _host;
        private static int _dispatcherErrorShown;

        public App()
        {
            SetupGlobalExceptionHandlers();

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

        private static void SetupGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    LogUnhandledException(ex, "AppDomain");
                }
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogUnhandledException(args.Exception, "TaskScheduler");
                args.SetObserved();
            };

            if (Current != null)
            {
                Current.DispatcherUnhandledException += (_, args) =>
                {
                    LogUnhandledException(args.Exception, "Dispatcher");
                    args.Handled = true;
                    if (Interlocked.CompareExchange(ref _dispatcherErrorShown, 1, 0) == 0)
                    {
                        try
                        {
                            MessageBox.Show(
                                "An unexpected error occurred. The app will continue running, but some results may be unavailable. Check the logs for details.",
                                "Unexpected Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                        catch (Exception dialogEx)
                        {
                            // Avoid crashing the app if the error dialog fails; just log the attempt.
                            LogUnhandledException(dialogEx, "DispatcherDialog");
                        }
                    }
                    MessageBox.Show(
                        "An unexpected error occurred. The app will continue running, but some results may be unavailable. Check the logs for details.",
                        "Unexpected Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                };
            }
        }

        private static void LogUnhandledException(Exception ex, string source)
        {
            var details = $"[{source}] {ex}";
            Debug.WriteLine(details);
            Console.Error.WriteLine(details);
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
