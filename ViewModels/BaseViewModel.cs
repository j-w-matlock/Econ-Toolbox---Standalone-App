using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.ViewModels
{
    public abstract class BaseViewModel : ObservableObject, IStatefulViewModel
    {
        private bool _isDirty;

        protected BaseViewModel()
        {
            PropertyChanged += OnBasePropertyChanged;
        }

        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
        }

        protected void MarkDirty()
        {
            if (!IsDirty)
            {
                IsDirty = true;
            }
        }

        protected void MarkClean()
        {
            IsDirty = false;
        }

        private void OnBasePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsDirty))
            {
                return;
            }

            MarkDirty();
        }

        public virtual object CaptureState()
        {
            throw new NotSupportedException("State capture is not supported for this view model.");
        }

        public virtual void RestoreState(object state)
        {
            throw new NotSupportedException("State restore is not supported for this view model.");
        }
    }
}
