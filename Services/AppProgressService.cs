using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EconToolbox.Desktop.Services
{
    public sealed class AppProgressService : IAppProgressService
    {
        private bool _isActive;
        private double _progressPercent;
        private string _message = "Ready";

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged();
            }
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            private set
            {
                var clamped = Math.Clamp(value, 0, 100);
                if (Math.Abs(_progressPercent - clamped) < 0.1) return;
                _progressPercent = clamped;
                OnPropertyChanged();
            }
        }

        public string Message
        {
            get => _message;
            private set
            {
                if (string.Equals(_message, value, StringComparison.Ordinal)) return;
                _message = value;
                OnPropertyChanged();
            }
        }

        public void Start(string message, double percent = 0)
        {
            IsActive = true;
            Message = string.IsNullOrWhiteSpace(message) ? "Working..." : message;
            ProgressPercent = percent;
        }

        public void Report(string message, double percent)
        {
            IsActive = true;
            Message = string.IsNullOrWhiteSpace(message) ? Message : message;
            ProgressPercent = percent;
        }

        public void Complete(string? message = null)
        {
            ProgressPercent = 100;
            Message = string.IsNullOrWhiteSpace(message) ? "Complete" : message;
            IsActive = false;
        }

        public void Fail(string message)
        {
            Message = string.IsNullOrWhiteSpace(message) ? "Failed" : message;
            IsActive = false;
            ProgressPercent = 0;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
