using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var outputPath = Path.GetFullPath(
    args.Length > 0 ? args[0] : "../entity-students-template.xlsx");

using var package = new ExcelPackage();
var ws = package.Workbook.Worksheets.Add("תלמידים");
ws.View.RightToLeft = true;

// ── Palette ────────────────────────────────────────────────────────────────
var headerBlue = Color.FromArgb(0x2E, 0x75, 0xB6);  // table header
var metaBg     = Color.FromArgb(0xDD, 0xEB, 0xF7);  // meta rows background
var rowStripe  = Color.FromArgb(0xF2, 0xF7, 0xFD);  // alternate stripe
var labelColor = Color.FromArgb(0x1F, 0x49, 0x7D);  // label text

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

// ── Row 1: Report title (13 columns: A–M) ─────────────────────────────────
ws.Cells["A1:M1"].Merge = true;
ws.Cells["A1"].Value = "דוח תלמידים לפי ישות";
ws.Cells["A1"].Style.Font.Bold = true;
ws.Cells["A1"].Style.Font.Size = 15;
ws.Cells["A1"].Style.Font.Color.SetColor(Color.White);
ws.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
ws.Cells["A1"].Style.Fill.BackgroundColor.SetColor(headerBlue);
ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
ws.Cells["A1"].Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
ws.Row(1).Height = 28;

// ── Row 2: Spacer ──────────────────────────────────────────────────────────
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

// ── Row 5: Spacer ──────────────────────────────────────────────────────────
ws.Row(5).Height = 6;

// ── Row 6: Column headers ──────────────────────────────────────────────────
// Columns:  A              B       C         D           E       F         G             H           I             J                  K                              L          M
string[] headers = {
    "שם בית ספר",  // A
    "ת.ז.",        // B
    "שם פרטי",     // C
    "שם משפחה",    // D
    "כיתה",        // E
    "קטגוריה",     // F
    "תאריך קליטה", // G
    "תאריך סיום",  // H
    "חודשים",      // I
    "עלות מרכיב בסיסי",         // J  — full annual (pre-proration) price
    "עלות ממשית מרכיב בסיסי",   // K  — prorated actual cost
    "סטטוס",                     // L
    "רשות שולחת"                 // M
};

for (int i = 0; i < headers.Length; i++)
{
    var cell = ws.Cells[6, i + 1];
    cell.Value = headers[i];
    cell.Style.Font.Bold = true;
    cell.Style.Font.Color.SetColor(Color.White);
    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
    cell.Style.Fill.BackgroundColor.SetColor(headerBlue);
    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    cell.Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.White);
    cell.Style.WrapText = true;
}
ws.Row(6).Height = 30;

// ── Row 7: Collection start marker (deleted by engine at runtime) ──────────
ws.Cells["A7"].Value = "{{#students}}";
ws.Cells["A7"].Style.Font.Color.SetColor(Color.Gray);
ws.Cells["A7"].Style.Font.Italic = true;

// ── Row 8: Template data row (engine copies styles for every student) ──────
// NOTE: Replace "בסיסית" with the exact value from the pricing element 'name' column.
string[] tokens = {
    "{{students.SchoolName}}",       // A
    "{{students.IdNumber}}",         // B
    "{{students.FirstName}}",        // C
    "{{students.LastName}}",         // D
    "{{students.ClassName}}",        // E
    "{{students.DisabilityCategory}}",// F
    "{{students.StartDate}}",        // G
    "{{students.EndDate}}",          // H
    "{{students.CalculatedMonths}}", // I
    "{{students.בסיסית_מלא}}",       // J — full annual price of basic element
    "{{students.בסיסית}}",           // K — prorated (actual) price of basic element
    "{{students.Status}}",           // L — student status name
    "{{students.CouncilName}}"       // M — sending council name
};

for (int i = 0; i < tokens.Length; i++)
{
    var cell = ws.Cells[8, i + 1];
    cell.Value = tokens[i];
    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
    cell.Style.Fill.BackgroundColor.SetColor(rowStripe);
    cell.Style.Border.Bottom.Style = ExcelBorderStyle.Hair;
    cell.Style.Border.Bottom.Color.SetColor(Color.LightGray);
    cell.Style.Border.Top.Style   = ExcelBorderStyle.Hair;
    cell.Style.Border.Top.Color.SetColor(Color.LightGray);
}

// Column-specific formatting
ws.Cells["B8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;   // ID
ws.Cells["E8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;   // Class
ws.Cells["F8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;   // Category
ws.Cells["G8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;   // Start date
ws.Cells["H8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;   // End date
ws.Cells["I8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;   // Months
ws.Cells["J8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;     // Total cost
ws.Cells["J8"].Style.Numberformat.Format = "#,##0.00";
ws.Cells["K8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;     // Actual cost
ws.Cells["K8"].Style.Numberformat.Format = "#,##0.00";
ws.Cells["L8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;   // Status
ws.Cells["M8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;    // Council name

// ── Row 9: Collection end marker (deleted by engine at runtime) ────────────
ws.Cells["A9"].Value = "{{/students}}";
ws.Cells["A9"].Style.Font.Color.SetColor(Color.Gray);
ws.Cells["A9"].Style.Font.Italic = true;

// ── Row 10: Totals row ─────────────────────────────────────────────────────
ws.Cells["I10"].Value = "סה\"כ:";
ws.Cells["I10"].Style.Font.Bold = true;
ws.Cells["I10"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

ws.Cells["J10"].Formula = "SUM(J8:J8)";   // engine inserts rows; formula shifts automatically
ws.Cells["J10"].Style.Font.Bold = true;
ws.Cells["J10"].Style.Numberformat.Format = "#,##0.00";
ws.Cells["J10"].Style.Border.Top.Style    = ExcelBorderStyle.Medium;
ws.Cells["J10"].Style.Border.Top.Color.SetColor(headerBlue);

ws.Cells["K10"].Formula = "SUM(K8:K8)";
ws.Cells["K10"].Style.Font.Bold = true;
ws.Cells["K10"].Style.Numberformat.Format = "#,##0.00";
ws.Cells["K10"].Style.Border.Top.Style    = ExcelBorderStyle.Medium;
ws.Cells["K10"].Style.Border.Top.Color.SetColor(headerBlue);

// ── Column widths ──────────────────────────────────────────────────────────
ws.Column(1).Width  = 26;  // A — School name
ws.Column(2).Width  = 12;  // B — ID
ws.Column(3).Width  = 14;  // C — First name
ws.Column(4).Width  = 16;  // D — Last name
ws.Column(5).Width  = 9;   // E — Class
ws.Column(6).Width  = 11;  // F — Category
ws.Column(7).Width  = 14;  // G — Start date
ws.Column(8).Width  = 14;  // H — End date
ws.Column(9).Width  = 10;  // I — Months
ws.Column(10).Width = 20;  // J — Total cost
ws.Column(11).Width = 22;  // K — Actual cost
ws.Column(12).Width = 12;  // L — Status
ws.Column(13).Width = 22;  // M — Council name

// ── Freeze panes ───────────────────────────────────────────────────────────
ws.View.FreezePanes(7, 1);

// ── Print settings ─────────────────────────────────────────────────────────
ws.PrinterSettings.Orientation = eOrientation.Landscape;
ws.PrinterSettings.FitToPage   = true;
ws.PrinterSettings.FitToWidth  = 1;
ws.PrinterSettings.FitToHeight = 0;
ws.PrinterSettings.RepeatRows  = new ExcelAddress("$1:$6");

package.SaveAs(new FileInfo(outputPath));
Console.WriteLine($"✅  Template saved → {outputPath}");
