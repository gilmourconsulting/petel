using MiniSoftware;
using System.Dynamic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Petel.Core.Documents
{
    /// <summary>
    /// Fills a Word (.docx) template from a <see cref="Excel.ReportTemplateSchema"/>.
    ///
    /// Template syntax (MiniWord)
    /// ───────────────────────────
    /// Scalar placeholder:    {{dsName_FieldName}}
    ///   – Replaced with a single value from a "scalar" data source.
    ///
    /// Collection table row:  first column cell contains {{listName}}
    ///                        other cells contain {{listName.FieldName}}
    ///   – MiniWord detects the list key and expands the row for each element.
    /// </summary>
    public class DocumentTemplateEngine
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Excel.IExcelEntityRegistry _registry;
        private readonly ILogger<DocumentTemplateEngine> _logger;

        public DocumentTemplateEngine(
            Excel.IExcelEntityRegistry registry,
            ILogger<DocumentTemplateEngine> logger)
        {
            _registry = registry;
            _logger = logger;
        }

        /// <summary>
        /// Generate a filled .docx file from a template and a definition JSON.
        /// </summary>
        /// <param name="templateBlob">Raw bytes of the .docx template file.</param>
        /// <param name="definitionJson">JSON string conforming to <see cref="Excel.ReportTemplateSchema"/>.</param>
        /// <param name="context">Entity/year scope for registry queries.</param>
        /// <param name="runtimeParams">Values supplied by the caller (year id, council id, etc.).</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<byte[]> GenerateAsync(
            byte[] templateBlob,
            string definitionJson,
            Excel.ExcelEntityContext context,
            Dictionary<string, string> runtimeParams,
            CancellationToken ct = default)
        {
            var definition = JsonSerializer.Deserialize<Excel.ReportTemplateSchema>(definitionJson, JsonOpts)
                ?? throw new InvalidOperationException("Invalid report definition JSON.");

            // ── Build MiniWord value dictionary ───────────────────────────
            // Scalar sources  → flat keys "dsName_FieldName"
            // Collection sources → key "dsName" with List<ExpandoObject> value
            var valueDict = new Dictionary<string, object>();

            foreach (var ds in definition.DataSources)
            {
                ct.ThrowIfCancellationRequested();

                var config = new Excel.ExcelQueryConfig
                {
                    EntityName = ds.Entity,
                    Filters    = ds.Filters,
                    Sort       = ds.Sort,
                    Fields     = new List<Excel.ExcelQueryConfig.SelectedField>()
                };

                var rows = await _registry.QueryEntityAsync(config, context, runtimeParams, ct);

                if (string.Equals(ds.Type, "scalar", StringComparison.OrdinalIgnoreCase))
                {
                    var row = rows.FirstOrDefault() ?? new Dictionary<string, object?>();
                    foreach (var kv in row)
                    {
                        var key = $"{ds.Name}_{kv.Key}";
                        valueDict[key] = kv.Value ?? string.Empty;
                    }
                }
                else
                {
                    // Convert each row dict to an ExpandoObject so MiniWord can read properties
                    var expandoList = rows.Select(RowToExpando).ToList();
                    valueDict[ds.Name] = expandoList;
                }
            }

            // ── Fill template with MiniWord (uses temp files; MiniWord requires file paths) ──
            var templatePath = Path.Combine(Path.GetTempPath(), $"miniword_tpl_{Guid.NewGuid():N}.docx");
            var outputPath   = Path.Combine(Path.GetTempPath(), $"miniword_out_{Guid.NewGuid():N}.docx");
            try
            {
                await File.WriteAllBytesAsync(templatePath, templateBlob, ct);
                MiniWord.SaveAsByTemplate(outputPath, templatePath, valueDict);
                return await File.ReadAllBytesAsync(outputPath, ct);
            }
            finally
            {
                if (File.Exists(templatePath)) File.Delete(templatePath);
                if (File.Exists(outputPath))   File.Delete(outputPath);
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private static ExpandoObject RowToExpando(Dictionary<string, object?> row)
        {
            var expando = (IDictionary<string, object?>)new ExpandoObject();
            foreach (var kv in row)
                expando[kv.Key] = kv.Value;
            return (ExpandoObject)expando;
        }
    }
}
