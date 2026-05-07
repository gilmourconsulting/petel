using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace Petel.Core.Excel
{
    /// <summary>
    /// Generates Excel workbooks from in-memory tabular data using EPPlus.
    /// All output uses RTL worksheet layout for Hebrew content.
    /// </summary>
    public class ExcelGenerationService
    {
        static ExcelGenerationService()
        {
            // EPPlus 7 requires explicit license context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Creates a single-sheet Excel workbook from a list of row dictionaries.
        /// </summary>
        /// <param name="rows">Data rows. Each dictionary key is a column key.</param>
        /// <param name="columns">
        /// Ordered column definitions. Key = dictionary key; Label = Hebrew header text.
        /// </param>
        /// <param name="sheetName">Name for the worksheet tab (Hebrew).</param>
        /// <returns>Raw .xlsx byte array ready to be streamed to the client.</returns>
        public byte[] GenerateFromRows(
            IReadOnlyList<Dictionary<string, object?>> rows,
            IReadOnlyList<(string Key, string Label)> columns,
            string sheetName = "נתונים")
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add(sheetName);

            // Hebrew RTL
            ws.View.RightToLeft = true;

            // ── Headers ────────────────────────────────────────────────────
            for (int c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cells[1, c + 1];
                cell.Value = columns[c].Label;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xD9, 0xE1, 0xF2)); // light blue
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }

            // ── Data rows ──────────────────────────────────────────────────
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int c = 0; c < columns.Count; c++)
                {
                    var key = columns[c].Key;
                    var rawValue = row.TryGetValue(key, out var v) ? v : null;
                    var cell = ws.Cells[r + 2, c + 1];
                    SetCellValue(cell, rawValue);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }

                // Alternate row shading
                if (r % 2 == 1)
                {
                    using var rowRange = ws.Cells[r + 2, 1, r + 2, columns.Count];
                    rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    rowRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xF2, 0xF2, 0xF2));
                }
            }

            // ── Formatting ─────────────────────────────────────────────────
            ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 60);

            // Freeze the header row
            ws.View.FreezePanes(2, 1);

            // Auto filter on the header row
            if (columns.Count > 0)
                ws.Cells[1, 1, 1, columns.Count].AutoFilter = true;

            return package.GetAsByteArray();
        }

        private static void SetCellValue(ExcelRange cell, object? value)
        {
            if (value is null)
            {
                cell.Value = null;
                return;
            }

            switch (value)
            {
                case DateTime dt:
                    cell.Value = dt;
                    cell.Style.Numberformat.Format = "dd/mm/yyyy";
                    break;
                case DateTimeOffset dto:
                    cell.Value = dto.DateTime;
                    cell.Style.Numberformat.Format = "dd/mm/yyyy";
                    break;
                case decimal dec:
                    cell.Value = dec;
                    break;
                case double dbl:
                    cell.Value = dbl;
                    break;
                case float flt:
                    cell.Value = flt;
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case long l:
                    cell.Value = l;
                    break;
                case bool b:
                    cell.Value = b ? "כן" : "לא";
                    break;
                default:
                    cell.Value = value.ToString();
                    break;
            }
        }
    }
}
