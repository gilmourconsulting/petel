using DocumentFormat.OpenXml.Packaging;
using System.Text.RegularExpressions;

namespace Petel.Core.Documents
{
    /// <summary>
    /// Scans a .docx template file and returns all {{placeholder}} token names found in the document body.
    /// </summary>
    public class DocumentTemplateService
    {
        private static readonly Regex TokenRegex =
            new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

        /// <summary>
        /// Scan a .docx template blob and return the list of unique placeholder names
        /// (e.g. "header_SchoolName", "students").
        /// </summary>
        public IReadOnlyList<string> ScanPlaceholders(byte[] templateBlob)
        {
            using var ms  = new MemoryStream(templateBlob);
            using var doc = WordprocessingDocument.Open(ms, isEditable: false);

            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
                return Array.Empty<string>();

            var text = body.InnerText;

            var names = TokenRegex.Matches(text)
                .Select(m => m.Groups[1].Value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();

            return names;
        }
    }
}
