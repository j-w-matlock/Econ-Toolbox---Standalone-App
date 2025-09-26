using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.ViewModels;

namespace EconToolbox.Desktop.Views
{
    public partial class UdvView : UserControl
    {
        private readonly Stack<List<HistoricalVisitationRow>> _undoStack = new();
        private readonly Stack<List<HistoricalVisitationRow>> _redoStack = new();

        public UdvView()
        {
            InitializeComponent();
        }

        private void HistoricalVisitationGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
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

            switch (e.Key)
            {
                case Key.V:
                    HandlePaste(dataGrid, viewModel, e);
                    break;
                case Key.Z:
                    HandleUndo(viewModel, e);
                    break;
                case Key.R:
                    HandleRedo(viewModel, e);
                    break;
            }
        }

        private void HandlePaste(DataGrid dataGrid, UdvViewModel viewModel, KeyEventArgs e)
        {
            if (!TryGetClipboardLines(out var lines))
            {
                return;
            }

            var orderedColumns = dataGrid.Columns
                .OrderBy(c => c.DisplayIndex)
                .ToList();

            if (lines.Count == 0 || orderedColumns.Count == 0)
            {
                return;
            }

            dataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            dataGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var rows = viewModel.HistoricalVisitationRows;

            _undoStack.Push(CloneRows(rows));
            _redoStack.Clear();

            var currentCell = dataGrid.CurrentCell;
            object? currentItem = currentCell.IsValid ? currentCell.Item : null;
            if (Equals(currentItem, CollectionView.NewItemPlaceholder))
            {
                currentItem = null;
            }

            int startRowIndex = currentItem != null ? dataGrid.Items.IndexOf(currentItem) : -1;
            if (startRowIndex < 0)
            {
                startRowIndex = dataGrid.SelectedIndex >= 0 ? dataGrid.SelectedIndex : rows.Count;
            }

            int startColumnIndex = currentCell.Column?.DisplayIndex ?? 0;

            for (int i = 0; i < lines.Count; i++)
            {
                var values = lines[i];
                if (values.Length == 0)
                {
                    continue;
                }

                int targetRowIndex = startRowIndex + i;
                HistoricalVisitationRow row;
                if (targetRowIndex < rows.Count)
                {
                    row = rows[targetRowIndex];
                }
                else
                {
                    row = new HistoricalVisitationRow();
                    rows.Add(row);
                }

                for (int j = 0; j < values.Length && startColumnIndex + j < orderedColumns.Count; j++)
                {
                    ApplyCellValue(row, orderedColumns[startColumnIndex + j], values[j]);
                }
            }

            e.Handled = true;
        }

        private void HandleUndo(UdvViewModel viewModel, KeyEventArgs e)
        {
            if (_undoStack.Count == 0)
            {
                return;
            }

            var rows = viewModel.HistoricalVisitationRows;
            _redoStack.Push(CloneRows(rows));

            var snapshot = _undoStack.Pop();
            ApplySnapshot(rows, snapshot);

            e.Handled = true;
        }

        private void HandleRedo(UdvViewModel viewModel, KeyEventArgs e)
        {
            if (_redoStack.Count == 0)
            {
                return;
            }

            var rows = viewModel.HistoricalVisitationRows;
            _undoStack.Push(CloneRows(rows));

            var snapshot = _redoStack.Pop();
            ApplySnapshot(rows, snapshot);

            e.Handled = true;
        }

        private static bool TryGetClipboardLines(out List<string[]> lines)
        {
            lines = new List<string[]>();

            string? clipboardText;

            try
            {
                clipboardText = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (Exception)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                return false;
            }

            var rawLines = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in rawLines)
            {
                var cells = rawLine.Split('\t');
                if (cells.Length > 0)
                {
                    lines.Add(cells);
                }
            }

            return lines.Count > 0;
        }

        private static void ApplyCellValue(HistoricalVisitationRow row, DataGridColumn column, string rawValue)
        {
            if (column is not DataGridBoundColumn boundColumn)
            {
                return;
            }

            if (boundColumn.Binding is not Binding binding || string.IsNullOrEmpty(binding.Path?.Path))
            {
                return;
            }

            string trimmed = rawValue.Trim();

            switch (binding.Path.Path)
            {
                case nameof(HistoricalVisitationRow.Label):
                    row.Label = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
                    break;
                case nameof(HistoricalVisitationRow.VisitationText):
                    row.VisitationText = trimmed;
                    break;
            }
        }

        private static List<HistoricalVisitationRow> CloneRows(IEnumerable<HistoricalVisitationRow> rows)
        {
            return rows
                .Select(r => new HistoricalVisitationRow
                {
                    Label = r.Label,
                    VisitationText = r.VisitationText,
                })
                .ToList();
        }

        private static void ApplySnapshot(ObservableCollection<HistoricalVisitationRow> target, IReadOnlyList<HistoricalVisitationRow> snapshot)
        {
            target.Clear();

            foreach (var row in snapshot)
            {
                target.Add(new HistoricalVisitationRow
                {
                    Label = row.Label,
                    VisitationText = row.VisitationText,
                });
            }
        }
    }
}
