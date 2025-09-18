using System.Windows.Controls;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class DrawingView : UserControl
    {
        public DrawingView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DrawingViewModel vm)
            {
                Canvas.DefaultDrawingAttributes = vm.DrawingAttributes;
            }
        }
    }
}
