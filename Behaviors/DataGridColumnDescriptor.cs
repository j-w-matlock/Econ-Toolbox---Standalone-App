using System.ComponentModel;

namespace EconToolbox.Desktop.Behaviors
{
    public class DataGridColumnDescriptor : INotifyPropertyChanged
    {
        private string? _headerText;

        public DataGridColumnDescriptor(string bindingPath)
        {
            BindingPath = bindingPath;
        }

        public string BindingPath { get; }

        public string? HeaderText
        {
            get => _headerText;
            set
            {
                if (_headerText == value)
                {
                    return;
                }

                _headerText = value;
                OnPropertyChanged(nameof(HeaderText));
            }
        }

        public bool IsReadOnly { get; init; }

        public double? Width { get; init; }

        public double? MinWidth { get; init; }

        public object? HeaderContext { get; init; }

        public string? HeaderBindingPath { get; init; }

        public bool IsHeaderEditable { get; init; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
