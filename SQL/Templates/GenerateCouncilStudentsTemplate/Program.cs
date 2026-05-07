using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var outputPath = Path.GetFullPath(
    args.Length > 0 ? args[0] : "../council-students-template.xlsx");

using var package = new ExcelPackage();
var ws = package.Workbook.Worksheets.Add("תלמידים");
ws.View.RightToLeft = true;

// ── Palette ────────────────────────────────────────────────────────────────
var headerBlue  = Color.FromArgb(0x2E, 0x75, 0xB6);  // table header
var metaBg      = Color.FromArgb(0xDD, 0xEB, 0xF7);  // meta rows background
var rowStripe   = Color.FromArgb(0xF2, 0xF7, 0xFD);  // alternate stripe (cosmetic – engine fills)
var labelColor  = Color.FromArgb(0x1F, 0x49, 0x7D);  // label text

// ── Helper ─────────────────────────────────────────────────────────────────
void SetLabel(ExcelRange cell, string text)
{
    cell.Value = text;
    cell.Style.Font.Bold = true;
    cell.Style.Font.Color.SetColor(labelColor);
}

void SetMeta(ExcelRange cell, string token)
{
    cell.Value = token;
    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
    cell.Style.Fill.BackgroundColor.SetColor(metaBg);
    cell.Style.Border.BorderAround(ExcelBorderStyle.Hair, Color.LightGray);
}

// ── Row 1: Report title ────────────────────────────────────────────────────
ws.Cells["A1:I1"].Merge = true;
ws.Cells["A1"].Value = "דוח תלמידים לפי רשות שולחת";
ws.Cells["A1"].Style.Font.Bold = true;
ws.Cells["A1"].Style.Font.Size = 15;
ws.Cells["A1"].Style.Font.Color.SetColor(Color.White);
ws.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
ws.Cells["A1"].Style.Fill.BackgroundColor.SetColor(headerBlue);
ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
ws.Cells["A1"].Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
ws.Row(1).Height = 28;

// ── Row 2: Empty spacer ────────────────────────────────────────────────────
ws.Row(2).Height = 6;

// ── Row 3: Owner entity ────────────────────────────────────────────────────
SetLabel(ws.Cells["A3"], "גוף מנהל:");
SetMeta (ws.Cells["B3"], "{{header.Name}}");
ws.Cells["B3:D3"].Merge = true;

// ── Row 4: Contact person ──────────────────────────────────────────────────
SetLabel(ws.Cells["A4"], "איש קשר:");
SetMeta (ws.Cells["B4"], "{{header.ContactPersonName}}");

SetLabel(ws.Cells["D4"], "טלפון:");
SetMeta (ws.Cells["E4"], "{{header.ContactPersonPhone}}");

SetLabel(ws.Cells["G4"], "אימייל:");
SetMeta (ws.Cells["H4"], "{{header.ContactPersonEmail}}");
ws.Cells["H4:I4"].Merge = true;

// ── Row 5: Sending council ─────────────────────────────────────────────────
SetLabel(ws.Cells["A5"], "רשות שולחת:");
SetMeta (ws.Cells["B5"], "{{council.Name}}");
ws.Cells["B5:D5"].Merge = true;

// ── Row 6: Spacer ──────────────────────────────────────────────────────────
ws.Row(6).Height = 6;

// ── Row 7: Column headers ──────────────────────────────────────────────────
string[] headers = {
    "שם בית ספר",   // A
    "סמל",          // B
    "ת.ז.",         // C
    "שם תלמיד",     // D
    "קטגוריה",      // E
    "כיתה",         // F
    "תאריך קליטה",  // G
    "תאריך סיום",   // H
    "עלות"          // I
};

for (int i = 0; i < headers.Length; i++)
{
    var cell = ws.Cells[7, i + 1];
    cell.Value = headers[i];
    cell.Style.Font.Bold = true;
    cell.Style.Font.Color.SetColor(Color.White);
    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
    cell.Style.Fill.BackgroundColor.SetColor(headerBlue);
    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    cell.Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.White);
    cell.Style.WrapText = false;
}
ws.Row(7).Height = 20;

