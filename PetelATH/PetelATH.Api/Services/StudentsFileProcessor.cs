using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Models.DTOs;
using System.Globalization;


namespace PetelATH.Api.Services
{
    public class StudentsFileProcessor
    {
        private readonly GlobalFunctions _globalFunctions;
        private readonly AppDbContext _context;
        private readonly ILogger<StudentsFileProcessor> _logger;
        private readonly StudentService _studentService;
        private readonly StudentPricingService _pricingService;

        public StudentsFileProcessor(
            AppDbContext context,
            ILogger<StudentsFileProcessor> logger,
            GlobalFunctions globalFunctions,
            StudentService studentService,
            StudentPricingService pricingService)
        {
            _context = context;
            _logger = logger;
            _globalFunctions = globalFunctions;
            _studentService = studentService;
            _pricingService = pricingService;
        }

        /// <summary>
        /// Process student rows from uploaded file.
        /// Date/council changes on existing students are queued for confirmation.
        /// </summary>
        public async Task<ProcessingResult> ProcessStudentRowsAsync(
            List<StudentFileRow> rows,
            int schoolId,
            int schoolYearId,
            string userId)
        {
            _logger.LogInformation("=== STARTING FILE PROCESSING === RowCount={RowCount}, SchoolId={SchoolId}, SchoolYearId={SchoolYearId}, UserId={UserId}",
                rows.Count, schoolId, schoolYearId, userId);
            var result = new ProcessingResult();

            var schoolYear = await _context.SchoolYears
                .AsNoTracking()
                .FirstOrDefaultAsync(sy => sy.Id == schoolYearId);

            if (schoolYear == null)
            {
                result.Errors.Add($"שנת לימודים {schoolYearId} לא נמצאה במערכת");
                _logger.LogError("School year not found. SchoolYearId={SchoolYearId}", schoolYearId);
                return result;
            }

            bool validateIdChecksum = await ShouldValidateIdNumberAsync();

            var allStudentsInYear = await _context.SchoolStudents
                .Where(s => s.IsLastVersion && s.SchoolYearId == schoolYearId)
                .ToListAsync();

            var councilNames = await _context.Councils
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            foreach (var group in rows.GroupBy(r => r.IdNumber))
            {
                try
                {
                    await ProcessStudentGroupAsync(
                        group.ToList(), schoolYearId, userId, result, schoolYear,
                        allStudentsInYear, councilNames, validateIdChecksum);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing student {IdNumber}", group.Key);
                    result.Errors.Add($"{group.Key} - שגיאת עיבוד: {ex.Message}");
                }
            }

            return result;
        }

        private async Task ProcessStudentGroupAsync(
            List<StudentFileRow> groupRows,
            int schoolYearId,
            string userId,
            ProcessingResult result,
            SchoolYear schoolYear,
            List<SchoolStudent> allStudentsInYear,
            Dictionary<int, string> councilNames,
            bool validateIdChecksum)
        {
            var idNumber = groupRows[0].IdNumber;
            _logger.LogInformation("Processing student {IdNumber} with {RowCount} file row(s)", idNumber, groupRows.Count);

            var resolved = new List<ResolvedPeriod>();
            bool groupInvalid = false;

            foreach (var row in groupRows)
            {
                var (isValid, formatError) = ValidateRowFormat(row, validateIdChecksum, schoolYear.StartDate, schoolYear.EndDate);
                if (!isValid)
                {
                    result.Errors.Add($"{row.IdNumber} - שגיאת פורמט: {formatError}");
                    groupInvalid = true;
                    continue;
                }

                var classId = await _globalFunctions.GetClassIdByName(row.Class, schoolYearId);
                if (classId == null)
                {
                    result.Errors.Add($"{row.IdNumber} - כיתה '{row.Class}' לא נמצאה בשנת הלימודים");
                    groupInvalid = true;
                    continue;
                }

                var councilId = await ResolveCouncilIdAsync(row.SendingCouncil, result, row.IdNumber);
                if (councilId == null && !string.IsNullOrWhiteSpace(row.SendingCouncil) && row.SendingCouncil != "99999")
                {
                    groupInvalid = true;
                    continue;
                }

                var hebrewCulture = CultureInfo.GetCultureInfo("he-IL");
                resolved.Add(new ResolvedPeriod
                {
                    Row = row,
                    Start = DateOnly.FromDateTime(DateTime.Parse(row.StartDate, hebrewCulture)),
                    End = DateOnly.FromDateTime(DateTime.Parse(row.EndDate, hebrewCulture)),
                    CouncilId = councilId,
                    ClassId = classId.Value
                });
            }

            if (groupInvalid || resolved.Count == 0)
                return;

            resolved = CollapseDuplicatePeriods(resolved);

            var existingStudent = allStudentsInYear.FirstOrDefault(s => s.IdNumber == idNumber);
            var existingPeriods = existingStudent == null
                ? new List<SchoolStudent>()
                : await LoadBillablePeriodsAsync(existingStudent.MasterStudentId, schoolYearId);

            if (FilePeriodsConflict(resolved))
            {
                var pending = BuildPendingItem(
                    existingStudent, existingPeriods, resolved, councilNames,
                    StudentUploadPromptType.SplitCouncilBlocked, blocked: true);
                result.Pending.Add(pending);
                result.Errors.Add($"{idNumber} - התקופות חופפות. לא ניתן לקבל.");
                return;
            }

            if (existingStudent == null)
            {
                var created = await CreateAllPeriodsForNewStudentAsync(resolved, schoolYearId, userId);
                if (created.HasValue)
                {
                    var last = await _context.SchoolStudents.FirstOrDefaultAsync(s => s.Id == created.Value);
                    if (last != null)
                        allStudentsInYear.Add(last);
                    result.Created++;
                    _logger.LogInformation("Created new student {IdNumber} with {PeriodCount} period(s)", idNumber, resolved.Count);
                }
                return;
            }

            if (AllFilePeriodsAlreadyBillable(resolved, existingPeriods))
            {
                await NormalizeBillableDuplicatesAsync(existingStudent.MasterStudentId, schoolYearId);
                var fileForLast = resolved.FirstOrDefault(r =>
                    r.Start == existingStudent.StartDate
                    && r.End == existingStudent.EndDate
                    && r.CouncilId == existingStudent.SendingCouncil);

                if (fileForLast != null && HasOtherFieldsChanged(existingStudent, fileForLast))
                {
                    await UpdateStudentWithNewVersionAsync(existingStudent, fileForLast, userId);
                    await NormalizeBillableDuplicatesAsync(existingStudent.MasterStudentId, schoolYearId);
                    result.Updated++;
                    return;
                }

                result.Unchanged.Add($"{idNumber} - נתונים לא השתנו");
                return;
            }

            string type;
            bool blocked = false;
            if (resolved.Count > 1)
            {
                type = StudentUploadPromptType.MultiPeriod;
            }
            else
            {
                type = ClassifyAgainstExisting(existingStudent, resolved[0], out blocked);
            }

            bool requiresChoice = !blocked && RequiresSameCouncilChoice(existingPeriods, resolved);
            var item = BuildPendingItem(
                existingStudent, existingPeriods, resolved, councilNames, type, blocked, requiresChoice);
            result.Pending.Add(item);
            if (item.IsBlocked)
                result.Errors.Add($"{idNumber} - התקופות חופפות. לא ניתן לקבל.");
        }

