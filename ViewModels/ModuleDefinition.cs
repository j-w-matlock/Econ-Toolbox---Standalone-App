using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace EconToolbox.Desktop.ViewModels
{
    public class ModuleDefinition
    {
        public ModuleDefinition(
            string title,
            string description,
            IEnumerable<string> inputSteps,
            IEnumerable<string> outputHighlights,
            BaseViewModel contentViewModel,
            ICommand? computeCommand)
        {
            Title = title;
            Description = description;
            InputSteps = new ReadOnlyCollection<string>(inputSteps.ToList());
            OutputHighlights = new ReadOnlyCollection<string>(outputHighlights.ToList());
            ContentViewModel = contentViewModel;
            ComputeCommand = computeCommand;
        }

        public string Title { get; }

        public string Description { get; }

        public IReadOnlyList<string> InputSteps { get; }

        public IReadOnlyList<string> OutputHighlights { get; }

        public BaseViewModel ContentViewModel { get; }

        public ICommand? ComputeCommand { get; }
    }
}
