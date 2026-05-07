using OfficeOpenXml;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Petel.Core.Excel
{
    /// <summary>
    /// Fills an Excel template from a <see cref="ReportDefinition"/>.
    ///
    /// Template syntax
    /// ───────────────
    /// Scalar placeholder:      {{dsName.FieldName}}
    ///   – Replaced with the matching value from a "scalar" data source.
    ///
    /// Collection start marker: {{#dsName}}
    ///   – A row whose only content is this token marks the start of a
    ///     repeating block.  The row is deleted after expansion.
    ///
    /// Collection template row: (the row immediately after {{#dsName}})
    ///   – May contain any number of {{dsName.FieldName}} tokens.
    ///   – This row is used as the style/format template; it is copied for
    ///     each data row via EPPlus InsertRow(…, copyStylesFromRow).
    ///
    /// Collection end marker:   {{/dsName}}
    ///   – Marks the end of the block.  Deleted after expansion.
    ///
    /// SUM formulas in rows below the collection block automatically
    /// adjust because EPPlus InsertRow shifts them down.
    /// </summary>
    public class ReportTemplateEngine
    {
        private static readonly Regex TokenRegex =
            new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IExcelEntityRegistry _registry;

        public ReportTemplateEngine(IExcelEntityRegistry registry)
        {
            _registry = registry;
        }

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Generate a filled Excel file from a template and a definition.
        /// </summary>
        /// <param name="templateBytes">Raw bytes of the .xlsx template file.</param>
        /// <param name="definitionJson">JSON string conforming to <see cref="ReportDefinition"/>.</param>
        /// <param name="context">Entity/year scope for registry queries.</param>
        /// <param name="runtimeParams">
        ///   Values supplied by the caller (year id, council id, etc.).
        ///   Keys match <see cref="ParameterDefinition.Name"/> and
        ///   <see cref="ExcelQueryConfig.FilterCondition.ParamName"/>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<byte[]> GenerateAsync(
            byte[] templateBytes,
            string definitionJson,
            ExcelEntityContext context,
            Dictionary<string, string> runtimeParams,
            CancellationToken ct = default)
        {
            var definition = JsonSerializer.Deserialize<ReportDefinition>(definitionJson, JsonOpts)
                ?? throw new InvalidOperationException("Invalid report definition JSON.");

            // ── Resolve all data sources ──────────────────────────────────
            var scalars     = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
            var collections = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var ds in definition.DataSources)
            {
                var config = new ExcelQueryConfig
                {
                    EntityName = ds.Entity,
                    Filters    = ds.Filters,
                    Sort       = ds.Sort,
                    Fields     = new List<ExcelQueryConfig.SelectedField>() // empty = all fields
                };

                var rows = await _registry.QueryEntityAsync(config, context, runtimeParams, ct);

                if (string.Equals(ds.Type, "scalar", StringComparison.OrdinalIgnoreCase))
                    scalars[ds.Name] = rows.FirstOrDefault() ?? new Dictionary<string, object?>();
                else
                    collections[ds.Name] = rows;
            }

            // ── Open template and fill ────────────────────────────────────
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var ms = new MemoryStream(templateBytes);
            using var package = new ExcelPackage(ms);

            foreach (var ws in package.Workbook.Worksheets)
            {
                if (ws.Dimension == null) continue;

                // Expand collection blocks first (row count changes affect scalar row indices)
                foreach (var (dsName, rows) in collections)
                    ExpandCollection(ws, dsName, rows);

                // Fill remaining scalar {{ds.Field}} tokens
                FillScalars(ws, scalars);

                // Also fill any leftover collection scalar tokens
                // (collection cells in single-record mode)
                var collectionScalars = collections
                    .Where(kvp => kvp.Value.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value[0],
                        StringComparer.OrdinalIgnoreCase);
                if (collectionScalars.Count > 0)
                    FillScalars(ws, collectionScalars);
            }

            return package.GetAsByteArray();
        }

        // ─── Collection Row Expansion ─────────────────────────────────────

        private static void ExpandCollection(
            ExcelWorksheet ws,
            string dsName,
            List<Dictionary<string, object?>> rows)
        {
            int startRow = FindMarkerRow(ws, $"{{{{#{dsName}}}}}");
            int endRow   = FindMarkerRow(ws, $"{{{{/{dsName}}}}}");
            if (startRow < 0 || endRow < 0) return;

            int templateDataRow = startRow + 1; // row with {{dsName.Field}} tokens
            int totalCols = ws.Dimension.End.Column;

            // Capture cell text patterns from the template data row
            var patterns  = new string[totalCols + 1];
            for (int c = 1; c <= totalCols; c++)
                patterns[c] = ws.Cells[templateDataRow, c].Text ?? string.Empty;

            if (rows.Count == 0)
            {
                // Remove the three marker rows (end first to preserve row numbers)
                ws.DeleteRow(endRow);
                ws.DeleteRow(templateDataRow);
                ws.DeleteRow(startRow);
                return;
            }

            // Insert (rows.Count - 1) extra rows immediately after the template row,
            // copying its style.  The template row itself becomes row[0].
            if (rows.Count > 1)
                ws.InsertRow(templateDataRow + 1, rows.Count - 1, templateDataRow);

            // Fill each output row
            for (int i = 0; i < rows.Count; i++)
            {
                int targetRow = templateDataRow + i;
                for (int c = 1; c <= totalCols; c++)
                {
                    var pattern = patterns[c];
                    if (string.IsNullOrEmpty(pattern)) continue;

                    // Replace only tokens belonging to this data source
                    var resolvedText = TokenRegex.Replace(pattern, m =>
                    {
                        var token = m.Groups[1].Value.Trim();
                        int dot   = token.IndexOf('.');
                        if (dot < 0) return m.Value;

                        var tDs    = token[..dot];
                        var tField = token[(dot + 1)..];

                        if (!string.Equals(tDs, dsName, StringComparison.OrdinalIgnoreCase))
                            return m.Value; // Different data source — leave for FillScalars

                        return rows[i].TryGetValue(tField, out var v)
                            ? FormatValue(v) : string.Empty;
                    });

                    // For single-token cells, preserve the typed value (number/date)
                    // so Excel treats it correctly in formulas.
                    ws.Cells[targetRow, c].Value =
                        ResolveTypedValue(pattern, resolvedText, dsName, rows[i]);
                }
            }

            // Delete end marker first (its row index has shifted by rows.Count - 1)
            ws.DeleteRow(endRow + rows.Count - 1);
            // Delete start marker (its original row index is unchanged)
            ws.DeleteRow(startRow);
        }

        // ─── Scalar Fill ──────────────────────────────────────────────────

        private static void FillScalars(
            ExcelWorksheet ws,
            Dictionary<string, Dictionary<string, object?>> scalars)
        {
            if (ws.Dimension == null || scalars.Count == 0) return;

            for (int row = ws.Dimension.Start.Row; row <= ws.Dimension.End.Row; row++)
            {
                for (int col = ws.Dimension.Start.Column; col <= ws.Dimension.End.Column; col++)
                {
                    var cell = ws.Cells[row, col];
                    var text = cell.Text;
                    if (string.IsNullOrEmpty(text) || !text.Contains("{{")) continue;

                    var newText = TokenRegex.Replace(text, m =>
                    {
                        var token = m.Groups[1].Value.Trim();
                        int dot   = token.IndexOf('.');
                        if (dot < 0) return m.Value;

                        var dsName = token[..dot];
                        var field  = token[(dot + 1)..];

                        if (!scalars.TryGetValue(dsName, out var dsData)) return m.Value;
                        return dsData.TryGetValue(field, out var v)
                            ? FormatValue(v) : string.Empty;
                    });

                    if (newText != text)
                    {
                        // For single-token cells preserve typed value
                        var m = TokenRegex.Match(text);
                        if (m.Success && m.Value == text)
                        {
                            var token = m.Groups[1].Value.Trim();
                            int dot   = token.IndexOf('.');
                            if (dot >= 0)
                            {
                                var dsName = token[..dot];
                                var field  = token[(dot + 1)..];
                                if (scalars.TryGetValue(dsName, out var dsData) &&
                                    dsData.TryGetValue(field, out var v))
                                {
                                    cell.Value = v;
                                    continue;
                                }
                            }
                        }

                        cell.Value = newText;
                    }
                }
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        /// <summary>Find the first row whose any cell contains the exact marker token.</summary>
        private static int FindMarkerRow(ExcelWorksheet ws, string marker)
        {
            if (ws.Dimension == null) return -1;
            for (int r = ws.Dimension.Start.Row; r <= ws.Dimension.End.Row; r++)
                for (int c = ws.Dimension.Start.Column; c <= ws.Dimension.End.Column; c++)
                    if (ws.Cells[r, c].Text?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true)
                        return r;
            return -1;
        }

        private static string FormatValue(object? v) => v switch
        {
            null        => string.Empty,
            DateOnly d  => d.ToString("dd/MM/yyyy"),
            DateTime dt => dt.ToString("dd/MM/yyyy"),
            decimal dec => dec.ToString("N2"),
            bool b      => b ? "כן" : "לא",
            _           => v.ToString() ?? string.Empty
        };

        /// <summary>
        /// For cells whose entire content is a single placeholder token, return
        /// the raw typed value (int, decimal, DateTime …) so EPPlus stores it
        /// as a number/date rather than a string.  Falls back to the resolved
        /// string for mixed-content cells.
        /// </summary>
        private static object? ResolveTypedValue(
            string pattern,
            string resolvedText,
            string dsName,
            Dictionary<string, object?> row)
        {
            var m = TokenRegex.Match(pattern);
            // Is the entire cell a single placeholder?
            if (!m.Success || m.Value != pattern) return resolvedText;

            var token = m.Groups[1].Value.Trim();
            int dot   = token.IndexOf('.');
            if (dot < 0) return resolvedText;

            var field = token[(dot + 1)..];
            return row.TryGetValue(field, out var v) ? v : resolvedText;
        }
    }
}
