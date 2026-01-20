using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace EconToolbox.Desktop.ViewModels
{
    public abstract class DiagnosticViewModelBase : BaseViewModel, IDiagnosticsProvider
    {
        private readonly ObservableCollection<DiagnosticItem> _diagnostics = new();

        public IReadOnlyList<DiagnosticItem> Diagnostics => _diagnostics;

        public event EventHandler? DiagnosticsChanged;

        protected abstract IEnumerable<DiagnosticItem> BuildDiagnostics();

        protected void RefreshDiagnostics()
        {
            UpdateDiagnostics(BuildDiagnostics());
        }

        protected void UpdateDiagnostics(IEnumerable<DiagnosticItem> diagnostics)
        {
            _diagnostics.Clear();
            foreach (var item in diagnostics)
            {
                _diagnostics.Add(item);
            }

            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }

    }
}
