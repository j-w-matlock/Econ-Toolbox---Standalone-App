using System.ComponentModel;

namespace EconToolbox.Desktop.Services
{
    public interface IAppProgressService : INotifyPropertyChanged
    {
        bool IsActive { get; }
        double ProgressPercent { get; }
        string Message { get; }

        void Start(string message, double percent = 0);
        void Report(string message, double percent);
        void Complete(string? message = null);
        void Fail(string message);
    }
}
