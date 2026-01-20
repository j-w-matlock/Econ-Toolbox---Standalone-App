using System;
using System.Collections.Generic;

namespace EconToolbox.Desktop.ViewModels
{
    public interface IDiagnosticsProvider
    {
        IReadOnlyList<DiagnosticItem> Diagnostics { get; }

        event EventHandler? DiagnosticsChanged;
    }
}
