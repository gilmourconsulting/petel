using OfficeOpenXml;
using System.Text.RegularExpressions;

namespace Petel.Core.Excel
{
    /// <summary>
    /// Fills an uploaded Excel template by replacing {{placeholder}} tokens
    /// with data values and expanding collection ranges.
    /// </summary>
    public class ExcelTemplateService
    {
        private static readonly Regex PlaceholderRegex =
            new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

        static ExcelTemplateService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Scans a template byte array and returns all unique {{placeholder}} names found.
        /// Used by the mapping UI after template upload.
        /// </summary>
        public IReadOnlyList<string> ScanPlaceholders(byte[] templateBytes)
        {
            using var ms = new MemoryStream(templateBytes);
            using var package = new ExcelPackage(ms);

            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ws in package.Workbook.Worksheets)
            {
                if (ws.Dimension == null) continue;

                for (int row = ws.Dimension.Start.Row; row <= ws.Dimension.End.Row; row++)
                {
                    for (int col = ws.Dimension.Start.Column; col <= ws.Dimension.End.Column; col++)
                    {
                        var text = ws.Cells[row, col].Text;
                        if (string.IsNullOrEmpty(text)) continue;

                        foreach (Match m in PlaceholderRegex.Matches(text))
                            found.Add(m.Groups[1].Value.Trim());
                    }
                }
            }

            return found.OrderBy(p => p).ToList();
        }

        /// <summary>
        /// Fills the template with scalar data values.
        /// Each {{Placeholder}} cell is replaced with the matching value from <paramref name="data"/>.
        /// </summary>
        /// <param name="templateBytes">Original template .xlsx content.</param>
        /// <param name="data">Map of placeholder name → replacement value.</param>
        /// <returns>Filled .xlsx as a byte array.</returns>
        public byte[] FillTemplate(byte[] templateBytes, Dictionary<string, object?> data)
        {
            using var ms = new MemoryStream(templateBytes);
            using var package = new ExcelPackage(ms);

            foreach (var ws in package.Workbook.Worksheets)
            {
                if (ws.Dimension == null) continue;

                for (int row = ws.Dimension.Start.Row; row <= ws.Dimension.End.Row; row++)
                {
                    for (int col = ws.Dimension.Start.Column; col <= ws.Dimension.End.Column; col++)
                    {
                        var cell = ws.Cells[row, col];
                        var text = cell.Text;
                        if (string.IsNullOrEmpty(text)) continue;

                        var newText = PlaceholderRegex.Replace(text, m =>
                        {
                            var key = m.Groups[1].Value.Trim();
                            if (data.TryGetValue(key, out var val))
                                return val?.ToString() ?? string.Empty;
                            return m.Value; // Leave unresolved placeholders as-is
                        });

                        if (newText != text)
                            cell.Value = newText;
                    }
                }
            }

            return package.GetAsByteArray();
        }
    }
}
