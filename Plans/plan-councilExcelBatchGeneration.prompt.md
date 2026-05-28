## Plan: Council Excel Batch Generation from School List

**TL;DR**: Add a "ייצוא Excel רשויות" button to `SchoolList.razor`. Clicking it queues a Hangfire background job that iterates all sending councils for the selected year (scoped to the logged-in entity), generates a per-council student Excel using `SQL/Templates/council-students-template.xlsx` via `ReportTemplateEngine`, and stores each as a document linked to the entity. Documents surface in the existing `EntityDocuments.razor` page.

---

### Phase 1 — Database & Template Preparation
1. **SQL migration** — Insert document type (idempotent `ON CONFLICT DO NOTHING`):
   `name = "Excel תלמידי רשויות"`, `level = "רשת"`, `year_id = NULL`

2. **Embed template** — Add `SQL/Templates/council-students-template.xlsx` to `PetelATH.Api.csproj` as `<EmbeddedResource>` so the job loads it from the assembly.

3. **Fix year filter in `by-entity-hierarchy`** — Extend the existing filter in `DocumentsController` from `d.DocumentType.YearId == yearId` to also include `|| d.DocumentType.YearId == null`, so council Excels (with null-YearId document type) appear in EntityDocuments regardless of which year is selected.

### Phase 2 — Backend Job Service
4. **New `CouncilExcelGenerationService`** in `PetelATH.Api/Services/`:
   - Injects `IServiceScopeFactory`, `ILogger`
   - Method `GenerateForAllCouncils(int entityId, int yearId, int? userId)` (Hangfire-safe, creates own scope):
     - Loads councils from `CouncilSummaryVw` scoped to entity + year
     - Finds document type by name `"Excel תלמידי רשויות"`
     - Loads template bytes from embedded resource
     - For each council: queries students → builds `ReportTemplateEngine` datasources → generates Excel bytes → creates `Document` + `DocumentLink` (EntityId = entityId) → sets MasterDocumentId *(mirrors existing `generate-school-documents` pattern)*
5. **Register** `CouncilExcelGenerationService` as **transient** in `Program.cs`

### Phase 3 — API Endpoint
6. **New endpoint** in `DocumentsController`:
   `POST /api/documents/generate-council-excels?yearId={yearId}`
   - Validates session → resolves `entityId`
   - Enqueues Hangfire job (or runs synchronously if Hangfire not configured)
   - Returns `202 Accepted` with `{ success: true, message: "הועבר לביצוע ברקע..." }`

### Phase 4 — Frontend
7. **Add button** to `SchoolList.razor` context-buttons section, using existing `SecureButton` pattern with `ActionName="schoollist_generateCouncilExcels"`
8. **Add handler + state fields** (`_isGenerating`, `_generateMessage`, `_generateError`) + inline feedback UI next to the button
9. On success: show toast "הפעולה הועברת לביצוע. הקבצים יופיעו במסמכי ישות בסיום."

---

### Relevant Files
- [SchoolList.razor](PetelATH/PetelATH.BlazorServer/Components/Pages/SchoolList.razor) — button + handler
- [DocumentsController.cs](PetelATH/PetelATH.Api/Controllers/DocumentsController.cs) — new endpoint + fix year filter
- `PetelATH/PetelATH.Api/Services/CouncilExcelGenerationService.cs` — **NEW**
- [PetelATH.Api.csproj](PetelATH/PetelATH.Api/PetelATH.Api.csproj) — embed template resource
- [Program.cs](PetelATH/PetelATH.Api/Program.cs) — register service
- `SQL/add-council-excel-doctype.sql` — **NEW** migration
- `SQL/Templates/council-students-template.xlsx` — source template

---

### Verification
1. `POST /api/documents/generate-council-excels?yearId=X` via Swagger → `202 Accepted`
2. Hangfire dashboard (`/hangfire`) → job shows Succeeded
3. `/entitydocuments` → council Excel files appear (one per council), visible with year filter active
4. Download one Excel → student data for that council is correct
5. Clicking the button a second time → existing documents for same council are skipped (not duplicated)
6. Without Hangfire connection string → synchronous fallback completes without error

---

### Further Considerations
1. **Template placeholders**: The exact `{{datasourceName.FieldName}}` names inside `council-students-template.xlsx` must be verified by opening the file before coding the datasource builder. This determines what the LINQ query must select.
2. **Duplicate prevention**: A second button-click would queue another job that creates duplicate documents. The service should check for an existing document of the same type + description per entity before inserting (skip or replace).
3. **Security action registration**: `ActionName="schoollist_generateCouncilExcels"` needs a row in `petel_schema.security_actions` + role assignment — check how the other `schoollist_*` actions are set up.
