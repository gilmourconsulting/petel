using CsvHelper;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PetelApp.Api.Services
{
    public static class StudentsFileValidator
    {
        public static readonly string[] MandatoryFields = new[]
        {
            "id_number", "first_name", "last_name", "gender", "class", "start_date", "end_date",
            "disability_category", "street", "house_number", "city", "post_code", "sending_counsil"
        };

        /// <summary>
        /// Validates the uploaded students file for required fields and column mapping.
        /// </summary>
        /// <param name="file">Uploaded file (IFormFile)</param>
        /// <param name="mapping">Optional mapping dictionary: { "id_number": "ת.ז", ... }</param>
        /// <returns>Tuple: (isValid, errorMessage)</returns>
        public static async Task<(bool isValid, string errorMessage)> ValidateStudentsFileAsync(
            IFormFile file, 
            Dictionary<string, string>? mapping = null)
        {
            if (file == null || file.Length == 0)
                return (false, "לא הועלה קובץ");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            List<string> headers;
            using (var stream = file.OpenReadStream())
            {
                if (ext == ".csv")
                {
                    using var reader = new StreamReader(stream);
                    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                    csv.Read();
                    csv.ReadHeader();
                    
                    if (csv.HeaderRecord == null || csv.HeaderRecord.Length == 0)
                        return (false, "קובץ CSV אינו מכיל שורת כותרות");
                    
                    headers = csv.HeaderRecord.ToList();
                }
                else if (ext == ".xls" || ext == ".xlsx")
                {
                    using var workbook = new XLWorkbook(stream);
                    var worksheet = workbook.Worksheets.FirstOrDefault();
                    
                    if (worksheet == null)
                        return (false, "קובץ Excel ריק");

                    var firstRow = worksheet.FirstRowUsed();
                    if (firstRow == null)
                        return (false, "קובץ Excel אינו מכיל שורת כותרות");

                    headers = firstRow.CellsUsed()
                        .Select(cell => cell.Value.ToString()?.Trim() ?? "")
                        .ToList();
                }
                else
                {
                    return (false, "סוג קובץ לא נתמך. רק CSV, XLS, XLSX מותרים");
                }
            }

            // If mapping provided, validate mapped headers exist
            if (mapping != null && mapping.Count > 0)
            {
                foreach (var field in MandatoryFields)
                {
                    if (!mapping.ContainsKey(field))
                        return (false, $"חסר מיפוי לשדה חובה: {field}");

                    var mappedHeader = mapping[field];
                    if (!headers.Contains(mappedHeader))
                        return (false, $"כותרת ממופה '{mappedHeader}' לשדה '{field}' לא נמצאה בקובץ");
                }
            }
            else
            {
                // No mapping: check headers match mandatory fields exactly
                if (headers.Count < MandatoryFields.Length)
                    return (false, $"הקובץ חייב להכיל לפחות {MandatoryFields.Length} עמודות");

                foreach (var mandatoryField in MandatoryFields)
                {
                    if (!headers.Any(h => string.Equals(h, mandatoryField, System.StringComparison.OrdinalIgnoreCase)))
                        return (false, $"חסרה עמודה חובה: {mandatoryField}");
                }
            }

            return (true, "");
        }
    }
}