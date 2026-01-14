using System;

namespace EconToolbox.Desktop.ViewModels
{
    public enum DiagnosticLevel
    {
        Info,
        Warning,
        Advisory,
        Error
    }

    public class DiagnosticItem
    {
        public DiagnosticItem(DiagnosticLevel level, string title, string description)
        {
            Level = level;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public DiagnosticLevel Level { get; }

        public string Title { get; }

        public string Description { get; }
    }
}
