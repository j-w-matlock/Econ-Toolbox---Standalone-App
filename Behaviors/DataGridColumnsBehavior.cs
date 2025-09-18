using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EconToolbox.Desktop.Behaviors
{
    public static class DataGridColumnsBehavior
    {
        public static readonly DependencyProperty ColumnsSourceProperty = DependencyProperty.RegisterAttached(
            "ColumnsSource",
            typeof(IEnumerable),
            typeof(DataGridColumnsBehavior),
            new PropertyMetadata(null, OnColumnsSourceChanged));

        private static readonly DependencyProperty ColumnsBindingProperty = DependencyProperty.RegisterAttached(
            "ColumnsBinding",
            typeof(ColumnsBinding),
            typeof(DataGridColumnsBehavior),
            new PropertyMetadata(null));

        public static IEnumerable? GetColumnsSource(DependencyObject obj) => (IEnumerable?)obj.GetValue(ColumnsSourceProperty);

        public static void SetColumnsSource(DependencyObject obj, IEnumerable? value) => obj.SetValue(ColumnsSourceProperty, value);

        private static void OnColumnsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid)
            {
                return;
            }

            if (GetColumnsBinding(dataGrid) is ColumnsBinding oldBinding)
            {
                oldBinding.Detach();
            }

            if (e.NewValue is IEnumerable newSource)
            {
                var binding = new ColumnsBinding(dataGrid, newSource);
                SetColumnsBinding(dataGrid, binding);
            }
            else
            {
                dataGrid.Columns.Clear();
                SetColumnsBinding(dataGrid, null);
            }
        }

        private static ColumnsBinding? GetColumnsBinding(DependencyObject obj)
            => (ColumnsBinding?)obj.GetValue(ColumnsBindingProperty);

        private static void SetColumnsBinding(DependencyObject obj, ColumnsBinding? value)
            => obj.SetValue(ColumnsBindingProperty, value);

        private sealed class ColumnsBinding
        {
            private readonly DataGrid _dataGrid;
            private readonly IEnumerable _source;

            public ColumnsBinding(DataGrid dataGrid, IEnumerable source)
            {
                _dataGrid = dataGrid;
                _source = source;

                if (source is INotifyCollectionChanged notify)
                {
                    notify.CollectionChanged += OnCollectionChanged;
                }

                foreach (var descriptor in source.OfType<INotifyPropertyChanged>())
                {
                    descriptor.PropertyChanged += OnDescriptorPropertyChanged;
                }

                RebuildColumns();
            }

            private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                if (sender is INotifyCollectionChanged notify)
                {
                    if (e.OldItems != null)
                    {
                        foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                        {
                            item.PropertyChanged -= OnDescriptorPropertyChanged;
                        }
                    }

                    if (e.NewItems != null)
                    {
                        foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                        {
                            item.PropertyChanged += OnDescriptorPropertyChanged;
                        }
                    }
                }

                RebuildColumns();
            }

            private void OnDescriptorPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(DataGridColumnDescriptor.HeaderText))
                {
                    UpdateHeaders();
                }
            }

            public void Detach()
            {
                if (_source is INotifyCollectionChanged notify)
                {
                    notify.CollectionChanged -= OnCollectionChanged;
                }

                foreach (var descriptor in _source.OfType<INotifyPropertyChanged>())
                {
                    descriptor.PropertyChanged -= OnDescriptorPropertyChanged;
                }

                _dataGrid.Columns.Clear();
            }

            private void RebuildColumns()
            {
                _dataGrid.Columns.Clear();

                foreach (var descriptor in _source.OfType<DataGridColumnDescriptor>())
                {
                    _dataGrid.Columns.Add(CreateColumn(descriptor));
                }
            }

            private void UpdateHeaders()
            {
                int index = 0;
                foreach (var descriptor in _source.OfType<DataGridColumnDescriptor>())
                {
                    if (index < _dataGrid.Columns.Count)
                    {
                        _dataGrid.Columns[index].Header = CreateHeader(descriptor);
                    }

                    index++;
                }
            }

            private static DataGridColumn CreateColumn(DataGridColumnDescriptor descriptor)
            {
                var binding = new Binding(descriptor.BindingPath)
                {
                    Mode = descriptor.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    ValidatesOnDataErrors = true,
                    ValidatesOnExceptions = true
                };

                var column = new DataGridTextColumn
                {
                    Binding = binding,
                    IsReadOnly = descriptor.IsReadOnly,
                    SortMemberPath = descriptor.BindingPath
                };

                if (descriptor.Width.HasValue)
                {
                    column.Width = new DataGridLength(descriptor.Width.Value);
                }

                if (descriptor.MinWidth.HasValue)
                {
                    column.MinWidth = descriptor.MinWidth.Value;
                }

                column.Header = CreateHeader(descriptor);

                return column;
            }

            private static object CreateHeader(DataGridColumnDescriptor descriptor)
            {
                if (descriptor.HeaderContext != null && !string.IsNullOrWhiteSpace(descriptor.HeaderBindingPath))
                {
                    if (descriptor.IsHeaderEditable)
                    {
                        var headerBox = new TextBox
                        {
                            DataContext = descriptor.HeaderContext,
                            Padding = new Thickness(4, 2, 4, 2),
                            MinWidth = descriptor.MinWidth ?? 80,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            BorderThickness = new Thickness(0),
                            Background = System.Windows.Media.Brushes.Transparent
                        };

                        headerBox.SetBinding(TextBox.TextProperty, new Binding(descriptor.HeaderBindingPath)
                        {
                            Mode = BindingMode.TwoWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                        });

                        return headerBox;
                    }

                    var headerText = new TextBlock
                    {
                        DataContext = descriptor.HeaderContext,
                        Margin = new Thickness(0),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    headerText.SetBinding(TextBlock.TextProperty, new Binding(descriptor.HeaderBindingPath));
                    return headerText;
                }

                return descriptor.HeaderText ?? descriptor.BindingPath;
            }
        }
    }
}
