using System.Windows;
using System.Windows.Controls;

namespace EconToolbox.Desktop.Behaviors
{
    public static class DataGridCommitBehavior
    {
        public static readonly DependencyProperty CommitOnLostFocusProperty = DependencyProperty.RegisterAttached(
            "CommitOnLostFocus",
            typeof(bool),
            typeof(DataGridCommitBehavior),
            new PropertyMetadata(false, OnCommitOnLostFocusChanged));

        public static bool GetCommitOnLostFocus(DependencyObject obj)
            => (bool)obj.GetValue(CommitOnLostFocusProperty);

        public static void SetCommitOnLostFocus(DependencyObject obj, bool value)
            => obj.SetValue(CommitOnLostFocusProperty, value);

        private static void OnCommitOnLostFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid)
            {
                return;
            }

            dataGrid.LostKeyboardFocus -= OnDataGridLostKeyboardFocus;
            dataGrid.Unloaded -= OnDataGridUnloaded;

            if (e.NewValue is true)
            {
                dataGrid.LostKeyboardFocus += OnDataGridLostKeyboardFocus;
                dataGrid.Unloaded += OnDataGridUnloaded;
            }
        }

        private static void OnDataGridUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGrid dataGrid)
            {
                return;
            }

            dataGrid.LostKeyboardFocus -= OnDataGridLostKeyboardFocus;
            dataGrid.Unloaded -= OnDataGridUnloaded;
        }

        private static void OnDataGridLostKeyboardFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGrid dataGrid)
            {
                return;
            }

            if (dataGrid.IsKeyboardFocusWithin)
            {
                return;
            }

            dataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            dataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }
}
