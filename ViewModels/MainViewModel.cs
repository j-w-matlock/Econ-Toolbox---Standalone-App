using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public ReadMeViewModel ReadMe { get; }
        public EadViewModel Ead { get; }
        public AgricultureDepthDamageViewModel AgricultureDepthDamage { get; }
        public UpdatedCostViewModel UpdatedCost { get; }
        public AnnualizerViewModel Annualizer { get; }
        public UdvViewModel Udv { get; }
        public WaterDemandViewModel WaterDemand { get; }
        public MindMapViewModel MindMap { get; }
        public GanttViewModel Gantt { get; }
        public DrawingViewModel Drawing { get; }

        public IReadOnlyList<ModuleDefinition> Modules { get; }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCalculateVisible));
                OnPropertyChanged(nameof(SelectedModule));
                OnPropertyChanged(nameof(PrimaryActionLabel));
            }
        }

        public IRelayCommand CalculateCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }

        public ModuleDefinition? SelectedModule => SelectedIndex >= 0 && SelectedIndex < Modules.Count
            ? Modules[SelectedIndex]
            : null;

        public bool IsCalculateVisible => SelectedModule?.ComputeCommand?.CanExecute(null) == true;

        public string PrimaryActionLabel => SelectedModule?.Title switch
        {
            "Water Demand Forecasting" => "Forecast",
            "Standard Gantt Planner" => "Schedule",
            _ => "Calculate"
        };

        private readonly IExcelExportService _excelExportService;

        public MainViewModel(
            ReadMeViewModel readMe,
            EadViewModel ead,
            AgricultureDepthDamageViewModel agricultureDepthDamage,
            UpdatedCostViewModel updatedCost,
            AnnualizerViewModel annualizer,
            UdvViewModel udv,
            WaterDemandViewModel waterDemand,
            MindMapViewModel mindMap,
            GanttViewModel gantt,
            DrawingViewModel drawing,
            IExcelExportService excelExportService)
        {
            ReadMe = readMe;
            Ead = ead;
            AgricultureDepthDamage = agricultureDepthDamage;
            UpdatedCost = updatedCost;
            Annualizer = annualizer;
            Udv = udv;
            WaterDemand = waterDemand;
            MindMap = mindMap;
            Gantt = gantt;
            Drawing = drawing;
            _excelExportService = excelExportService;

            CalculateCommand = new RelayCommand(Calculate);
            ExportCommand = new AsyncRelayCommand(ExportAsync);

            Modules = new List<ModuleDefinition>
            {
                new ModuleDefinition(
                    "Project README",
                    "Review onboarding tips, module descriptions, and build instructions without leaving the toolbox.",
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
                    ReadMe,
                    null),
                new ModuleDefinition(
                    "Expected Annual Damage (EAD)",
                    "Quantify how frequently damages occur by pairing exceedance probabilities with damage estimates.",
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
                    Ead,
                    Ead.ComputeCommand),
                new ModuleDefinition(
                    "Agriculture Depth-Damage",
                    "Calibrate crop and structure damages for agricultural assets using custom depth-damage relationships.",
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
                    AgricultureDepthDamage,
                    AgricultureDepthDamage.ComputeCommand),
                new ModuleDefinition(
                    "Updated Cost of Storage",
                    "Update historical costs and allocate joint expenses based on storage recommendations.",
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
                    UpdatedCost,
                    UpdatedCost.ComputeCommand),
                new ModuleDefinition(
                    "Cost Annualization",
                    "Translate capital outlays and future costs into comparable annual values.",
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
                    Annualizer,
                    Annualizer.ComputeCommand),
                new ModuleDefinition(
                    "Water Demand Forecasting",
                    "Project future water demand scenarios from historical data and planning assumptions.",
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
                    WaterDemand,
                    WaterDemand.ComputeCommand),
                new ModuleDefinition(
                    "Unit Day Value",
                    "Calibrate recreational benefits and visitation patterns to compute annual unit day values.",
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
                    Udv,
                    Udv.ComputeCommand),
                new ModuleDefinition(
                    "Mind Map Workspace",
                    "Capture qualitative insights, organize themes, and track next steps collaboratively.",
                    new[]
                    {
                        "Start with the seeded central idea or add a new root topic to frame your analysis.",
                        "Use the toolbar to add child and sibling nodes, recording notes and owners as you go.",
                        "Arrange nodes on the canvas and adjust zoom to communicate the story effectively."
                    },
                    new[]
                    {
                        "Maintains a navigable mind map with connection lines and breadcrumb paths.",
                        "Supports quick export alongside other modules for project documentation.",
                        "Provides an always-on workspace to summarize qualitative findings."
                    },
                    "Example: Mapping insights from a coastal storm resilience workshop organizes nodes for risk drivers, mitigation concepts, funding leads, and assigned follow-up tasks.",
                    MindMap,
                    null),
                new ModuleDefinition(
                    "Standard Gantt Planner",
                    "Organize project activities, dependencies, and milestones in a timeline consistent with industry schedules.",
                    new[]
                    {
                        "List each task with a start date, duration, and responsible workstream.",
                        "Identify predecessors to respect finish-to-start dependencies across the plan.",
                        "Adjust pen thickness and color in the sketch tab to annotate delivery risks or notes."
                    },
                    new[]
                    {
                        "Automatically sequences start and finish dates based on dependency logic.",
                        "Generates a bar chart showing duration, percent complete, and milestones.",
                        "Exports both the task register and timeline graphic alongside other modules."
                    },
                    "Example: A feasibility study includes kickoff, stakeholder workshops, baseline analysis, and a design milestone with finish-to-start dependencies.",
                    Gantt,
                    Gantt.ComputeCommand),
                new ModuleDefinition(
                    "Sketch Pad",
                    "Capture freehand notes, diagrams, or signatures directly in the toolbox.",
                    new[]
                    {
                        "Pick a pen color and thickness that complements your drawing style.",
                        "Draw directly on the canvas using the mouse or stylus.",
                        "Use undo or clear to refine the canvas before exporting."
                    },
                    new[]
                    {
                        "Maintains stroke data for export to Excel as an image.",
                        "Supports multiple pen widths and color palettes for rapid sketching.",
                        "Provides an always-available space to annotate workshop takeaways."
                    },
                    "Example: Sketching a reservoir layout with notes about control structures during a design charrette.",
                    Drawing,
                    null)
            };

            foreach (var module in Modules)
            {
                SubscribeToComputeCommand(module.ComputeCommand);
            }
        }

        private void Calculate()
        {
            var command = SelectedModule?.ComputeCommand;
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }

        private void SubscribeToComputeCommand(ICommand? command)
        {
            if (command == null)
            {
                return;
            }

            command.CanExecuteChanged += (_, _) => OnPropertyChanged(nameof(IsCalculateVisible));
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
                await Task.Run(() => _excelExportService.ExportAll(
                    Ead,
                    UpdatedCost,
                    Annualizer,
                    WaterDemand,
                    Udv,
                    MindMap,
                    Gantt,
                    Drawing,
                    dlg.FileName));
            }
        }
    }
}
