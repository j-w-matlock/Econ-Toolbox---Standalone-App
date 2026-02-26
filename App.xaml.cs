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
        private IHost? _host;
        private static int _dispatcherErrorShown;

        public App()
        {
            InitializeComponent();
            SetupGlobalExceptionHandlers();

            try
            {
                _host = BuildHost();
            }
            catch (Exception ex)
            {
                LogUnhandledException(ex, "HostBuild");
                ShowStartupError(
                    "building the dependency container",
                    "Confirm the .NET SDK is installed and run the VS Code Restore task before starting the app.",
                    ex);
            }
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
                };
            }
        }

        private static void LogUnhandledException(Exception ex, string source)
        {
            var details = $"[{source}] {ex}";
            Debug.WriteLine(details);
            Console.Error.WriteLine(details);
        }

        private static IHost BuildHost()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<IExcelExportService, ExcelExportService>();
                    services.AddSingleton<ILayoutSettingsService, LayoutSettingsService>();
                    services.AddSingleton<IViewModelFactory, ViewModelFactory>();

                    services.AddTransient<ReadMeViewModel>();
                    services.AddTransient<EadViewModel>();
                    services.AddTransient<AgricultureDepthDamageViewModel>();
                    services.AddTransient<UpdatedCostViewModel>();
                    services.AddTransient<AnnualizerViewModel>();
                    services.AddTransient<UdvViewModel>();
                    services.AddTransient<WaterDemandViewModel>();
                    services.AddTransient<RecreationCapacityViewModel>();
                    services.AddTransient<GanttViewModel>();
                    services.AddTransient<ConceptualModelViewModel>();
                    services.AddTransient<StageDamageOrganizerViewModel>();
                    services.AddTransient<UncertaintyStatisticsViewModel>();

                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        private static void ShowStartupError(string stage, string guidance, Exception ex)
        {
            var message =
                $"Economic Toolbox could not start while {stage}.\n\n" +
                $"{ex.GetType().Name}: {ex.Message}\n\n" +
                $"{guidance}\n\n" +
                "See the VS Code debug console for full details.";

            try
            {
                MessageBox.Show(message, "Startup Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // If we cannot show a dialog, at least ensure the exception is logged.
                LogUnhandledException(ex, "StartupErrorDialog");
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            if (_host == null)
            {
                Shutdown(-1);
                return;
            }

            try
            {
                await _host.StartAsync();

                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                LogUnhandledException(ex, "HostStartup");
                ShowStartupError(
                    "starting services",
                    "Run the Restore task in VS Code (Terminal → Run Task… → restore) and verify the Windows Desktop SDK workload is available.",
                    ex);
                Shutdown(-1);
                return;
            }


            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }

            base.OnExit(e);
        }
    }
}
