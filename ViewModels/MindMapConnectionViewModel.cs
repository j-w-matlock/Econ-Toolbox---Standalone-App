using System;
using System.ComponentModel;

namespace EconToolbox.Desktop.ViewModels
{
    public class MindMapConnectionViewModel : BaseViewModel, IDisposable
    {
        public MindMapConnectionViewModel(MindMapNodeViewModel source, MindMapNodeViewModel target)
        {
            Source = source;
            Target = target;

            Source.PropertyChanged += OnNodePropertyChanged;
            Target.PropertyChanged += OnNodePropertyChanged;

            RaisePositionChanges();
        }

        public MindMapNodeViewModel Source { get; }
        public MindMapNodeViewModel Target { get; }

        public double StartX => Source.X + Source.VisualWidth / 2;
        public double StartY => Source.Y + Source.VisualHeight / 2;
        public double EndX => Target.X + Target.VisualWidth / 2;
        public double EndY => Target.Y + Target.VisualHeight / 2;

        public void Dispose()
        {
            Source.PropertyChanged -= OnNodePropertyChanged;
            Target.PropertyChanged -= OnNodePropertyChanged;
        }

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MindMapNodeViewModel.X) ||
                e.PropertyName == nameof(MindMapNodeViewModel.Y) ||
                e.PropertyName == nameof(MindMapNodeViewModel.VisualWidth) ||
                e.PropertyName == nameof(MindMapNodeViewModel.VisualHeight))
            {
                RaisePositionChanges();
            }
        }

        private void RaisePositionChanges()
        {
            OnPropertyChanged(nameof(StartX));
            OnPropertyChanged(nameof(StartY));
            OnPropertyChanged(nameof(EndX));
            OnPropertyChanged(nameof(EndY));
        }
    }
}