// ── Row 8: Collection start marker ────────────────────────────────────────
// This row is deleted by the engine at runtime.
ws.Cells["A8"].Value = "{{#students}}";
ws.Cells["A8"].Style.Font.Color.SetColor(Color.Gray);
ws.Cells["A8"].Style.Font.Italic = true;

// ── Row 9: Template data row ───────────────────────────────────────────────
// The engine copies this row's styles for every student.
string[] tokens = {
    "{{students.SchoolName}}",
    "{{students.SchoolSymbol}}",
    "{{students.IdNumber}}",
    "{{students.FullName}}",
    "{{students.DisabilityCategory}}",
    "{{students.ClassName}}",
    "{{students.StartDate}}",
    "{{students.EndDate}}",
    "{{students.Cost}}"
};

for (int i = 0; i < tokens.Length; i++)
{
    var cell = ws.Cells[9, i + 1];
    cell.Value = tokens[i];
    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
    cell.Style.Fill.BackgroundColor.SetColor(rowStripe);
    cell.Style.Border.Bottom.Style = ExcelBorderStyle.Hair;
    cell.Style.Border.Bottom.Color.SetColor(Color.LightGray);
    cell.Style.Border.Top.Style   = ExcelBorderStyle.Hair;
    cell.Style.Border.Top.Color.SetColor(Color.LightGray);
}
// Cost column: right-align & number format
ws.Cells["I9"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
ws.Cells["I9"].Style.Numberformat.Format = "#,##0.00";
// Dates: center
ws.Cells["G9"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
ws.Cells["H9"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
// ID: center
ws.Cells["C9"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
// Symbol: center
ws.Cells["B9"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
// Category: center
ws.Cells["E9"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
// Class: center
ws.Cells["F9"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

// ── Row 10: Collection end marker ─────────────────────────────────────────
// Deleted by the engine at runtime.
ws.Cells["A10"].Value = "{{/students}}";
ws.Cells["A10"].Style.Font.Color.SetColor(Color.Gray);
ws.Cells["A10"].Style.Font.Italic = true;

// ── Row 11: Totals row (will shift down automatically when rows are inserted)
ws.Cells["H11"].Value = "סה\"כ:";
ws.Cells["H11"].Style.Font.Bold = true;
ws.Cells["H11"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
// SUM formula - EPPlus InsertRow shifts this down automatically
ws.Cells["I11"].Formula = "SUM(I9:I9)";   // engine expands rows; formula range shifts automatically
ws.Cells["I11"].Style.Font.Bold = true;
ws.Cells["I11"].Style.Numberformat.Format = "#,##0.00";
ws.Cells["I11"].Style.Border.Top.Style    = ExcelBorderStyle.Medium;
ws.Cells["I11"].Style.Border.Top.Color.SetColor(headerBlue);

// ── Column widths ──────────────────────────────────────────────────────────
ws.Column(1).Width = 24;  // School name
ws.Column(2).Width = 9;   // Symbol
ws.Column(3).Width = 12;  // ID
ws.Column(4).Width = 22;  // Student name
ws.Column(5).Width = 11;  // Category
ws.Column(6).Width = 9;   // Class
ws.Column(7).Width = 14;  // Start date
ws.Column(8).Width = 14;  // End date
ws.Column(9).Width = 13;  // Cost

// ── Freeze panes: keep header rows visible while scrolling ─────────────────
ws.View.FreezePanes(8, 1);

// ── Print settings ─────────────────────────────────────────────────────────
ws.PrinterSettings.Orientation  = eOrientation.Landscape;
ws.PrinterSettings.FitToPage    = true;
ws.PrinterSettings.FitToWidth   = 1;
ws.PrinterSettings.FitToHeight  = 0;
ws.PrinterSettings.RepeatRows   = new ExcelAddress("$1:$7");  // repeat header rows on each printed page

package.SaveAs(new FileInfo(outputPath));
Console.WriteLine($"✅  Template saved → {outputPath}");
