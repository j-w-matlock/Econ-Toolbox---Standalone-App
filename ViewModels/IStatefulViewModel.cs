namespace EconToolbox.Desktop.ViewModels
{
    public interface IStatefulViewModel
    {
        object CaptureState();
        void RestoreState(object state);
    }
}
