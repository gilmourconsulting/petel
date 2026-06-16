using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace PetelATH.BlazorServer.Services
{
    /// <summary>
    /// Generic client-side Excel export service.
    /// Accepts column headers and rows of string values; returns raw .xlsx bytes
    /// suitable for passing to <c>window.downloadFileFromBase64</c> in JS.
    /// </summary>
    public class ExcelExportService
    {
        /// <summary>
        /// Generate an .xlsx file from the supplied headers and row data.
        /// </summary>
        /// <param name="headers">Column header labels (Hebrew or otherwise).</param>
        /// <param name="rows">Each row is an array of string values matching <paramref name="headers"/> by index.</param>
        /// <param name="sheetName">Worksheet name (defaults to "נתונים").</param>
        public byte[] Export(string[] headers, IEnumerable<string[]> rows, string sheetName = "נתונים")
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add(sheetName);
            ws.View.RightToLeft = true;

            // Header row — bold white text on blue background
            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = ws.Cells[1, col];
                cell.Value = headers[col - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0x21, 0x96, 0xF3));
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            }

            // Data rows
            int row = 2;
            foreach (var rowData in rows)
            {
                for (int col = 1; col <= rowData.Length; col++)
                {
                    ws.Cells[row, col].Value = rowData[col - 1];
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                row++;
            }

            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }
    }
}
