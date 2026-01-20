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
    }
}
