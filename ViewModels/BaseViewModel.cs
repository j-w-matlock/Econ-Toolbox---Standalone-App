using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EconToolbox.Desktop.ViewModels
{
    public abstract class BaseViewModel : ObservableObject
    {
        private bool _isDirty;

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

        protected override void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            base.OnPropertyChanged(name);
            if (name == nameof(IsDirty))
            {
                return;
            }

            MarkDirty();
        }
    }
}
