# PetelATH — Reports, Excel & Documents

> Load only for ATH report/Excel/document work. Shared Blazor export: docs/agents/core/blazor-patterns.md

### Excel Import/Export Pattern

**Standard Implementation**: All Excel operations use EPPlus library with consistent error handling and validation.

**Required Package**: 
```xml
<PackageReference Include="EPPlus" Version="7.0.0" />
```

#### Import Pattern (Backend)

```csharp
[HttpPost("import")]
public async Task<IActionResult> ImportFromExcel(IFormFile file)
{
    if (file == null || file.Length == 0)
        return BadRequest("No file uploaded");

    if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        return BadRequest("Only .xlsx files are supported");

    if (file.Length > 10 * 1024 * 1024)  // 10MB limit
        return BadRequest("File too large (max 10MB)");

    var session = GetCurrentSession();
    var errors = new List<string>();
    var importedCount = 0;

    try
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        
        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension.Rows;

        // Stage 1: Header validation
        var expectedHeaders = new Dictionary<int, string>
        {
            { 1, "×ž×–×”×”" },
            { 2, "×©×" },
            { 3, "×›×™×ª×”" }
        };

        for (int col = 1; col <= expectedHeaders.Count; col++)
        {
            var header = worksheet.Cells[1, col].Text.Trim();
            if (header != expectedHeaders[col])
            {
                return BadRequest($"Invalid header in column {col}. Expected '{expectedHeaders[col]}', got '{header}'");
            }
        }

        // Stage 2: Duplicate detection in file
        var duplicateIds = new HashSet<string>();
        var existingIds = await _context.Students
            .Where(s => s.SchoolYearId == schoolYearId)
            .Select(s => s.StudentId)
            .ToListAsync();

        // Stage 3: Row processing with validation
        for (int row = 2; row <= rowCount; row++)
        {
            try
            {
                var id = worksheet.Cells[row, 1].Text.Trim();
                var name = worksheet.Cells[row, 2].Text.Trim();
                var className = worksheet.Cells[row, 3].Text.Trim();

                // Required field validation
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"Row {row}: Missing required ID");
                    continue;
                }

                // Duplicate in file check
                if (duplicateIds.Contains(id))
                {
                    errors.Add($"Row {row}: Duplicate ID '{id}' in import file");
                    continue;
                }
                duplicateIds.Add(id);

                // Duplicate in database check
                if (existingIds.Contains(id))
                {
                    errors.Add($"Row {row}: ID '{id}' already exists in database");
                    continue;
                }

                // Use GlobalFunctions for entity resolution
                var classId = await _globalFunctions.GetClassIdByName(className, schoolYearId);
                if (classId == null)
                {
                    errors.Add($"Row {row}: Class '{className}' not found");
                    continue;
                }

                // Create entity
                var entity = new MyEntity
                {
                    Id = id,
                    Name = name,
                    ClassId = classId.Value
                };

                _context.MyEntities.Add(entity);
                importedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {row}: {ex.Message}");
            }
        }

        if (importedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            ImportedCount = importedCount,
            ErrorCount = errors.Count,
            Errors = errors.Take(50).ToList(),  // Limit to first 50
            HasMoreErrors = errors.Count > 50
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error importing Excel file");
        return StatusCode(500, $"Error processing file: {ex.Message}");
    }
}
```

#### Export Pattern (Backend)

```csharp
[HttpGet("export")]
public async Task<IActionResult> ExportToExcel()
{
    var session = GetCurrentSession();
    
    try
    {
        var data = await _context.MyEntities
            .Where(e => e.EntityId == int.Parse(session.EntityId))
            .Include(e => e.RelatedEntity)  // Use navigation properties
            .ToListAsync();

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("× ×ª×•× ×™×");

        // Headers with RTL support
        var headers = new[] { "×ž×–×”×”", "×©×", "×ª×™××•×¨" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
        }

        // Data rows
        for (int i = 0; i < data.Count; i++)
        {
            var item = data[i];
            var row = i + 2;
            
            worksheet.Cells[row, 1].Value = item.Id;
            worksheet.Cells[row, 2].Value = item.Name;
            worksheet.Cells[row, 3].Value = item.Description;
        }

        // Formatting
        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        worksheet.View.RightToLeft = true;

        var stream = new MemoryStream(package.GetAsByteArray());
        var fileName = $"Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        
        return File(stream, 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
            fileName);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error exporting to Excel");
        return StatusCode(500, "Error generating Excel file");
    }
}
```

#### Blazor Frontend Integration

Use `ApiService` from `Petel.BlazorCore` for all Excel operations:

```csharp
// Upload (in a modal component)
async Task UploadExcelAsync(IBrowserFile file)
{
    using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
    using var content = new MultipartFormDataContent();
    content.Add(new StreamContent(stream), "file", file.Name);
    var result = await ApiService.PostMultipartAsync<ImportResult>("myentities/import", content);
    // result.ImportedCount, result.Errors, etc.
}

// Download
async Task DownloadExcelAsync()
{
    var response = await ApiService.GetFileAsync("myentities/export");
    // Use JSRuntime to trigger browser download from blob
}
```

#### Import Validation Best Practices

**Multi-Stage Validation**:
1. âœ… File format validation (extension, size)
2. âœ… Structure validation (headers, column count)
3. âœ… Data type validation (per column)
4. âœ… Business logic validation (required fields, duplicates)
5. âœ… Reference validation (foreign keys exist)

