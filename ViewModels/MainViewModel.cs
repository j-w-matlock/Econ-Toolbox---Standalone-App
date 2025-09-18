using System.Collections.Generic;
using System.Windows.Input;
using EconToolbox.Desktop.Services;

namespace EconToolbox.Desktop.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public EadViewModel Ead { get; } = new();
        public UpdatedCostViewModel UpdatedCost { get; } = new();
        public AnnualizerViewModel Annualizer { get; } = new();
        public UdvViewModel Udv { get; } = new();
        public WaterDemandViewModel WaterDemand { get; } = new();
        public MindMapViewModel MindMap { get; } = new();

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
            }
        }

        public ICommand CalculateCommand { get; }
        public ICommand ExportCommand { get; }

        public ModuleDefinition? SelectedModule => SelectedIndex >= 0 && SelectedIndex < Modules.Count
            ? Modules[SelectedIndex]
            : null;

        public bool IsCalculateVisible => SelectedModule?.ComputeCommand?.CanExecute(null) == true;

        public MainViewModel()
        {
            CalculateCommand = new RelayCommand(Calculate);
            ExportCommand = new RelayCommand(Export);

            Modules = new List<ModuleDefinition>
            {
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
                    Ead,
                    Ead.ComputeCommand),
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
                    MindMap,
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

        private void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "econ_toolbox.xlsx"
            };
            if (dlg.ShowDialog() == true)
            {
                ExcelExporter.ExportAll(Ead, UpdatedCost, Annualizer, WaterDemand, Udv, MindMap, dlg.FileName);
            }
        }
    }
}
