using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EconToolbox.Desktop.ViewModels
{
    public class ModuleDefinition
    {
        public ModuleDefinition(
            string title,
            string description,
            string functionSummary,
            IEnumerable<string> inputSteps,
            IEnumerable<string> outputHighlights,
            string example,
            Type viewModelType)
        {
            Title = title;
            Description = description;
            FunctionSummary = functionSummary;
            InputSteps = new ReadOnlyCollection<string>(inputSteps.ToList());
            OutputHighlights = new ReadOnlyCollection<string>(outputHighlights.ToList());
            Example = example;
            if (!typeof(BaseViewModel).IsAssignableFrom(viewModelType))
            {
                throw new ArgumentException("Module view model types must derive from BaseViewModel.", nameof(viewModelType));
            }

            ViewModelType = viewModelType;
        }

        public string Title { get; }

        public string Description { get; }

        public string FunctionSummary { get; }

        public IReadOnlyList<string> InputSteps { get; }

        public IReadOnlyList<string> OutputHighlights { get; }

        public string Example { get; }

        public Type ViewModelType { get; }

        public string ExplorerCategory => Title switch
        {
            "Cost Annualization" or "Expected Annual Damage (EAD)" => "Annualization and Cost",
            "Unit Day Value" or "Traffic Delay Analysis" or "Advanced Bridge Replacement" => "Benefits",
            "Updated Cost of Storage" or "Water Demand Forecasting" => "Water Management",
            "Stage Damage Organizer" or "Shapefile Uncertainty Statistics" => "Data Analysis",
            _ => "Other Mods"
        };

        public int ExplorerCategoryOrder => ExplorerCategory switch
        {
            "Annualization and Cost" => 0,
            "Benefits" => 1,
            "Water Management" => 2,
            "Data Analysis" => 3,
            _ => 4
        };
    }
}
