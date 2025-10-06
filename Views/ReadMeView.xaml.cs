using System;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace EconToolbox.Desktop.Views
{
    public partial class ReadMeView : UserControl
    {
        public ReadMeView()
        {
            InitializeComponent();

            MarkdownViewer.AddHandler(Hyperlink.RequestNavigateEvent,
                new RequestNavigateEventHandler(MarkdownViewer_OnRequestNavigate));
        }

        private void MarkdownViewer_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                var link = e.Uri?.AbsoluteUri ?? e.Uri?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(link) && Uri.TryCreate(link, UriKind.Absolute, out var uri))
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
            }
            catch
            {
                // Swallow exceptions to avoid crashing the UI when a link cannot be opened.
            }

            e.Handled = true;
        }
    }
}
