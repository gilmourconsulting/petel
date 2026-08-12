using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using PetelAssistants.Api.DTOs;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PetelAssistants.Api.Services
{
    public class PersonalApprovalsPdfParser
    {
        private const long MaxFileBytes = 20 * 1024 * 1024;
        private static readonly Regex DateRegex = new(@"\d{2}/\d{2}/\d{4}", RegexOptions.Compiled);

        private sealed class TextRun
        {
            public double Y { get; init; }
            public double X { get; init; }
            public string Text { get; init; } = string.Empty;
        }

        public PersonalApprovalsPdfConvertResult ConvertToExcel(Stream pdfStream, string? originalFileName)
        {
            if (pdfStream.CanSeek)
                pdfStream.Position = 0;

            using var ms = new MemoryStream();
            pdfStream.CopyTo(ms);
            if (ms.Length == 0)
                return Fail("הקובץ ריק");
            if (ms.Length > MaxFileBytes)
                return Fail("גודל הקובץ חורג מהמותר (20MB)");

            var rows = new List<PersonalApprovalRow>();
            var errors = new List<string>();

            try
            {
                ms.Position = 0;
                using var document = PdfDocument.Open(ms);
                for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
                {
                    var page = document.GetPage(pageNumber);
                    var row = ParsePage(page, pageNumber);
                    rows.Add(row);
                    if (!string.IsNullOrWhiteSpace(row.Error))
                        errors.Add($"עמוד {pageNumber}: {row.Error}");
                }
            }
            catch (Exception ex)
            {
                return Fail($"שגיאה בקריאת PDF: {ex.Message}");
            }

            if (rows.Count == 0)
                return Fail("לא נמצאו עמודים בקובץ");

            var excelBytes = BuildExcel(rows);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            var fileName = $"personal-approvals-{stamp}.xlsx";

            return new PersonalApprovalsPdfConvertResult
            {
                Success = true,
                FileName = fileName,
                ContentBase64 = Convert.ToBase64String(excelBytes),
                RowCount = rows.Count,
                ErrorCount = errors.Count,
                Errors = errors.Take(50).ToList(),
                Message = string.IsNullOrWhiteSpace(originalFileName)
                    ? null
                    : $"עובד: {originalFileName}"
            };
        }

        private static PersonalApprovalsPdfConvertResult Fail(string message) => new()
        {
            Success = false,
            Message = message,
            Errors = new List<string> { message }
        };

        private static PersonalApprovalRow ParsePage(Page page, int pageNumber)
        {
            var runs = ExtractRuns(page);
            var row = new PersonalApprovalRow { PageNumber = pageNumber };
            var issues = new List<string>();

            var allDates = runs
                .SelectMany(r => DateRegex.Matches(r.Text).Cast<Match>().Select(m => (r.Y, Date: m.Value)))
                .OrderByDescending(x => x.Y)
                .Select(x => x.Date)
                .ToList();
            if (allDates.Count > 0)
                row.ApprovalDate = allDates[0];

            ParseLearnerLine(runs, row, issues);
            ParseStudentName(runs, row, issues);
            ParseStudentId(runs, row, issues);
            ParseInstitution(runs, row, issues);
            ParseHoursAndDates(runs, row, issues);
            ParseParticipation(runs, row, issues);
            ParseCouncil(runs, row);

            if (string.IsNullOrWhiteSpace(row.StudentId))
                issues.Add("חסר ת.ז.");
            if (string.IsNullOrWhiteSpace(row.Hours))
                issues.Add("חסרות שעות");
            if (string.IsNullOrWhiteSpace(row.Framework))
                issues.Add("חסרה מסגרת (גן/כיתה)");

            if (issues.Count > 0)
                row.Error = string.Join("; ", issues.Distinct());

            return row;
        }

        private static void ParseLearnerLine(List<TextRun> runs, PersonalApprovalRow row, List<string> issues)
        {
            var learner = runs.FirstOrDefault(r =>
                r.Text.Contains("תלמיד הלומד", StringComparison.Ordinal) ||
                r.Text.Contains("תלמידה הלומדת", StringComparison.Ordinal));

            if (learner == null)
            {
                issues.Add("לא נמצאה שורת תלמיד הלומד");
                return;
            }

            if (learner.Text.Contains("בגן", StringComparison.Ordinal))
                row.Framework = "גן";
            else if (learner.Text.Contains("בכיתה", StringComparison.Ordinal))
                row.Framework = "כיתה";
            else if (learner.Text.Contains("חינוך מיוחד", StringComparison.Ordinal))
                row.Framework = "חינוך מיוחד";

            var codeMatch = Regex.Match(learner.Text, @"קוד תומכת חינוך\s+(\d{1,3})");
            if (!codeMatch.Success)
                codeMatch = Regex.Match(learner.Text, @"\s(\d{1,3})\s+לשנה");
            if (codeMatch.Success)
                row.SupportCode = codeMatch.Groups[1].Value;
            else
                issues.Add("חסר קוד תומכת חינוך");
        }

        private static void ParseStudentName(List<TextRun> runs, PersonalApprovalRow row, List<string> issues)
        {
            var nameLine = runs.FirstOrDefault(r =>
                r.Text.StartsWith("שם ", StringComparison.Ordinal));

            if (nameLine == null)
            {
                issues.Add("לא נמצא שם תלמיד");
                return;
            }

            var fullName = nameLine.Text["שם".Length..].Trim();
            var (first, last) = SplitStudentName(fullName);
            row.StudentFirstName = first;
            row.StudentLastName = last;
            if (string.IsNullOrWhiteSpace(last))
                issues.Add("שם משפחה חסר");
        }

        internal static (string First, string Last) SplitStudentName(string fullName)
        {
            var tokens = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (tokens.Count == 0)
                return (string.Empty, string.Empty);
            if (tokens.Count == 1)
                return (tokens[0], string.Empty);
            if (tokens.Count == 2)
                return (tokens[0], tokens[1]);
            if (tokens.Count == 3 && tokens[1] == "בן")
                return (tokens[0], $"בן {tokens[2]}");

            return (string.Join(' ', tokens.Take(tokens.Count - 1)), tokens[^1]);
        }

        private static void ParseStudentId(List<TextRun> runs, PersonalApprovalRow row, List<string> issues)
        {
            string? id = null;
            foreach (var run in runs)
            {
                if (run.Text.Contains("ת ז", StringComparison.Ordinal) ||
                    run.Text.Contains("ת.ז", StringComparison.Ordinal))
                {
                    var m = Regex.Match(run.Text, @"\b(\d{8,9})\b");
                    if (m.Success)
                    {
                        id = m.Groups[1].Value;
                        break;
                    }
                }
            }

            id ??= runs
                .Select(r => Regex.Match(r.Text, @"\b(\d{9})\b"))
                .FirstOrDefault(m => m.Success)?.Groups[1].Value;

            if (id == null)
                issues.Add("חסר ת.ז.");
            else
                row.StudentId = id.PadLeft(9, '0');
        }

        private static void ParseInstitution(List<TextRun> runs, PersonalApprovalRow row, List<string> issues)
        {
            var line = runs.FirstOrDefault(r => r.Text.StartsWith("מוסד ", StringComparison.Ordinal));
            if (line == null)
            {
                issues.Add("לא נמצא מוסד");
                return;
            }

            var rest = line.Text["מוסד".Length..].Trim();

            // Symbol is the 6-digit number at end of line. A glued trailing single digit
            // (e.g. 7672773 → 767277) is ignored.
            var endNum = Regex.Match(rest, @"(\d{6,7})\s*$");
            if (endNum.Success)
            {
                var digits = endNum.Groups[1].Value;
                row.InstitutionSymbol = digits.Length >= 7 ? digits[..6] : digits;
                row.InstitutionName = rest[..^digits.Length].Trim();
            }
            else
            {
                var symMatch = Regex.Match(rest, @"\b(\d{6})\b");
                if (symMatch.Success)
                {
                    row.InstitutionSymbol = symMatch.Groups[1].Value;
                    row.InstitutionName = rest.Replace(symMatch.Groups[1].Value, "").Trim();
                }
                else
                {
                    row.InstitutionName = rest;
                }
            }

            if (string.IsNullOrWhiteSpace(row.InstitutionSymbol))
                issues.Add("חסר סמל מוסד");
            if (string.IsNullOrWhiteSpace(row.InstitutionName))
                issues.Add("חסר שם מוסד");
        }

        private static void ParseHoursAndDates(List<TextRun> runs, PersonalApprovalRow row, List<string> issues)
        {
            var line = runs.FirstOrDefault(r =>
                r.Text.Contains("בהיקף", StringComparison.Ordinal) &&
                r.Text.Contains("מתאריך", StringComparison.Ordinal));

            line ??= runs.FirstOrDefault(r => r.Text.Contains("בהיקף", StringComparison.Ordinal));

            if (line == null)
            {
                issues.Add("חסרים תאריכי תוקף");
                return;
            }

            var hoursMatch = Regex.Match(line.Text, @"בהיקף של\s+(\d{1,3})\b");
            if (hoursMatch.Success)
                row.Hours = hoursMatch.Groups[1].Value;

            var dates = DateRegex.Matches(line.Text).Select(m => m.Value).ToList();
            if (dates.Count >= 2)
            {
                row.StartDate = dates[0];
                row.EndDate = dates[1];
            }
            else if (dates.Count == 1)
            {
                row.StartDate = dates[0];
            }

            if (string.IsNullOrWhiteSpace(row.StartDate) || string.IsNullOrWhiteSpace(row.EndDate))
                issues.Add("חסרים תאריכי תוקף");
        }

        private static void ParseParticipation(List<TextRun> runs, PersonalApprovalRow row, List<string> issues)
        {
            var pct = runs
                .Select(r => Regex.Match(r.Text, @"\b(\d{1,3})%"))
                .FirstOrDefault(m => m.Success);

            if (pct is not { Success: true })
            {
                issues.Add("חסר אחוז השתתפות");
                return;
            }

            if (decimal.TryParse(pct.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                row.ParticipationPct = value;
            else
                issues.Add("אחוז השתתפות לא תקין");
        }

        private static void ParseCouncil(List<TextRun> runs, PersonalApprovalRow row)
        {
            var line = runs.FirstOrDefault(r =>
                r.Text.Contains("מקומית", StringComparison.Ordinal) ||
                (r.Text.StartsWith("אל ", StringComparison.Ordinal) && ContainsHebrew(r.Text)));

            if (line != null)
            {
                var name = line.Text.Replace("אל ", "", StringComparison.Ordinal).Trim();
                name = RepairCouncilName(name);

                if (!name.Contains("מועצה", StringComparison.Ordinal) && name.Contains("מקומית", StringComparison.Ordinal))
                {
                    var locality = name
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(p => p is not ("מקומית" or "מ" or "מועצה" or "אל"));
                    name = locality != null ? $"מועצה מקומית {locality}" : $"מועצה {name}";
                }

                row.CouncilName = name;

                row.CouncilSymbol = runs
                    .Where(r => r.Y < line.Y && line.Y - r.Y < 40 && Regex.IsMatch(r.Text.Trim(), @"^\d{7,8}$"))
                    .OrderByDescending(r => r.Y)
                    .Select(r => r.Text.Trim())
                    .FirstOrDefault();
            }

            row.CouncilSymbol ??= runs
                .Where(r => Regex.IsMatch(r.Text.Trim(), @"^\d{7,8}$"))
                .Select(r => r.Text.Trim())
                .FirstOrDefault();
        }

        private static string RepairCouncilName(string name)
        {
            name = Regex.Replace(name, @"\s+", " ").Trim();
            name = Regex.Replace(name, @"\bמ\s+מקומית\b", "מועצה מקומית");
            name = Regex.Replace(name, @"מקומית\s+מ\b", "מקומית");
            if (name.StartsWith("מ ", StringComparison.Ordinal))
                name = name[2..].Trim();
            return name;
        }

        private static List<TextRun> ExtractRuns(Page page)
        {
            var letters = page.Letters
                .Select(l => (
                    Y: (l.GlyphRectangle.Bottom + l.GlyphRectangle.Top) / 2.0,
                    X: l.GlyphRectangle.Left,
                    W: l.GlyphRectangle.Width,
                    Ch: DecodeChar(l.Value)
                ))
                .Where(l => l.Ch.Length > 0)
                .OrderByDescending(l => l.Y)
                .ThenBy(l => l.X)
                .ToList();

            var runs = new List<TextRun>();
            if (letters.Count == 0)
                return runs;

            var clusters = new List<List<(double Y, double X, double W, string Ch)>>();
            foreach (var letter in letters)
            {
                var cluster = clusters.Count > 0 ? clusters[^1] : null;
                if (cluster == null || Math.Abs(cluster.Average(c => c.Y) - letter.Y) > 5.0)
                    clusters.Add(new List<(double, double, double, string)> { letter });
                else
                    cluster.Add(letter);
            }

            foreach (var cluster in clusters)
            {
                var ordered = cluster.OrderBy(c => c.X).ToList();
                var sb = new StringBuilder();
                double? lastRight = null;
                var startX = ordered[0].X;
                foreach (var c in ordered)
                {
                    if (lastRight.HasValue && c.X - lastRight.Value > 3.5)
                        sb.Append(' ');
                    sb.Append(c.Ch);
                    lastRight = c.X + Math.Max(c.W, 1);
                }

                var text = NormalizeRunText(sb.ToString());
                if (!string.IsNullOrWhiteSpace(text))
                {
                    runs.Add(new TextRun
                    {
                        Y = cluster.Average(c => c.Y),
                        X = startX,
                        Text = text
                    });
                }
            }

            return runs;
        }

        private static string DecodeChar(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                var o = (int)c;
                if (o >= 0x02A0 && o <= 0x02BA)
                    sb.Append((char)(o + 0x330));
                else if (o < 32)
                    continue;
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static string NormalizeRunText(string raw)
        {
            // Keep dates/numbers as atomic tokens so they are not reversed with Hebrew
            raw = Regex.Replace(raw, @"(\d{2}/\d{2}/\d{4})", " $1 ");
            raw = Regex.Replace(raw, @"(\d{1,3}%)", " $1 ");
            raw = Regex.Replace(raw, @"(\d{5,9})", " $1 ");
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(raw))
                return raw;

            // Visual LTR → reverse Hebrew chars within token; keep digits/dates; reverse token order
            var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => ContainsHebrew(t) ? Reverse(t) : t)
                .Reverse()
                .ToArray();

            return string.Join(' ', tokens);
        }

        private static string Reverse(string s)
        {
            var chars = s.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        private static bool ContainsHebrew(string text) =>
            text.Any(c => c is >= '\u05D0' and <= '\u05EA');

        private static byte[] BuildExcel(List<PersonalApprovalRow> rows)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("אישורים");

            string[] headers =
            {
                "תאריך אישור",
                "שם רשות",
                "סמל רשות",
                "שם פרטי",
                "שם משפחה",
                "ת.ז. תלמיד",
                "קוד תומכת חינוך",
                "מסגרת",
                "שם מוסד",
                "סמל מוסד",
                "שעות",
                "מתאריך",
                "עד תאריך",
                "השתתפות הרשות"
            };

            for (var i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            ws.Row(1).Style.Font.Bold = true;

            var r = 2;
            foreach (var row in rows)
            {
                ws.Cell(r, 1).Value = row.ApprovalDate ?? "";
                ws.Cell(r, 2).Value = row.CouncilName ?? "";
                ws.Cell(r, 3).Value = row.CouncilSymbol ?? "";
                ws.Cell(r, 4).Value = row.StudentFirstName ?? "";
                ws.Cell(r, 5).Value = row.StudentLastName ?? "";
                ws.Cell(r, 6).Value = row.StudentId ?? "";
                ws.Cell(r, 7).Value = row.SupportCode ?? "";
                ws.Cell(r, 8).Value = row.Framework ?? "";
                ws.Cell(r, 9).Value = row.InstitutionName ?? "";
                ws.Cell(r, 10).Value = row.InstitutionSymbol ?? "";
                ws.Cell(r, 11).Value = row.Hours ?? "";
                ws.Cell(r, 12).Value = row.StartDate ?? "";
                ws.Cell(r, 13).Value = row.EndDate ?? "";
                if (row.ParticipationPct.HasValue)
                {
                    // Store as Excel percentage (30 → 30%)
                    ws.Cell(r, 14).Value = row.ParticipationPct.Value / 100m;
                    ws.Cell(r, 14).Style.NumberFormat.Format = "0%";
                }
                else
                {
                    ws.Cell(r, 14).Value = "";
                }
                r++;
            }

            ws.Columns().AdjustToContents();

            using var outStream = new MemoryStream();
            workbook.SaveAs(outStream);
            return outStream.ToArray();
        }
    }
}
