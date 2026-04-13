using CsvHelper;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PetelATH.Api.Services
{
    public static class StudentsFileValidator
    {
        // ✅ Optional fields: gender, street, house_number, post_code
        // ✅ Default for gender in database is 99 (unknown)
        public static readonly string[] MandatoryFields = new[]
        {
            "id_number", "first_name", "last_name", "class", "start_date", "end_date",
            "disability_category", "city", "sending_council"
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
                // ✅ Special handling for class field - can be either "class" OR "class_level" + "class_number"
                var hasClass = mapping.ContainsKey("class");
                var hasClassLevel = mapping.ContainsKey("class_level");
                var hasClassNumber = mapping.ContainsKey("class_number");
                var hasClassParts = hasClassLevel && hasClassNumber;

                if (!hasClass && !hasClassParts)
                {
                    return (false, "חסר מיפוי לשדה כיתה. יש למפות 'כיתה' או 'שכבה'+'מספר כיתה'");
                }

                // Check all other mandatory fields (excluding 'class' since we handled it above)
                var otherMandatoryFields = MandatoryFields.Where(f => f != "class").ToArray();

                foreach (var field in otherMandatoryFields)
                {
                    if (!mapping.ContainsKey(field))
                        return (false, $"חסר מיפוי לשדה חובה: {field}");

                    var mappedHeader = mapping[field];
                    if (!headers.Contains(mappedHeader))
                        return (false, $"עמודה '{mappedHeader}' לא נמצאה בקובץ (ממופה מ-{field})");
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