using System.Diagnostics;
using System.Windows.Controls;

namespace EconToolbox.Desktop.Views
{
    public partial class AnnualizerView : UserControl
    {
        public AnnualizerView()
        {
            InitializeComponent();
        }

        private void DataGrid_OnError(object sender, ValidationErrorEventArgs e)
        {
            if (e.Exception == null)
                return;

            var context = sender is DataGrid grid ? grid.Name : "Annualizer grid";
            var details = $"[{context}] {e.Exception}";
            Debug.WriteLine(details);
            e.Handled = true;
        }
    }
}
