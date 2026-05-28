## Plan: Pricing Elements as Student Fields in Excel Reports

**TL;DR**: Add a new registry entity `StudentsWithPricingElements` to `AthExcelEntityRegistry`. It mirrors all 18 fields of `StudentsWithSchool` and appends dynamic columns per pricing element — price, hours, and determining factor — keyed by the element's `Title` (Hebrew, ≤25 chars). Template designers use `{{students.מוגבלות}}`, `{{students.מוגבלות_שעות}}`, `{{students.מוגבלות_גורם}}`. No changes to `ReportTemplateEngine`, `CouncilExcelGenerationService`, or any Blazor page.

---

### Phase 1 — Registry Entity (`AthExcelEntityRegistry.cs`)

1. Add `"StudentsWithPricingElements"` arm to the `switch` in `QueryEntityAsync()` — after the existing `"StudentsWithSchool"` case
2. Add entity descriptor to `BuildDescriptors()` static list with display name `"תלמידים + מרכיבי תמחור"`, type `"collection"`
3. Add private `QueryStudentsWithPricingElementsAsync()`:
   - **a.** Get `yearIds` via existing `GetSchoolYearIdsAsync(context, ct)`
   - **b.** Run the exact same student + school logic as `QueryStudentsWithSchoolAsync` → base 18-field dictionaries
   - **c.** Load pricing elements for the Hebrew year: `WHERE pe.YearId == context.SchoolYearId ORDER BY SortOrder` — if `context.SchoolYearId` is null, return base rows as-is (no error)
   - **d.** Extract all `studentIds` and bulk-load assignments: `SchoolStudentPricingElements WHERE StudentId IN (...)`
   - **e.** Build in-memory lookup `studentId → (pricingElementId → (Price, Hours, DeterminingFactor))`
   - **f.** For each student row, iterate all pricing elements and add 3 keys per element:
     - `[element.Title]` = price (or null if no assignment)
     - `[element.Title + "_שעות"]` = hours
     - `[element.Title + "_גורם"]` = determining factor

### Phase 2 — Report Definition Update

4. Update `SQL/insert-council-students-report.sql`: change `"entity": "StudentsWithSchool"` → `"StudentsWithPricingElements"` in the `students` datasource
5. Add new `SQL/update-council-report-entity.sql` — idempotent `UPDATE ... SET definition_json = REPLACE(...)` to migrate existing databases

### Phase 3 — Template File (Manual Step)

6. `SQL/Templates/council-students-template.xlsx` must be opened in Excel and new pricing columns added using the token convention above — this is done by the template designer, not in code

---

### Relevant Files
- `PetelATH/PetelATH.Api/Services/AthExcelEntityRegistry.cs` — all code changes (Steps 1–3)
- `SQL/insert-council-students-report.sql` — Step 4
- `SQL/update-council-report-entity.sql` — **NEW** — Step 5

---

### Verification
1. Generate the council report with a year that has pricing elements → XLSX contains pricing columns, values are correct
2. Student with no assignment for an element → cell is empty (not a `{{...}}` literal)
3. Existing report using `StudentsWithSchool` entity → output unchanged (no regression)
4. `context.SchoolYearId == null` → no exception, base fields returned
5. Token for an element not in DB → empty cell (existing engine behavior)

---

### Decisions
- Token suffix: `_שעות` (hours), `_גורם` (determining factor) — Hebrew suffixes matching app language
- Field key = `element.Title` (≤25 chars) — case-insensitive match already handled by template engine
- `StudentsWithSchool` is untouched — new entity is purely additive
- Pricing elements filtered by HebrewYear (`pe.YearId`), not by `school_years.id`
- No UI/Blazor changes needed — `GetAvailableEntities()` auto-exposes the new entity in the report builder

---

### Further Considerations
1. **Shared code**: `QueryStudentsWithSchoolAsync` and `QueryStudentsWithPricingElementsAsync` share ~70 lines of student + school loading logic. A private helper `QueryBaseStudentRowsAsync()` could eliminate duplication — optional refactoring, not required.
2. **`Title` uniqueness**: If two elements for the same year share the same `Title`, their keys would collide in the dictionary (last one wins). Worth a log warning inside the method.
