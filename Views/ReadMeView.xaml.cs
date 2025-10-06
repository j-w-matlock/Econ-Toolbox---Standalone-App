using System;
using System.Diagnostics;
using System.Windows.Controls;
using Markdig.Wpf;

namespace EconToolbox.Desktop.Views
{
    public partial class ReadMeView : UserControl
    {
        public ReadMeView()
        {
            InitializeComponent();
        }

        private void MarkdownViewer_OnLinkClicked(object sender, LinkEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Link))
            {
                return;
            }

            try
            {
                if (Uri.TryCreate(e.Link, UriKind.Absolute, out var uri))
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
            }
            catch
            {
                // Swallow exceptions to avoid crashing the UI when a link cannot be opened.
            }
        }
    }
}