        public async Task<ProcessingResult> ConfirmPendingAsync(
            List<StudentUploadPendingItem> accepted,
            string userId)
        {
            var result = new ProcessingResult();
            if (accepted == null || accepted.Count == 0)
                return result;

            foreach (var item in accepted)
            {
                try
                {
                    await ApplyPendingItemAsync(item, result, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error confirming student {StudentId}", item.ExistingStudentId);
                    result.Errors.Add($"{item.IdNumber} - שגיאת אישור: {ex.Message}");
                }
            }

            return result;
        }

        private async Task ApplyPendingItemAsync(StudentUploadPendingItem item, ProcessingResult result, string userId)
        {
            if (item.IsBlocked || item.Type == StudentUploadPromptType.SplitCouncilBlocked)
            {
                result.Errors.Add($"{item.IdNumber} - לא ניתן לאשר תקופות חופפות");
                return;
            }

            var existing = await _context.SchoolStudents
                .FirstOrDefaultAsync(s => s.Id == item.ExistingStudentId && s.IsLastVersion);

            if (existing == null)
            {
                result.Errors.Add($"{item.IdNumber} - תלמיד לא נמצא או אינו הגרסה האחרונה");
                return;
            }

            var proposed = GetProposedPeriods(item);
            if (proposed.Count == 0)
            {
                result.Errors.Add($"{item.IdNumber} - חסרות תקופות לאישור");
                return;
            }

            var suggested = item.SuggestedUpdates ?? new List<StudentUploadPeriodDto>();
            var toApply = proposed.Concat(suggested).ToList();

            if (item.Type == StudentUploadPromptType.SplitCouncil && proposed.Count == 1)
            {
                var period = proposed[0];
                if (!period.StartDate.HasValue || !period.EndDate.HasValue ||
                    !existing.StartDate.HasValue || !existing.EndDate.HasValue)
                {
                    result.Errors.Add($"{item.IdNumber} - חסרים תאריכים לפיצול רשות");
                    return;
                }

                if (DatesOverlap(existing.StartDate.Value, existing.EndDate.Value,
                    period.StartDate.Value, period.EndDate.Value))
                {
                    result.Errors.Add($"{item.IdNumber} - התקופות חופפות, לא ניתן לאשר");
                    return;
                }
            }

            var oldId = existing.Id;
            var originalStatus = existing.StatusId;
            var createdIds = new List<int>();
            var matchingIds = new List<int>();
            int sourceId = existing.Id;

            var billable = await LoadBillablePeriodsAsync(existing.MasterStudentId, existing.SchoolYearId);

            foreach (var period in toApply.OrderBy(p => p.StartDate))
            {
                var match = billable.FirstOrDefault(b => PeriodMatches(b, period));
                if (match != null)
                {
                    matchingIds.Add(match.Id);
                    continue;
                }

                var newVersionId = await _studentService.CreateNewStudentVersionAsync(
                    sourceId,
                    newVersion => ApplyPeriodFields(newVersion, period, isNew: false, originalStatus, unmatchedPeriod: true),
                    ParseUserId(userId));

                if (!newVersionId.HasValue)
                {
                    result.Errors.Add($"{item.IdNumber} - יצירת גרסה חדשה נכשלה");
                    return;
                }

                createdIds.Add(newVersionId.Value);
                sourceId = newVersionId.Value;
            }

            var currentIds = matchingIds.Concat(createdIds).ToHashSet();
            bool keepBoth = !item.RequiresSameCouncilChoice
                || string.Equals(item.SameCouncilApplyMode, SameCouncilApplyMode.KeepBoth, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(item.SameCouncilApplyMode);

            if (item.Type == StudentUploadPromptType.SameEndCouncilSplit)
            {
                // Cap is in createdIds; do not keep the uncapped old row.
            }
            else if (item.RequiresSameCouncilChoice && keepBoth)
            {
                foreach (var row in billable)
                {
                    if (currentIds.Contains(row.Id) || !row.StartDate.HasValue || !row.EndDate.HasValue)
                        continue;
                    bool keepOldSameCouncil = proposed.Any(p =>
                        p.CouncilId == row.SendingCouncil
                        && p.StartDate.HasValue && p.EndDate.HasValue
                        && (p.StartDate != row.StartDate || p.EndDate != row.EndDate)
                        && !DatesOverlap(row.StartDate.Value, row.EndDate.Value, p.StartDate.Value, p.EndDate.Value));
                    if (keepOldSameCouncil)
                        currentIds.Add(row.Id);
                }
            }
            else if (!item.RequiresSameCouncilChoice && item.Type == StudentUploadPromptType.SplitCouncil)
            {
                currentIds.Add(oldId);
            }

            await AssignCurrentPeriodIncludesAsync(existing.MasterStudentId, existing.SchoolYearId, currentIds);

            foreach (var id in currentIds.Distinct())
                await _pricingService.RecalculateAndSaveInPlaceAsync(id);

            result.Updated++;
            _logger.LogInformation("Confirmed {Type} for student {IdNumber}, versions {Ids}",
                item.Type, item.IdNumber, string.Join(",", createdIds));
        }

        private StudentUploadPendingItem BuildPendingItem(
            SchoolStudent? existing,
            List<SchoolStudent> existingPeriods,
            List<ResolvedPeriod> proposed,
            Dictionary<int, string> councilNames,
            string type,
            bool blocked,
            bool requiresSameCouncilChoice = false)
        {
            var first = proposed[0];
            string studentName = existing != null
                ? $"{existing.FirstName} {existing.LastName}".Trim()
                : $"{first.Row.FirstName} {first.Row.LastName}".Trim();

            var existingDtos = existingPeriods
                .GroupBy(p => (p.StartDate, p.EndDate, p.SendingCouncil))
                .Select(g => g.First())
                .OrderBy(p => p.StartDate)
                .Select(p => new StudentUploadPeriodDto
                {
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    CouncilId = p.SendingCouncil,
                    CouncilName = FormatCouncilName(p.SendingCouncil, councilNames)
                })
                .ToList();

            if (existingDtos.Count == 0 && existing != null)
            {
                existingDtos.Add(new StudentUploadPeriodDto
                {
                    StartDate = existing.StartDate,
                    EndDate = existing.EndDate,
                    CouncilId = existing.SendingCouncil,
                    CouncilName = FormatCouncilName(existing.SendingCouncil, councilNames)
                });
            }

            var proposedDtos = proposed
                .OrderBy(p => p.Start)
                .Select(p => ToPeriodDto(p, councilNames))
                .ToList();

            var item = new StudentUploadPendingItem
            {
                Type = type,
                IsBlocked = blocked,
                ExistingStudentId = existing?.Id ?? 0,
                IdNumber = first.Row.IdNumber,
                StudentName = studentName,
                ExistingStartDate = existing?.StartDate,
                ExistingEndDate = existing?.EndDate,
                ExistingCouncilId = existing?.SendingCouncil,
                ExistingCouncilName = existing == null ? null : FormatCouncilName(existing.SendingCouncil, councilNames),
                ProposedStartDate = first.Start,
                ProposedEndDate = first.End,
                ProposedCouncilId = first.CouncilId,
                ProposedCouncilName = FormatCouncilName(first.CouncilId, councilNames),
                FirstName = first.Row.FirstName,
                LastName = first.Row.LastName,
                Gender = ParseGender(first.Row.Gender),
                ClassId = first.ClassId,
                DisabilityCategory = string.IsNullOrWhiteSpace(first.Row.DisabilityCategory)
                    ? null
                    : int.Parse(first.Row.DisabilityCategory),
                Street = first.Row.Street ?? string.Empty,
                HouseNumber = first.Row.HouseNumber?.Trim() ?? string.Empty,
                City = first.Row.City,
                PostCode = first.Row.PostCode ?? string.Empty,
                ExistingPeriods = existingDtos,
                ProposedPeriods = proposedDtos,
                RequiresSameCouncilChoice = requiresSameCouncilChoice
            };

            if (!blocked && type == StudentUploadPromptType.SameEndCouncilSplit && existing != null)
            {
                var cap = BuildCappedExistingPeriod(existing, proposed[0], councilNames);
                if (cap != null)
                    item.SuggestedUpdates.Add(cap);
            }

            item.Question = BuildSimpleQuestion(
                studentName, first.Row.IdNumber, existingDtos, proposedDtos, item.SuggestedUpdates,
                blocked, requiresSameCouncilChoice);
            return item;
        }

        private StudentUploadPeriodDto? BuildCappedExistingPeriod(
            SchoolStudent existing,
            ResolvedPeriod filePeriod,
            Dictionary<int, string> councilNames)
        {
            if (!existing.StartDate.HasValue)
                return null;

            var capEnd = filePeriod.Start.AddDays(-1);
            if (capEnd < existing.StartDate.Value)
                return null;

            return new StudentUploadPeriodDto
            {
                StartDate = existing.StartDate,
                EndDate = capEnd,
                CouncilId = existing.SendingCouncil,
                CouncilName = FormatCouncilName(existing.SendingCouncil, councilNames),
                FirstName = existing.FirstName ?? string.Empty,
                LastName = existing.LastName ?? string.Empty,
                Gender = existing.Gender,
                ClassId = existing.ClassId ?? 0,
                DisabilityCategory = existing.DisabilityCategory,
                Street = existing.Street ?? string.Empty,
                HouseNumber = existing.HouseNumber ?? string.Empty,
                City = existing.City ?? string.Empty,
                PostCode = existing.PostCode ?? string.Empty
            };
        }

        private static string BuildSimpleQuestion(
            string studentName,
            string idNumber,
            List<StudentUploadPeriodDto> existing,
            List<StudentUploadPeriodDto> proposed,
            List<StudentUploadPeriodDto> suggestedUpdates,
            bool blocked,
            bool requiresSameCouncilChoice)
        {
            var lines = new List<string>
            {
                $"{studentName} ({idNumber})",
                "",
                "קיים במערכת:"
            };

            if (existing.Count == 0)
                lines.Add("אין רשומה קיימת");
            else
                lines.AddRange(existing.Select(FormatPeriodLine));

            if (!blocked && suggestedUpdates.Count > 0)
            {
                var toUpdate = suggestedUpdates
                    .Concat(proposed)
                    .OrderBy(p => p.StartDate)
                    .ToList();

                lines.Add("");
                lines.Add("לעדכן:");
                lines.AddRange(toUpdate.Select(FormatPeriodLine));
                lines.Add("");
                lines.Add("?");
                return string.Join("\n", lines);
            }

            lines.Add("");
            lines.Add("בקובץ:");
            lines.AddRange(proposed.Select(FormatPeriodLine));

            lines.Add("");
            if (blocked)
                lines.Add("התקופות חופפות. לא ניתן לקבל.");
            else if (requiresSameCouncilChoice)
                lines.Add("יש לבחור: שתי התקופות לחיוב, או תיקון.");
            else
                lines.Add("לאשר עדכון?");
            return string.Join("\n", lines);
        }

        private static string FormatPeriodLine(StudentUploadPeriodDto period) =>
            $"{period.CouncilName ?? "ללא רשות"}, {FormatDateRange(period.StartDate, period.EndDate)}";

        private static void ApplyPeriodFields(
            SchoolStudent student,
            StudentUploadPeriodDto period,
            bool isNew,
            int? previousStatus,
            bool unmatchedPeriod = false)
        {
            student.FirstName = period.FirstName;
            student.LastName = period.LastName;
            student.Gender = period.Gender;
            student.ClassId = period.ClassId;
            student.StartDate = period.StartDate;
            student.EndDate = period.EndDate;
            student.DisabilityCategory = period.DisabilityCategory;
            student.Street = period.Street ?? string.Empty;
            student.HouseNumber = period.HouseNumber ?? string.Empty;
            student.City = period.City;
            student.PostCode = period.PostCode ?? string.Empty;
            student.SendingCouncil = period.CouncilId;
            student.IncludeInCouncilSummary = false;
            student.StatusId = unmatchedPeriod && !isNew
                ? ResolveUnmatchedPeriodStatus(student.StartDate, student.EndDate)
                : ResolveUploadStatus(isNew, previousStatus, student.StartDate, student.EndDate);
        }

        private static int ResolveUnmatchedPeriodStatus(DateOnly? start, DateOnly? end)
        {
            if (start.HasValue && end.HasValue && start == end)
                return 8;
            return 9;
        }

        private static bool PeriodMatches(SchoolStudent existing, StudentUploadPeriodDto period) =>
            existing.StartDate == period.StartDate
            && existing.EndDate == period.EndDate
            && existing.SendingCouncil == period.CouncilId;

        /// <summary>
        /// New student → 1. Previous 2 or 4 → 9. Otherwise keep previous. Start = end → 8 (strongest).
        /// </summary>
        private static int ResolveUploadStatus(bool isNew, int? previousStatus, DateOnly? start, DateOnly? end)
        {
            if (start.HasValue && end.HasValue && start == end)
                return 8;
            if (isNew)
                return 1;
            if (previousStatus == 2 || previousStatus == 4)
                return 9;
            return previousStatus ?? 1;
        }

        private async Task AssignCurrentPeriodIncludesAsync(
            int masterStudentId,
            int schoolYearId,
            IEnumerable<int> currentPeriodIds)
        {
            var idSet = currentPeriodIds.ToHashSet();
            var versions = await _context.SchoolStudents
                .Where(s => s.MasterStudentId == masterStudentId && s.SchoolYearId == schoolYearId)
                .ToListAsync();

            foreach (var row in versions)
            {
                if (row.IsLastVersion)
                    row.IncludeInCouncilSummary = false;
                else
                    row.IncludeInCouncilSummary = idSet.Contains(row.Id);
            }

            await _context.SaveChangesAsync();
        }

        private async Task NormalizeBillableDuplicatesAsync(int masterStudentId, int schoolYearId)
        {
            var versions = await _context.SchoolStudents
                .Where(s => s.MasterStudentId == masterStudentId && s.SchoolYearId == schoolYearId)
                .ToListAsync();

            var keepIds = versions
                .Where(s => s.IsLastVersion || s.IncludeInCouncilSummary)
                .GroupBy(s => (s.StartDate, s.EndDate, s.SendingCouncil))
                .Select(g => g.OrderByDescending(x => x.IsLastVersion).ThenByDescending(x => x.Version).First().Id)
                .ToList();

            await AssignCurrentPeriodIncludesAsync(masterStudentId, schoolYearId, keepIds);
        }

        private static bool AllFilePeriodsAlreadyBillable(List<ResolvedPeriod> file, List<SchoolStudent> billable)
        {
            if (file.Count == 0)
                return false;

            var dbKeys = billable
                .Where(b => b.StartDate.HasValue && b.EndDate.HasValue)
                .Select(b => (b.StartDate!.Value, b.EndDate!.Value, b.SendingCouncil))
                .ToHashSet();

            return file.All(p => dbKeys.Contains((p.Start, p.End, p.CouncilId)));
        }

        private static bool RequiresSameCouncilChoice(List<SchoolStudent> billable, List<ResolvedPeriod> file)
        {
            foreach (var period in file)
            {
                foreach (var existing in billable)
                {
                    if (existing.SendingCouncil != period.CouncilId)
                        continue;
                    if (existing.StartDate == period.Start && existing.EndDate == period.End)
                        continue;
                    if (!existing.StartDate.HasValue || !existing.EndDate.HasValue)
                        continue;
                    if (!DatesOverlap(existing.StartDate.Value, existing.EndDate.Value, period.Start, period.End))
                        return true;
                }
            }
            return false;
        }

        private async Task<List<SchoolStudent>> LoadBillablePeriodsAsync(int masterStudentId, int schoolYearId)
        {
            return await _context.SchoolStudents
                .AsNoTracking()
                .Where(s => s.MasterStudentId == masterStudentId
                    && s.SchoolYearId == schoolYearId
                    && (s.IsLastVersion || s.IncludeInCouncilSummary))
                .ToListAsync();
        }

        private static List<ResolvedPeriod> CollapseDuplicatePeriods(List<ResolvedPeriod> periods)
        {
            var byKey = new Dictionary<(DateOnly, DateOnly, int?), ResolvedPeriod>();
            foreach (var period in periods)
                byKey[(period.Start, period.End, period.CouncilId)] = period;
            return byKey.Values.ToList();
        }

        private static bool FilePeriodsConflict(List<ResolvedPeriod> periods)
        {
            for (int i = 0; i < periods.Count; i++)
            {
                for (int j = i + 1; j < periods.Count; j++)
                {
                    if (DatesOverlap(periods[i].Start, periods[i].End, periods[j].Start, periods[j].End))
                        return true;
                }
            }
            return false;
        }

        private static string ClassifyAgainstExisting(SchoolStudent existing, ResolvedPeriod proposed, out bool blocked)
        {
            bool datesChanged = existing.StartDate != proposed.Start || existing.EndDate != proposed.End;
            bool councilChanged = existing.SendingCouncil != proposed.CouncilId;
            bool datesOverlap = existing.StartDate.HasValue && existing.EndDate.HasValue
                && DatesOverlap(existing.StartDate.Value, existing.EndDate.Value, proposed.Start, proposed.End);
            bool laterStartSameEnd = existing.EndDate == proposed.End
                && existing.StartDate.HasValue
                && proposed.Start > existing.StartDate.Value;

            blocked = false;
            if (councilChanged && !datesChanged)
                return StudentUploadPromptType.ReplaceCouncil;
            if (councilChanged && datesChanged && laterStartSameEnd)
                return StudentUploadPromptType.SameEndCouncilSplit;
            if (councilChanged && datesChanged && datesOverlap)
            {
                blocked = true;
                return StudentUploadPromptType.SplitCouncilBlocked;
            }
            if (councilChanged && datesChanged)
                return StudentUploadPromptType.SplitCouncil;
            return StudentUploadPromptType.VerifyDates;
        }

        private static List<StudentUploadPeriodDto> GetProposedPeriods(StudentUploadPendingItem item)
        {
            if (item.ProposedPeriods != null && item.ProposedPeriods.Count > 0)
                return item.ProposedPeriods;

            return new List<StudentUploadPeriodDto>
            {
                new()
                {
                    StartDate = item.ProposedStartDate,
                    EndDate = item.ProposedEndDate,
                    CouncilId = item.ProposedCouncilId,
                    CouncilName = item.ProposedCouncilName,
                    FirstName = item.FirstName,
                    LastName = item.LastName,
                    Gender = item.Gender,
                    ClassId = item.ClassId,
                    DisabilityCategory = item.DisabilityCategory,
                    Street = item.Street,
                    HouseNumber = item.HouseNumber,
                    City = item.City,
                    PostCode = item.PostCode
                }
            };
        }

        private StudentUploadPeriodDto ToPeriodDto(ResolvedPeriod period, Dictionary<int, string> councilNames)
        {
            return new StudentUploadPeriodDto
            {
                StartDate = period.Start,
                EndDate = period.End,
                CouncilId = period.CouncilId,
                CouncilName = FormatCouncilName(period.CouncilId, councilNames),
                FirstName = period.Row.FirstName,
                LastName = period.Row.LastName,
                Gender = ParseGender(period.Row.Gender),
                ClassId = period.ClassId,
                DisabilityCategory = string.IsNullOrWhiteSpace(period.Row.DisabilityCategory)
                    ? null
                    : int.Parse(period.Row.DisabilityCategory),
                Street = period.Row.Street ?? string.Empty,
                HouseNumber = period.Row.HouseNumber?.Trim() ?? string.Empty,
                City = period.Row.City,
                PostCode = period.Row.PostCode ?? string.Empty
            };
        }

        private async Task<int?> CreateAllPeriodsForNewStudentAsync(
            List<ResolvedPeriod> periods,
            int schoolYearId,
            string userId)
        {
            var ordered = periods.OrderBy(p => p.Start).ToList();
            var firstId = await CreateNewStudentAsync(ordered[0], schoolYearId, userId);
            if (!firstId.HasValue)
                return null;

            var createdIds = new List<int> { firstId.Value };
            int sourceId = firstId.Value;
            int? lastCreated = firstId;
            var created = await _context.SchoolStudents.FirstAsync(s => s.Id == firstId.Value);
            int? previousStatus = created.StatusId;

            for (int i = 1; i < ordered.Count; i++)
            {
                var dto = ToPeriodDto(ordered[i], new Dictionary<int, string>());
                var newId = await _studentService.CreateNewStudentVersionAsync(
                    sourceId,
                    newVersion => ApplyPeriodFields(newVersion, dto, isNew: false, previousStatus),
                    ParseUserId(userId));

                if (!newId.HasValue)
                    return lastCreated;

                createdIds.Add(newId.Value);
                lastCreated = newId;
                sourceId = newId.Value;
                previousStatus = ResolveUploadStatus(false, previousStatus, dto.StartDate, dto.EndDate);
            }

            var lastRow = await _context.SchoolStudents.FirstAsync(s => s.Id == (lastCreated ?? firstId.Value));
            await AssignCurrentPeriodIncludesAsync(lastRow.MasterStudentId, schoolYearId, createdIds);

            var lastVersion = await _context.SchoolStudents
                .FirstOrDefaultAsync(s => s.MasterStudentId == lastRow.MasterStudentId
                    && s.SchoolYearId == schoolYearId
                    && s.IsLastVersion);

            return lastVersion?.Id ?? lastCreated;
        }

        private static bool DatesOverlap(DateOnly existingStart, DateOnly existingEnd, DateOnly newStart, DateOnly newEnd)
        {
            return newStart <= existingEnd && newEnd >= existingStart;
        }

        private static string FormatCouncilName(int? councilId, Dictionary<int, string> councilNames)
        {
            if (!councilId.HasValue)
                return "ללא רשות";
            return councilNames.TryGetValue(councilId.Value, out var name) ? name : councilId.Value.ToString();
        }

        private static string FormatDate(DateOnly? date) =>
            date?.ToString("dd/MM/yyyy") ?? "—";

        private static string FormatDateRange(DateOnly? start, DateOnly? end) =>
            $"{FormatDate(start)}–{FormatDate(end)}";


        private (bool isValid, string? error) ValidateRowFormat(
            StudentFileRow row,
            bool validateIdChecksum = false,
            DateTime? schoolYearStartDate = null,
            DateTime? schoolYearEndDate = null)
        {
            // Validate ID number (9 digits)
            if (string.IsNullOrWhiteSpace(row.IdNumber) || row.IdNumber.Length != 9 || !row.IdNumber.All(char.IsDigit))
                return (false, "מספר תעודת זהות לא תקין");

            // Validate Israeli ID checksum if enabled
            if (validateIdChecksum && !IsValidIsraeliId(row.IdNumber))
                return (false, "מספר תעודת זהות לא תקין - ספרת ביקורת שגויה");

            // Validate first name
            if (string.IsNullOrWhiteSpace(row.FirstName))
                return (false, "שם פרטי חסר");

            // Validate last name
            if (string.IsNullOrWhiteSpace(row.LastName))
                return (false, "שם משפחה חסר");

            // ✅ Gender is optional - validate only if provided
            if (!string.IsNullOrWhiteSpace(row.Gender) && !new[] { "1", "2", "99", "זכר", "נקבה" }.Contains(row.Gender))
                return (false, "מין לא תקין");

            // Validate class
            if (string.IsNullOrWhiteSpace(row.Class))
                return (false, "כיתה חסרה");

            // Validate dates (expecting day-month-year format: DD/MM/YYYY)
           /* var hebrewCulture = CultureInfo.GetCultureInfo("he-IL");
            var culture = CultureInfo.InvariantCulture;
            if (!DateTime.TryParse(row.StartDate, out _))
                return (false, $"תאריך התחלה לא תקין: '{row.StartDate}'");

            if (!DateTime.TryParse(row.EndDate, out _))
                return (false, $"תאריך סיום לא תקין: '{row.EndDate}'");*/

            var dateParseculture = CultureInfo.GetCultureInfo("he-IL");
            if (!DateTime.TryParse(row.StartDate, dateParseculture, DateTimeStyles.None, out var parsedStart))
                return (false, $"תאריך התחלה לא תקין: '{row.StartDate}'");

            if (!DateTime.TryParse(row.EndDate, dateParseculture, DateTimeStyles.None, out var parsedEnd))
                return (false, $"תאריך סיום לא תקין: '{row.EndDate}'");

            // Validate that start date is not after end date
            if (parsedStart > parsedEnd)
                return (false, $"תאריך התחלה ({row.StartDate}) חייב להיות קטן מאו שווה לתאריך סיום ({row.EndDate})");

            // Validate both dates are in school year range
            if (schoolYearStartDate.HasValue && schoolYearEndDate.HasValue)
            {
                var schoolYearStart = schoolYearStartDate.Value.Date;
                var schoolYearEnd = schoolYearEndDate.Value.Date;

                if (parsedStart.Date < schoolYearStart || parsedStart.Date > schoolYearEnd)
                    return (false, $"תאריך התחלה ({row.StartDate}) חייב להיות בין {schoolYearStart:dd/MM/yyyy} ל-{schoolYearEnd:dd/MM/yyyy}");

                if (parsedEnd.Date < schoolYearStart || parsedEnd.Date > schoolYearEnd)
                    return (false, $"תאריך סיום ({row.EndDate}) חייב להיות בין {schoolYearStart:dd/MM/yyyy} ל-{schoolYearEnd:dd/MM/yyyy}");
            }

            // Validate disability category (integer or empty for none)
            if (!string.IsNullOrWhiteSpace(row.DisabilityCategory) && !int.TryParse(row.DisabilityCategory, out _))
                return (false, "קטגוריית נכות לא תקינה");

            // ✅ Address fields - only HouseNumber length validation if provided
            if (!string.IsNullOrWhiteSpace(row.HouseNumber) && row.HouseNumber.Trim().Length > 6)
                return (false, "מספר בית ארוך מדי (מקסימום 6 תווים)");

            // Validate city (required)
            if (string.IsNullOrWhiteSpace(row.City))
                return (false, "עיר חסרה");

            // ✅ Postcode is optional - no validation needed

            // Validate sending council (integer or 99999 for none)
            if (string.IsNullOrWhiteSpace(row.SendingCouncil))
                return (false, "רשות שולחת לא תקינה");

            return (true, null);
        }

        private bool HasOtherFieldsChanged(SchoolStudent existing, ResolvedPeriod period)
        {
            var row = period.Row;
            var rowGender = ParseGender(row.Gender);
            var rowDisabilityCategory = string.IsNullOrWhiteSpace(row.DisabilityCategory) ? null : (int?)int.Parse(row.DisabilityCategory);

            return existing.FirstName != row.FirstName ||
                   existing.LastName != row.LastName ||
                   existing.Gender != rowGender ||
                   existing.ClassId != period.ClassId ||
                   existing.DisabilityCategory != rowDisabilityCategory ||
                   existing.Street != (row.Street ?? string.Empty) ||
                   existing.HouseNumber != (row.HouseNumber?.Trim() ?? string.Empty) ||
                   existing.City != row.City ||
                   existing.PostCode != (row.PostCode ?? string.Empty);
        }

        private async Task<int?> CreateNewStudentAsync(
            ResolvedPeriod period,
            int schoolYearId,
            string userId)
        {
            var row = period.Row;
            var studentId = await _studentService.CreateNewStudentAsync(
                schoolYearId,
                row.IdNumber,
                student =>
                {
                    student.FirstName = row.FirstName;
                    student.LastName = row.LastName;
                    student.Gender = ParseGender(row.Gender);
                    student.ClassId = period.ClassId;
                    student.StartDate = period.Start;
                    student.EndDate = period.End;
                    student.DisabilityCategory = string.IsNullOrWhiteSpace(row.DisabilityCategory)
                        ? null
                        : (int?)int.Parse(row.DisabilityCategory);
                    student.Street = row.Street ?? string.Empty;
                    student.HouseNumber = row.HouseNumber?.Trim() ?? string.Empty;
                    student.City = row.City;
                    student.PostCode = row.PostCode ?? string.Empty;
                    student.SendingCouncil = period.CouncilId;
                    student.StatusId = ResolveUploadStatus(isNew: true, previousStatus: null, student.StartDate, student.EndDate);
                },
                ParseUserId(userId));

            if (!studentId.HasValue)
                _logger.LogError("❌ Failed to create student {IdNumber}", row.IdNumber);

            return studentId;
        }

        private async Task<int?> ResolveCouncilIdAsync(
                string councilValue,
                ProcessingResult result,
                string studentIdNumber)
        {
            if (string.IsNullOrWhiteSpace(councilValue) || councilValue == "99999")
                return null;

            // Try numeric ID first (backwards compatibility)
            if (int.TryParse(councilValue, out int numericId))
            {
                return numericId;
            }

            // Try as council name
            var councilId = await _globalFunctions.GetCouncilByName(councilValue);
            if (councilId != null)
            {
                _logger.LogInformation("Resolved council '{Name}' to ID {Id}",
                    councilValue, councilId);
                return councilId;
            }

            // Not found
            result.Errors.Add($"{studentIdNumber} - רשות שולחת '{councilValue}' לא נמצאה במערכת");
            return null;
        }

        private async Task UpdateStudentWithNewVersionAsync(
            SchoolStudent existing,
            ResolvedPeriod period,
            string userId)
        {
            var previousStatus = existing.StatusId;
            var dto = ToPeriodDto(period, new Dictionary<int, string>());
            var newVersionId = await _studentService.CreateNewStudentVersionAsync(
                existing.Id,
                newVersion => ApplyPeriodFields(newVersion, dto, isNew: false, previousStatus),
                ParseUserId(userId));

            if (!newVersionId.HasValue)
                _logger.LogError("❌ Failed to create new version for student {IdNumber}", existing.IdNumber);
        }

        private static int? ParseUserId(string? userId) =>
            int.TryParse(userId, out var id) ? id : null;

        private int? ParseGender(string? gender)
        {
            // ✅ Default to 99 (unknown) for null/empty values
            if (string.IsNullOrWhiteSpace(gender))
                return 99;

            return gender.ToUpper() switch
            {
                "1" => 1,
                "2" => 2,
                "99" => 99,
                "זכר" => 1,
                "נקבה" => 2,
                _ => 99 // Default unknown for unrecognized values
            };
        }

        /// <summary>
        /// Check if ID number validation is enabled via system attribute.
        /// </summary>
        private async Task<bool> ShouldValidateIdNumberAsync()
        {
            try
            {
                var attribute = await _context.SystemAttributes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Name == "validate_israeli_id_checksum");

                if (attribute != null && bool.TryParse(attribute.Value, out bool isEnabled))
                {
                    _logger.LogInformation("Israeli ID validation enabled: {IsEnabled}", isEnabled);
                    return isEnabled;
                }

                _logger.LogInformation("Israeli ID validation attribute not found, defaulting to false");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking ID validation setting, defaulting to false");
                return false;
            }
        }

        /// <summary>
        /// Validates Israeli ID number using checksum algorithm.
        /// Uses the Luhn-like algorithm for Israeli ID validation.
        /// </summary>
        private bool IsValidIsraeliId(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber) || idNumber.Length != 9 || !idNumber.All(char.IsDigit))
                return false;

            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                int digit = int.Parse(idNumber[i].ToString());
                
