using PetelAssistants.Api.Services;
var pdf = Directory.GetFiles(@"c:\dev\PetelFullApp\PetelAssistants\ImportFiles", "*.pdf")[0];
await using var fs = File.OpenRead(pdf);
var result = new PersonalApprovalsPdfParser().ConvertToExcel(fs, Path.GetFileName(pdf));
Console.WriteLine($"rows={result.RowCount} errors={result.ErrorCount}");
File.WriteAllLines(@"c:\dev\PetelFullApp\.tmp-pdf-test\errors2.txt", result.Errors);
using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(Convert.FromBase64String(result.ContentBase64!)));
var ws = wb.Worksheet(1);
foreach (var r in new[] { 262, 266, 267 }) // excel rows = page+1
{
    var vals = string.Join(" | ", Enumerable.Range(1, 14).Select(c => ws.Cell(r, c).GetString()));
    File.AppendAllText(@"c:\dev\PetelFullApp\.tmp-pdf-test\fix-check.txt", $"excelRow{r}: {vals}\n");
}
