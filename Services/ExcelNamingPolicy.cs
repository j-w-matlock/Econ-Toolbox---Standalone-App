using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ClosedXML.Excel;

namespace EconToolbox.Desktop.Services
{
    public static class ExcelNamingPolicy
    {
        public const int ExcelMaxPictureNameLength = 31;
        public const int ExcelMaxWorksheetNameLength = 31;
        public const int ExcelMaxTableNameLength = 255;
        public const int ExcelMaxHeaderLength = 255;

        private static readonly char[] InvalidWorksheetCharacters = { ':', '\\', '/', '?', '*', '[', ']' };

        public static string NormalizeWhitespace(string input)
        {
            var chars = input.Select(c => char.IsWhiteSpace(c) ? ' ' : c).ToArray();
            var normalized = new string(chars).Trim();
            while (normalized.Contains("  "))
            {
                normalized = normalized.Replace("  ", " ");
            }

            return normalized;
        }

        public static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        public static string CreateUniqueName(
            string baseName,
            HashSet<string> usedNames,
            int maxLength,
            string defaultPrefix,
            bool requireLetterStart,
            Action<string>? log = null)
        {
            string sanitized = NormalizeWhitespace(baseName ?? string.Empty);
            sanitized = new string(sanitized.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            sanitized = sanitized.Replace('-', '_');

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = defaultPrefix;
                log?.Invoke($"Defaulting empty name to '{sanitized}'.");
            }

            if (requireLetterStart && !char.IsLetter(sanitized[0]))
            {
                sanitized = $"{defaultPrefix}_{sanitized}";
                log?.Invoke($"Prefixing name to start with a letter: '{sanitized}'.");
            }

            string truncated = Truncate(sanitized, maxLength);
            if (!string.Equals(truncated, sanitized, StringComparison.Ordinal))
            {
                log?.Invoke($"Truncated name '{sanitized}' to '{truncated}' to meet a {maxLength} character limit.");
            }

            string candidate = truncated;
            int suffix = 1;
            while (!usedNames.Add(candidate))
            {
                string suffixText = $"_{suffix}";
                int prefixLength = Math.Max(1, maxLength - suffixText.Length);
                string prefix = candidate.Length > prefixLength ? candidate[..prefixLength] : candidate;
                candidate = prefix + suffixText;
                suffix++;
            }

            if (!string.Equals(candidate, truncated, StringComparison.Ordinal))
            {
                log?.Invoke($"Adjusted name to ensure uniqueness: '{truncated}' -> '{candidate}'.");
            }

            return candidate;
        }

        public static string CreateWorksheetName(XLWorkbook workbook, string baseName, Action<string>? log = null)
        {
            if (workbook == null) throw new ArgumentNullException(nameof(workbook));

            string sanitized = string.IsNullOrWhiteSpace(baseName) ? "Sheet" : NormalizeWhitespace(baseName);
            string original = baseName ?? string.Empty;

            foreach (char invalid in InvalidWorksheetCharacters)
            {
                sanitized = sanitized.Replace(invalid, ' ');
            }

            sanitized = sanitized.Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "Sheet";
                log?.Invoke("Worksheet name was empty after sanitization; defaulting to 'Sheet'.");
            }

            string trimmed = Truncate(sanitized, ExcelMaxWorksheetNameLength);
            if (!string.Equals(trimmed, sanitized, StringComparison.Ordinal))
            {
                log?.Invoke($"Truncated worksheet name '{sanitized}' to '{trimmed}'.");
            }

            string candidate = trimmed;
            int suffix = 1;
            while (workbook.Worksheets.Any(ws => string.Equals(ws.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                string suffixText = $"_{suffix}";
                int maxLength = ExcelMaxWorksheetNameLength - suffixText.Length;
                maxLength = Math.Max(1, maxLength);
                string prefix = trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
                candidate = prefix + suffixText;
                suffix++;
            }

            if (!string.Equals(candidate, trimmed, StringComparison.Ordinal))
            {
                log?.Invoke($"Adjusted worksheet name for uniqueness: '{trimmed}' -> '{candidate}'.");
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                throw new InvalidOperationException($"Unable to generate a valid worksheet name from '{original}'.");
            }

            return candidate;
        }

        public static List<string> SanitizeHeaders(IEnumerable<string> headers, string fallbackPrefix, Action<string>? log = null)
        {
            var sanitizedHeaders = new List<string>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int index = 1;

            foreach (var header in headers)
            {
                string value = string.IsNullOrWhiteSpace(header) ? $"{fallbackPrefix} {index}" : NormalizeWhitespace(header);
                string truncated = Truncate(value, ExcelMaxHeaderLength);

                if (!string.Equals(truncated, value, StringComparison.Ordinal))
                {
                    log?.Invoke($"Truncated header '{value}' to '{truncated}' to meet Excel limits.");
                }

                string candidate = truncated;
                int suffix = 2;
                while (!used.Add(candidate))
                {
                    string suffixText = $" ({suffix})";
                    int maxLength = ExcelMaxHeaderLength - suffixText.Length;
                    candidate = Truncate(truncated, Math.Max(1, maxLength)) + suffixText;
                    suffix++;
                }

                if (!string.Equals(candidate, truncated, StringComparison.Ordinal))
                {
                    log?.Invoke($"Adjusted header for uniqueness: '{truncated}' -> '{candidate}'.");
                }

                sanitizedHeaders.Add(candidate);
                index++;
            }

            return sanitizedHeaders;
        }
    }
}
