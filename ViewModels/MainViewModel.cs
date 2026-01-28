using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private const double DefaultExplorerPaneWidth = 280;
        private const double DefaultDetailsPaneWidth = 340;
        public ModuleDefinition ReadMeModule { get; }
        public IReadOnlyList<ModuleDefinition> Modules { get; }
        public ObservableCollection<DiagnosticItem> Diagnostics { get; } = new();
        public ProjectViewModel ProjectManager { get; }

        private ModuleDefinition? _selectedModule;
        private ModuleDefinition? _explorerSelectedModule;
        private bool _isSyncingSelection;
        private ICommand? _currentComputeCommand;
        private IDiagnosticsProvider? _diagnosticsProvider;
        private readonly Dictionary<Type, BaseViewModel> _viewModelCache = new();
        public ModuleDefinition? SelectedModule
        {
            get => _selectedModule;
            set
            {
                if (CurrentViewModel is DiagnosticViewModelBase oldVm)
                {
                    oldVm.PropertyChanged -= OnCurrentViewModelPropertyChanged;
                }

                if (SetProperty(ref _selectedModule, value))
                {
                    SyncExplorerSelection(value);
                    UpdateCurrentModuleState(value);
                    OnPropertyChanged(nameof(IsCalculateVisible));
                    OnPropertyChanged(nameof(PrimaryActionLabel));
                    OnPropertyChanged(nameof(CurrentViewModel));
                    UpdateDiagnostics();

                    if (CurrentViewModel is DiagnosticViewModelBase newVm)
                    {
                        newVm.PropertyChanged += OnCurrentViewModelPropertyChanged;
                    }
                }
            }
        }

        public ModuleDefinition? ExplorerSelectedModule
        {
            get => _explorerSelectedModule;
            set
            {
                if (ReferenceEquals(value, _explorerSelectedModule))
                {
                    return;
                }

                _explorerSelectedModule = value;
                OnPropertyChanged();

                if (value != null && !ReferenceEquals(value, SelectedModule))
                {
                    SelectedModule = value;
                }
            }
        }

        private void SyncExplorerSelection(ModuleDefinition? selected)
        {
            if (_isSyncingSelection)
            {
                return;
            }

            _isSyncingSelection = true;
            if (selected != null && Modules.Contains(selected))
            {
                _explorerSelectedModule = selected;
            }
            else if (selected == null)
            {
                _explorerSelectedModule = null;
            }
            else
            {
                // Keep the last explorer selection when switching to modules outside the explorer list (e.g., ReadMe).
            }

            OnPropertyChanged(nameof(ExplorerSelectedModule));
            _isSyncingSelection = false;
        }

        public IRelayCommand CalculateCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IAsyncRelayCommand SaveProjectCommand { get; }
        public IAsyncRelayCommand LoadProjectCommand { get; }
        public IRelayCommand ToggleLeftPaneCommand { get; }
        public IRelayCommand ToggleRightPaneCommand { get; }
        public IRelayCommand ShowReadMeCommand { get; }

        private bool _isDetailsPaneVisible = true;
        public bool IsDetailsPaneVisible
        {
            get => _isDetailsPaneVisible;
            set
            {
                if (_isDetailsPaneVisible == value) return;
                _isDetailsPaneVisible = value;
                OnPropertyChanged();
                UpdateLayoutSettings();
            }
        }

        private bool _isExplorerPaneVisible = true;
        public bool IsExplorerPaneVisible
        {
            get => _isExplorerPaneVisible;
            set
            {
                if (_isExplorerPaneVisible == value) return;
                _isExplorerPaneVisible = value;
                OnPropertyChanged();
                UpdateLayoutSettings();
            }
        }

        private double _explorerPaneWidth = DefaultExplorerPaneWidth;
        public double ExplorerPaneWidth
        {
            get => _explorerPaneWidth;
            set
            {
                if (Math.Abs(_explorerPaneWidth - value) < 0.1) return;
                _explorerPaneWidth = value;
                if (value > 0)
                {
                    _explorerPaneWidthBeforeCollapse = value;
                }
                OnPropertyChanged();
                UpdateLayoutSettings();
            }
        }

        private double _detailsPaneWidth = DefaultDetailsPaneWidth;
        public double DetailsPaneWidth
        {
            get => _detailsPaneWidth;
            set
            {
                if (Math.Abs(_detailsPaneWidth - value) < 0.1) return;
                _detailsPaneWidth = value;
                if (value > 0)
                {
                    _detailsPaneWidthBeforeCollapse = value;
                }
                OnPropertyChanged();
                UpdateLayoutSettings();
            }
        }

        public BaseViewModel? CurrentViewModel
        {
            get
            {
                if (SelectedModule == null)
                {
                    return null;
                }

                if (!_viewModelCache.TryGetValue(SelectedModule.ViewModelType, out var cached))
                {
                    cached = _viewModelFactory.Create(SelectedModule.ViewModelType);
                    _viewModelCache[SelectedModule.ViewModelType] = cached;
                }

                return cached;
            }
        }

        public bool IsCalculateVisible => _currentComputeCommand?.CanExecute(null) == true;

        public string PrimaryActionLabel => SelectedModule?.Title switch
        {
            "Water Demand Forecasting" => "Forecast",
            "Standard Gantt Planner" => "Schedule",
            _ => "Calculate"
        };

        private readonly IExcelExportService _excelExportService;
        private readonly ILayoutSettingsService _layoutSettingsService;
        private readonly IViewModelFactory _viewModelFactory;
        private LayoutSettings _layoutSettings = new();
        private double _explorerPaneWidthBeforeCollapse = DefaultExplorerPaneWidth;
        private double _detailsPaneWidthBeforeCollapse = DefaultDetailsPaneWidth;
        private bool _isApplyingSettings;
        private double _zoomPercent = 100;

        public double ZoomPercent
        {
            get => _zoomPercent;
            set
            {
                var clamped = Math.Clamp(value, 50, 200);
                if (Math.Abs(_zoomPercent - clamped) < 0.1)
                {
                    return;
                }

                _zoomPercent = clamped;
                OnPropertyChanged();
            }
        }

        public MainViewModel(
            IViewModelFactory viewModelFactory,
            IExcelExportService excelExportService,
            ILayoutSettingsService layoutSettingsService)
        {
            _viewModelFactory = viewModelFactory;
            _excelExportService = excelExportService;
            _layoutSettingsService = layoutSettingsService;

            ProjectManager = new ProjectViewModel();

            CalculateCommand = new RelayCommand(Calculate);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync);
            LoadProjectCommand = new AsyncRelayCommand(LoadProjectAsync);
            ToggleLeftPaneCommand = new RelayCommand(ToggleExplorerPane);
            ToggleRightPaneCommand = new RelayCommand(ToggleDetailsPane);
            ShowReadMeCommand = new RelayCommand(ShowReadMe);

            ReadMeModule = new ModuleDefinition(
                "Project README",
                "Review onboarding tips, module descriptions, and build instructions without leaving the toolbox.",
                "Centralizes documentation and onboarding guidance inside the desktop shell.",
                new[]
                {
                    "Browse the introduction to understand the toolkit's purpose and architecture.",
                    "Follow the build and publish instructions before sharing the desktop app with teammates.",
                    "Reference the calculator summaries to confirm which module fits your analysis need."
                },
                new[]
                {
                    "Shows the repository's README rendered with headings, lists, and syntax highlighting.",
                    "Keeps the latest project documentation available offline inside the application shell.",
                    "Supports opening external links in your default browser for deeper references."
                },
                "Example: Quickly scan the packaging checklist before exporting deliverables for a stakeholder workshop.",
                typeof(ReadMeViewModel));

            Modules = new List<ModuleDefinition>
            {
                new ModuleDefinition(
                    "Expected Annual Damage (EAD)",
                    "Quantify how frequently damages occur by pairing exceedance probabilities with damage estimates.",
                    "Calculates frequency-damage relationships, curves, and EAD exports for comparison.",
                    new[]
                    {
                        "List probabilities between 0 and 1 in descending order so the curve integrates correctly.",
                        "Optionally add stage values that align with the same probabilities when stage-damage insight is needed.",
                        "Add, rename, and populate damage columns to represent scenarios, assets, or plans being compared."
                    },
                    new[]
                    {
                        "Computes the expected annual damage for every damage column using trapezoidal integration.",
                        "Draws frequency-damage and stage-damage curves so you can visually QA the shape of the inputs.",
                        "Exports the full grid, summary text, and charts to Excel for documentation."
                    },
                    "Example: The Cedar River levee district pairs 0.5, 0.1, and 0.01 annual exceedance probabilities with $250K, $1.2M, and $6.8M structure damage estimates captured in its 2019 flood study.",
                    typeof(EadViewModel)),
                new ModuleDefinition(
                    "Agriculture Depth-Damage",
                    "Calibrate crop and structure damages for agricultural assets using custom depth-damage relationships.",
                    "Simulates agricultural depth-damage relationships with Monte Carlo-driven results.",
                    new[]
                    {
                        "Enter annual exceedance probabilities as decimal values between 0 and 1 for each point on the curve.",
                        "Pair each probability with the representative flood depth and percent damage for your custom region.",
                        "Use Add/Remove Row to adjust the table before running the Monte Carlo simulation."
                    },
                    new[]
                    {
                        "Runs a Monte Carlo simulation that interpolates across your exceedance probabilities.",
                        "Summarizes mean, median, and 90th percentile damages alongside inundation depths.",
                        "Plots the resulting depth-damage function so you can visually confirm curve behavior."
                    },
                    "Example: A delta farm pairs 0.5, 0.1, and 0.02 annual exceedance probabilities with 0 ft, 1.5 ft, and 3.5 ft flood depths causing 0%, 25%, and 75% damages respectively.",
                    typeof(AgricultureDepthDamageViewModel)),
                new ModuleDefinition(
                    "Updated Cost of Storage",
                    "Update historical costs and allocate joint expenses based on storage recommendations.",
                    "Allocates joint reservoir costs and escalates historical expenditures for comparisons.",
                    new[]
                    {
                        "Confirm the total usable storage and the recommendation for your plan to establish the allocation percent.",
                        "Enter annual joint O&M totals and refresh historical cost line items with appropriate indices.",
                        "Capture RR&R/mitigation details and scenario-specific capital recovery factors before computing totals."
                    },
                    new[]
                    {
                        "Reports the storage allocation percentage applied to joint costs and RR&R values.",
                        "Summarizes updated capital, annualized RR&R, and total annual cost comparisons across scenarios.",
                        "Provides an export-ready workbook covering every sub-tab for auditability."
                    },
                    "Example: Modernizing the North Bay joint-use reservoir assigns 70% of its 850 acre-feet to the recommended diversion plan while escalating $2.3M of 2010 construction costs to FY24 dollars.",
                    typeof(UpdatedCostViewModel)),
                new ModuleDefinition(
                    "Cost Annualization",
                    "Translate capital outlays and future costs into comparable annual values.",
                    "Transforms capital, O&M, and benefits into comparable annualized values.",
                    new[]
                    {
                        "Enter first cost, discount rate, analysis period, and base year that frame the recovery analysis.",
                        "Populate the IDC schedule and any future cost entries in the year they will occur.",
                        "Document annual O&M and expected annual benefits to complete the annualization picture."
                    },
                    new[]
                    {
                        "Calculates IDC, total investment, capital recovery factor, annual cost, and benefit-cost ratio.",
                        "Generates schedules for future costs and IDC contributions for traceability.",
                        "Exports a concise summary table and supporting detail to Excel."
                    },
                    "Example: Annualizing a $45M pump station replacement uses a 3.5% discount rate over 50 years, includes $250K in annual O&M, and captures a $1.1M mid-life rehab cost occurring in year 25.",
                    typeof(AnnualizerViewModel)),
                new ModuleDefinition(
                    "Water Demand Forecasting",
                    "Project future water demand scenarios from historical data and planning assumptions.",
                    "Forecasts sector demand scenarios with configurable growth and efficiency levers.",
                    new[]
                    {
                        "Load historical demand records so baseline year and trend information can auto-populate.",
                        "Select a scenario, adjust sector shares, and fine-tune system improvements or losses.",
                        "Set growth assumptions (population and per-capita demand) and choose the forecast horizon."
                    },
                    new[]
                    {
                        "Produces annual demand projections for each scenario with context on growth drivers.",
                        "Displays comparison charts and tables to highlight divergence across scenarios.",
                        "Allows exporting to Excel with data tables and visualizations for stakeholder review."
                    },
                    "Example: Forecasting demand for a city of 180,000 residents that used 120 gallons per capita in 2023 while expecting 1.5% annual population growth and system improvements cutting losses by 8%.",
                    typeof(WaterDemandViewModel)),
                new ModuleDefinition(
                    "Unit Day Value",
                    "Calibrate recreational benefits and visitation patterns to compute annual unit day values.",
                    "Values recreation experiences to compute composite unit day benefits and visitation splits.",
                    new[]
                    {
                        "Describe the project setting, visitation characteristics, and facility quality inputs.",
                        "Populate demand projections and adjust quality points for each recreation type.",
                        "Review visitation splits and ensure weighting reflects expected usage."
                    },
                    new[]
                    {
                        "Calculates weighted unit day values and total annual recreational benefits.",
                        "Highlights how quality point adjustments influence the composite value.",
                        "Supports exporting the evaluation for integration into economic reports."
                    },
                    "Example: Evaluating the Riverwalk trail system anticipates 42,000 annual user days split between day hiking and cycling with average facility quality scores of 27 points.",
                    typeof(UdvViewModel)),
                new ModuleDefinition(
                    "Recreation Capacity Study",
                    "Evaluate camping, shoreline fishing, and boating capacity using USACE design day standards.",
                    "Estimates design day and seasonal capacities for recreation assets using USACE standards.",
                    new[]
                    {
                        "Document the campsite inventory, managed shoreline, and usable water surface from the master plan.",
                        "Confirm occupancy, spacing, and turnover multipliers align with your study assumptions.",
                        "Enter recreation season lengths before calculating so totals match your reporting window."
                    },
                    new[]
                    {
                        "Computes people-at-one-time and design day user capacities for each recreation activity.",
                        "Rolls the activities into a seasonal user-day total for master plan documentation.",
                        "Captures key assumptions so stakeholders can review capacity drivers."
                    },
                    "Example: Updating a lake master plan with 120 campsites, 2,400 feet of managed shoreline, and 650 acres of usable water surface.",
                    typeof(RecreationCapacityViewModel)),
                new ModuleDefinition(
                    "Standard Gantt Planner",
                    "Organize project activities, dependencies, and milestones in a timeline consistent with industry schedules.",
                    "Plans dependencies and milestones with auto-sequenced Gantt timelines and exports.",
                    new[]
                    {
                        "List each task with a start date, duration, and responsible workstream.",
                        "Identify predecessors to respect finish-to-start dependencies across the plan.",
                        "Use the Calculate action to refresh dates after editing durations or dependencies."
                    },
                    new[]
                    {
                        "Automatically sequences start and finish dates based on dependency logic.",
                        "Generates a bar chart showing duration, percent complete, and milestones.",
                        "Exports both the task register and timeline graphic alongside other modules."
                    },
                    "Example: A feasibility study includes kickoff, stakeholder workshops, baseline analysis, and a design milestone with finish-to-start dependencies.",
                    typeof(GanttViewModel)),
                new ModuleDefinition(
                    "Stage Damage Organizer",
                    "Compile FDA 2.0 Stage Damage Functions_StructureStageDamageDetails.csv files by damage category.",
                    "Loads CSV exports, groups by DamageCategory, highlights peak frequent-AEP structure damages, and plots a summary bar chart.",
                    new[]
                    {
                        "Select one or more Stage Damage Functions_StructureStageDamageDetails.csv files exported from FDA 2.0.",
                        "Confirm the DamageCategory values look correct in the summary table.",
                        "Optional: rename the summary to control the CSV export file name and chart legend."
                    },
                    new[]
                    {
                        "Shows total structure damages by DamageCategory across frequent AEPs (0.493, 0.224, 0.034).",
                        "Lists the structures with the highest frequent-AEP structure damages and their AEP of peak impact.",
                        "Exports a concise CSV summary that includes category totals and highlighted structures."
                    },
                    "Example: Organize multiple impact areas and report which DamageCategory drives the highest frequent-AEP structure damages.",
                    typeof(StageDamageOrganizerViewModel))
            };

            ApplyLayoutSettings();
            ExplorerSelectedModule = Modules.Count > 0 ? Modules[0] : null;
            SelectedModule ??= ExplorerSelectedModule;
            if (SelectedModule == null)
            {
                UpdateDiagnostics();
            }
        }

        private void ApplyLayoutSettings()
        {
            _layoutSettings = _layoutSettingsService.Load();
            _isApplyingSettings = true;

            _explorerPaneWidth = _layoutSettings.IsExplorerPaneVisible ? _layoutSettings.ExplorerPaneWidth : 0;
            _detailsPaneWidth = _layoutSettings.IsDetailsPaneVisible ? _layoutSettings.DetailsPaneWidth : 0;
            _isExplorerPaneVisible = _layoutSettings.IsExplorerPaneVisible;
            _isDetailsPaneVisible = _layoutSettings.IsDetailsPaneVisible;

            _explorerPaneWidthBeforeCollapse = _layoutSettings.ExplorerPaneWidth > 0
                ? _layoutSettings.ExplorerPaneWidth
                : DefaultExplorerPaneWidth;
            _detailsPaneWidthBeforeCollapse = _layoutSettings.DetailsPaneWidth > 0
                ? _layoutSettings.DetailsPaneWidth
                : DefaultDetailsPaneWidth;
            _isApplyingSettings = false;

            OnPropertyChanged(nameof(ExplorerPaneWidth));
            OnPropertyChanged(nameof(DetailsPaneWidth));
            OnPropertyChanged(nameof(IsExplorerPaneVisible));
            OnPropertyChanged(nameof(IsDetailsPaneVisible));
        }

        private void ToggleDetailsPane()
        {
            if (IsDetailsPaneVisible)
            {
                _detailsPaneWidthBeforeCollapse = DetailsPaneWidth > 0 ? DetailsPaneWidth : _detailsPaneWidthBeforeCollapse;
                DetailsPaneWidth = 0;
                IsDetailsPaneVisible = false;
                return;
            }

            IsDetailsPaneVisible = true;
            DetailsPaneWidth = _detailsPaneWidthBeforeCollapse > 0 ? _detailsPaneWidthBeforeCollapse : DefaultDetailsPaneWidth;
        }

        private void ToggleExplorerPane()
        {
            if (IsExplorerPaneVisible)
            {
                _explorerPaneWidthBeforeCollapse = ExplorerPaneWidth > 0 ? ExplorerPaneWidth : _explorerPaneWidthBeforeCollapse;
                ExplorerPaneWidth = 0;
                IsExplorerPaneVisible = false;
                return;
            }

            IsExplorerPaneVisible = true;
            ExplorerPaneWidth = _explorerPaneWidthBeforeCollapse > 0 ? _explorerPaneWidthBeforeCollapse : DefaultExplorerPaneWidth;
        }

        private void ShowReadMe()
        {
            SelectedModule = ReadMeModule;
        }

        private void UpdateCurrentModuleState(ModuleDefinition? module)
        {
            if (module == null)
            {
                SetCurrentComputeCommand(null);
                SetDiagnosticsProvider(null);
                return;
            }

            var viewModel = CurrentViewModel;
            SetDiagnosticsProvider(viewModel as IDiagnosticsProvider);
            SetCurrentComputeCommand((viewModel as IComputeModule)?.ComputeCommand);
        }

        private void SetDiagnosticsProvider(IDiagnosticsProvider? provider)
        {
            if (ReferenceEquals(_diagnosticsProvider, provider))
            {
                return;
            }

            if (_diagnosticsProvider != null)
            {
                _diagnosticsProvider.DiagnosticsChanged -= DiagnosticsProvider_DiagnosticsChanged;
            }

            _diagnosticsProvider = provider;

            if (_diagnosticsProvider != null)
            {
                _diagnosticsProvider.DiagnosticsChanged += DiagnosticsProvider_DiagnosticsChanged;
            }

            UpdateDiagnostics();
        }

        private void DiagnosticsProvider_DiagnosticsChanged(object? sender, EventArgs e)
        {
            UpdateDiagnostics();
        }

        private void SetCurrentComputeCommand(ICommand? command)
        {
            if (ReferenceEquals(_currentComputeCommand, command))
            {
                return;
            }

            if (_currentComputeCommand != null)
            {
                _currentComputeCommand.CanExecuteChanged -= OnCurrentComputeCommandChanged;
            }

            _currentComputeCommand = command;

            if (_currentComputeCommand != null)
            {
                _currentComputeCommand.CanExecuteChanged += OnCurrentComputeCommandChanged;
            }

            OnPropertyChanged(nameof(IsCalculateVisible));
        }

        private void OnCurrentComputeCommandChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsCalculateVisible));
        }

        private void Calculate()
        {
            var command = _currentComputeCommand;
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }

        private async Task ExportAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "econ_toolbox.xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var ead = GetModuleViewModel<EadViewModel>();
                    var agricultureDepthDamage = GetModuleViewModel<AgricultureDepthDamageViewModel>();
                    var updatedCost = GetModuleViewModel<UpdatedCostViewModel>();
                    var annualizer = GetModuleViewModel<AnnualizerViewModel>();
                    var waterDemand = GetModuleViewModel<WaterDemandViewModel>();
                    var udv = GetModuleViewModel<UdvViewModel>();
                    var recreationCapacity = GetModuleViewModel<RecreationCapacityViewModel>();
                    var gantt = GetModuleViewModel<GanttViewModel>();

                    if (ead.IsDirty)
                    {
                        ead.ForceCompute();
                    }
                    RefreshExportData(
                        agricultureDepthDamage,
                        updatedCost,
                        annualizer,
                        waterDemand,
                        udv,
                        recreationCapacity,
                        gantt);
                    await Task.Run(() => _excelExportService.ExportAll(
                        ead,
                        agricultureDepthDamage,
                        updatedCost,
                        annualizer,
                        waterDemand,
                        udv,
                        recreationCapacity,
                        gantt,
                        dlg.FileName));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task SaveProjectAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Econ Toolbox Project (*.etbx)|*.etbx",
                FileName = "econ_toolbox_project.etbx"
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var project = new EconToolboxProject
                {
                    Ead = CaptureState<EadData>(GetModuleViewModel<EadViewModel>()),
                    AgricultureDepthDamage = CaptureState<AgricultureDepthDamageData>(GetModuleViewModel<AgricultureDepthDamageViewModel>()),
                    UpdatedCost = CaptureState<UpdatedCostData>(GetModuleViewModel<UpdatedCostViewModel>()),
                    Annualizer = CaptureState<AnnualizerData>(GetModuleViewModel<AnnualizerViewModel>()),
                    WaterDemand = CaptureState<WaterDemandData>(GetModuleViewModel<WaterDemandViewModel>()),
                    Udv = CaptureState<UdvData>(GetModuleViewModel<UdvViewModel>()),
                    RecreationCapacity = CaptureState<RecreationCapacityData>(GetModuleViewModel<RecreationCapacityViewModel>()),
                    Gantt = CaptureState<GanttData>(GetModuleViewModel<GanttViewModel>()),
                    StageDamageOrganizer = CaptureState<StageDamageOrganizerData>(GetModuleViewModel<StageDamageOrganizerViewModel>())
                };

                string json = JsonSerializer.Serialize(project, CreateProjectJsonOptions());
                await File.WriteAllTextAsync(dlg.FileName, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show($"Save failed: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadProjectAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Econ Toolbox Project (*.etbx)|*.etbx",
                Title = "Open Econ Toolbox Project"
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(dlg.FileName);
                var project = JsonSerializer.Deserialize<EconToolboxProject>(json, CreateProjectJsonOptions());
                if (project == null)
                {
                    MessageBox.Show("Unable to read project data.", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                RestoreState(GetModuleViewModel<EadViewModel>(), project.Ead);
                RestoreState(GetModuleViewModel<AgricultureDepthDamageViewModel>(), project.AgricultureDepthDamage);
                RestoreState(GetModuleViewModel<UpdatedCostViewModel>(), project.UpdatedCost);
                RestoreState(GetModuleViewModel<AnnualizerViewModel>(), project.Annualizer);
                RestoreState(GetModuleViewModel<WaterDemandViewModel>(), project.WaterDemand);
                RestoreState(GetModuleViewModel<UdvViewModel>(), project.Udv);
                RestoreState(GetModuleViewModel<RecreationCapacityViewModel>(), project.RecreationCapacity);
                RestoreState(GetModuleViewModel<GanttViewModel>(), project.Gantt);
                RestoreState(GetModuleViewModel<StageDamageOrganizerViewModel>(), project.StageDamageOrganizer);
                UpdateDiagnostics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show($"Load failed: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static T? CaptureState<T>(BaseViewModel viewModel) where T : class
        {
            if (viewModel is not IStatefulViewModel stateful)
            {
                return null;
            }

            return stateful.CaptureState() as T;
        }

        private static void RestoreState(BaseViewModel viewModel, object? state)
        {
            if (state == null)
            {
                return;
            }

            if (viewModel is IStatefulViewModel stateful)
            {
                stateful.RestoreState(state);
            }
        }

        private static JsonSerializerOptions CreateProjectJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
        }

        private void RefreshExportData(
            AgricultureDepthDamageViewModel agricultureDepthDamage,
            UpdatedCostViewModel updatedCost,
            AnnualizerViewModel annualizer,
            WaterDemandViewModel waterDemand,
            UdvViewModel udv,
            RecreationCapacityViewModel recreationCapacity,
            GanttViewModel gantt)
        {
            if (agricultureDepthDamage.IsDirty)
            {
                ExecuteComputeCommand(agricultureDepthDamage.ComputeCommand);
            }

            if (updatedCost.IsDirty)
            {
                ExecuteComputeCommand(updatedCost.ComputeCommand);
            }

            if (annualizer.IsDirty)
            {
                ExecuteComputeCommand(annualizer.ComputeCommand);
            }

            if (waterDemand.IsDirty)
            {
                ExecuteComputeCommand(waterDemand.ComputeCommand);
            }

            if (udv.IsDirty)
            {
                ExecuteComputeCommand(udv.ComputeCommand);
            }

            if (recreationCapacity.IsDirty)
            {
                ExecuteComputeCommand(recreationCapacity.ComputeCommand);
            }

            if (gantt.IsDirty)
            {
                ExecuteComputeCommand(gantt.ComputeCommand);
            }
        }

        private static void ExecuteComputeCommand(ICommand? command)
        {
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }

        private void UpdateDiagnostics()
        {
            Diagnostics.Clear();

            if (_diagnosticsProvider == null)
            {
                Diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Info,
                    "Select a module",
                    "Choose a module from the explorer to review inputs, run calculations, and view outputs."));
                return;
            }

            var validationResults = _diagnosticsProvider.Diagnostics;
            if (validationResults.Count > 0)
            {
                foreach (var item in validationResults)
                {
                    Diagnostics.Add(item);
                }

                return;
            }

            Diagnostics.Add(new DiagnosticItem(
                DiagnosticLevel.Info,
                "Ready to Calculate",
                $"All inputs for {SelectedModule?.Title} appear valid. You can now run the calculation."));
        }

        private void OnCurrentViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateDiagnostics();
            OnPropertyChanged(nameof(IsCalculateVisible));
        }

        private void UpdateLayoutSettings()
        {
            if (_isApplyingSettings)
            {
                return;
            }

            _layoutSettings.ExplorerPaneWidth = IsExplorerPaneVisible ? ExplorerPaneWidth : _explorerPaneWidthBeforeCollapse;
            _layoutSettings.DetailsPaneWidth = IsDetailsPaneVisible ? DetailsPaneWidth : _detailsPaneWidthBeforeCollapse;
            _layoutSettings.IsExplorerPaneVisible = IsExplorerPaneVisible;
            _layoutSettings.IsDetailsPaneVisible = IsDetailsPaneVisible;
            _layoutSettingsService.Save(_layoutSettings);
        }

        private T GetModuleViewModel<T>() where T : BaseViewModel
        {
            if (_viewModelCache.TryGetValue(typeof(T), out var cached) && cached is T typed)
            {
                return typed;
            }

            var created = (T)_viewModelFactory.Create(typeof(T));
            _viewModelCache[typeof(T)] = created;
            return created;
        }
    }
}