                // Multiply by 1 or 2 alternately (1 for even positions, 2 for odd positions)
                int multipliedValue = digit * ((i % 2) + 1);
                
                // If result is greater than 9, subtract 9
                if (multipliedValue > 9)
                    multipliedValue -= 9;
                
                sum += multipliedValue;
            }

            // Valid if sum is divisible by 10
            bool isValid = sum % 10 == 0;
            
            if (!isValid)
            {
                _logger.LogWarning("Invalid Israeli ID checksum for {IdNumber}, sum={Sum}", idNumber, sum);
            }
            
            return isValid;
        }
    }

    internal sealed class ResolvedPeriod
    {
        public required StudentFileRow Row { get; init; }
        public DateOnly Start { get; init; }
        public DateOnly End { get; init; }
        public int? CouncilId { get; init; }
        public int ClassId { get; init; }
    }

    /// <summary>
    /// Represents a student row from the uploaded file.
    /// </summary>
    public class StudentFileRow
    {
        public required string IdNumber { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public string? Gender { get; set; } // ✅ Optional - defaults to 99 (unknown)
        public required string Class { get; set; }
        public required string StartDate { get; set; }
        public required string EndDate { get; set; }
        public string? DisabilityCategory { get; set; }
        public string? Street { get; set; } // ✅ Optional
        public string? HouseNumber { get; set; } // ✅ Optional
        public required string City { get; set; }
        public string? PostCode { get; set; } // ✅ Optional
        public required string SendingCouncil { get; set; }
    }

    /// <summary>
    /// Processing result summary.
    /// </summary>
    public class ProcessingResult
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public List<string> Unchanged { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<StudentUploadPendingItem> Pending { get; set; } = new();

        public int TotalProcessed => Created + Updated + Unchanged.Count;
        public int TotalErrors => Errors.Count;
    }
}