**Error Collection**:
- âœ… Collect ALL errors, don't stop on first error
- âœ… Include row number and column in error messages
- âœ… Return summary with counts and detailed error list
- âœ… Log errors for debugging

**Best Practices**:
```csharp
// âœ… CORRECT - Collect errors and continue
if (string.IsNullOrWhiteSpace(value))
{
    errors.Add($"Row {row}: Invalid value in column {col}");
    continue;
}

// âŒ WRONG - Throwing on first error
if (string.IsNullOrWhiteSpace(value))
    throw new Exception("Invalid value");  // NO!

// âœ… CORRECT - Use GlobalFunctions for lookups
var classId = await _globalFunctions.GetClassIdByName(className, yearId);

// âŒ WRONG - Direct database query
var classId = _context.SchoolClasses
    .FirstOrDefault(c => c.ClassName == className)?.Id;  // NO!
```


## Document Template Generation (Word/DOCX)

**Purpose**: Generate Word (`.docx`) documents from database-stored templates, mirroring the Excel generation pattern. Both formats share the same `report_*` tables and controllers â€” the `format` column on `report_definitions` selects the engine.

### Required Package (Petel.Core)

```xml
<PackageReference Include="MiniWord" Version="0.9.2" />
<PackageReference Include="DocumentFormat.OpenXml" Version="3.2.0" />
```

### Shared Services in `Petel.Core/Documents/`

| Class | Namespace | Purpose |
|---|---|---|
| `DocumentTemplateEngine` | `Petel.Core.Documents` | Generates `.docx` from a template blob + data context |
| `DocumentTemplateService` | `Petel.Core.Documents` | Scans `.docx` for `{{placeholder}}` tokens |

Register in API `Program.cs`:
```csharp
builder.Services.AddScoped<Petel.Core.Documents.DocumentTemplateEngine>();
builder.Services.AddScoped<Petel.Core.Documents.DocumentTemplateService>();
```

### Word Template Syntax

| Syntax | Binding |
|---|---|
| `{{DataSourceName_FieldName}}` | Scalar value from a single-row dataset |
| `{{listName}}` (in a table row) | Collection â€” dataset name is the list key |

**CRITICAL**: `MiniWord.SaveAsByTemplate` requires **file paths**, not streams. `DocumentTemplateEngine` writes the template blob to a temp file internally and cleans up in a `finally` block. Never call MiniWord directly with streams.

### Database Tables (Unified)

All reports â€” Excel and Word â€” share the same four tables:

| Table | Key Column | Notes |
|---|---|---|
| `report_definitions` | `format VARCHAR(10)` | `"excel"` (default) or `"word"` |
| `report_queries` | â€” | SQL / data-source query |
| `report_templates` | â€” | Binary blob of `.xlsx` or `.docx` |
| `report_parameters` | â€” | Runtime parameter definitions |

**SQL migration**: `SQL/rename-report-tables.sql` â€” renames legacy `excel_report_*` tables and adds `format` column.

### Controller Pattern

```csharp
// ReportsController.cs  (route: "api/reports")
[HttpGet("{id}/generate")]
public async Task<IActionResult> GenerateReport(int id)
{
    var session = GetCurrentSession();
    if (session == null) return Unauthorized(...);

    var report = await _context.ReportDefinitions
        .Include(r => r.Template)
        .Include(r => r.Query)
        .FirstOrDefaultAsync(r => r.Id == id);

    if (report == null) return NotFound();

    if (report.Format == "word")
        return await GenerateWordTemplateReportAsync(report, session);
    else
        return await GenerateTemplateReportAsync(report, session);   // Excel
}

private async Task<IActionResult> GenerateWordTemplateReportAsync(
    ReportDefinition report, UserSession session)
{
    var context = await BuildExcelEntityContext(report, session);   // shared helper
    var docBytes = await _docEngine.GenerateAsync(
        report.Template!.TemplateBlob, report.Query!.DefinitionJson,
        context, runtimeParams: new Dictionary<string, string>());

    var fileName = $"{report.Name}_{DateTime.Now:yyyyMMdd}.docx";
    return File(docBytes,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        fileName);
}
```

### Template Upload / Scan

`ReportTemplatesController` (`route: "api/reporttemplates"`) handles both formats:
- Accepts `.xlsx` **and** `.docx` on upload
- Routes placeholder scanning to `DocumentTemplateService` (Word) or `ExcelTemplateService` (Excel) based on filename extension
- Returns format-appropriate `Content-Type` on download

### Anti-Patterns

```csharp
// âŒ WRONG â€” old Excel-prefixed names (all removed)
_context.ExcelReportDefinitions
new ExcelReportDefinition()

// âœ… CORRECT â€” unified names
_context.ReportDefinitions
new ReportDefinition { Format = "word" }

// âŒ WRONG â€” calling MiniWord with a stream
MiniWord.SaveAsByTemplate(outputStream, templateStream, dict);  // NOT SUPPORTED

// âœ… CORRECT â€” use DocumentTemplateEngine (handles temp files internally)
var bytes = await _docEngine.GenerateAsync(templateBlob, definitionJson, context, runtimeParams);

// âŒ WRONG â€” selecting engine by file extension instead of DB format field
if (fileName.EndsWith(".docx")) { ... }

// âœ… CORRECT â€” use report.Format from DB
if (report.Format == "word") return await GenerateWordTemplateReportAsync(...);
```
