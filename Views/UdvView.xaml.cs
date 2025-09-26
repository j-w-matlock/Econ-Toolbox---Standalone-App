using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class UdvView : UserControl
    {
        public UdvView()
        {
            InitializeComponent();
        }

        private void HistoricalVisitationGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.V || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                return;
            }

            if (sender is not DataGrid dataGrid)
            {
                return;
            }

            if (DataContext is not UdvViewModel viewModel)
            {
                return;
            }

            string? clipboardText;

            try
            {
                clipboardText = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (Exception)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                return;
            }

            var rawLines = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string[]>();

            foreach (var rawLine in rawLines)
            {
                var cells = rawLine.Split('\t');
                if (cells.Length > 0)
                {
                    lines.Add(cells);
                }
            }

            if (lines.Count == 0)
            {
                return;
            }

            var rows = viewModel.HistoricalVisitationRows;

            int startIndex = dataGrid.SelectedIndex >= 0
                ? dataGrid.SelectedIndex
                : rows.Count;

            for (int i = 0; i < lines.Count; i++)
            {
                var values = lines[i];

                HistoricalVisitationRow row;
                int targetIndex = startIndex + i;

                if (targetIndex < rows.Count)
                {
                    row = rows[targetIndex];
                }
                else
                {
                    row = new HistoricalVisitationRow();
                    rows.Add(row);
                }

                if (values.Length >= 2)
                {
                    row.Label = string.IsNullOrWhiteSpace(values[0]) ? null : values[0].Trim();
                    row.VisitationText = values[1].Trim();
                }
                else if (values.Length == 1)
                {
                    row.VisitationText = values[0].Trim();
                }
            }

            e.Handled = true;
        }
    }
}
