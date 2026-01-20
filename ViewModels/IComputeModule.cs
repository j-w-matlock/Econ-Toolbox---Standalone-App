using System.Windows.Input;

namespace EconToolbox.Desktop.ViewModels
{
    public interface IComputeModule
    {
        ICommand ComputeCommand { get; }
    }
}
