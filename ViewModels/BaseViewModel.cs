using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.ViewModels
{
    public abstract class BaseViewModel : ObservableObject
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
    }
}
