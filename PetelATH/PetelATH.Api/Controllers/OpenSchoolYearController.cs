using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.DTOs;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OpenSchoolYearController : BaseController
    {
        private readonly AppDbContext _context;

        public OpenSchoolYearController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<OpenSchoolYearController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet("preview")]
        public async Task<IActionResult> Preview()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!TryGetCaller(session, out var caller, out var callerError))
                    return callerError!;

                var (targetYear, yearError) = await GetTargetYearAsync(session);
                if (yearError != null)
                    return yearError;

                var previous = await GetPreviousYearAsync(targetYear!.Id);
                var attributeTypesReady = await _context.SchoolAttributeTypes
                    .AsNoTracking()
                    .AnyAsync(t => t.YearId == targetYear.Id);

                var preview = new OpenSchoolYearPreviewDto
                {
                    Success = true,
                    TargetYearId = targetYear.Id,
                    TargetYearName = targetYear.HebrewYearText,
                    PreviousYearId = previous?.Id,
                    PreviousYearName = previous?.HebrewYearText,
                    AttributeTypesReady = attributeTypesReady
                };

                if (previous == null)
                {
                    preview.Message = "לא נמצאה שנת לימודים קודמת";
                    return Ok(preview);
                }

                if (!attributeTypesReady)
                {
                    preview.Message = "לשנה הנבחרת אין סוגי מאפיינים. יש להעתיק הגדרות מהשנה הקודמת במסך הגדרות שנת לימודים";
                }

                var schools = await GetScopedPreviousSchoolsAsync(previous.Id, caller.EntityId, caller.IsAdmin);
                if (schools.Count == 0)
                    return Ok(preview);

                var entityIds = schools.Select(s => s.EntityId).ToList();
                var openedIds = await GetOpenedSchoolIdsAsync(entityIds, targetYear.Id, targetYear.HebrewYearText);
                var sourceYearIds = schools.Select(s => s.SchoolYearId).Distinct().ToList();

                var classes = await _context.SchoolClasses
                    .AsNoTracking()
                    .Where(c => sourceYearIds.Contains(c.SchoolYearId))
                    .ToListAsync();

                var characterizationIds = classes
                    .Where(c => c.CharacterizationId.HasValue)
                    .Select(c => c.CharacterizationId!.Value)
                    .Distinct()
                    .ToList();

                var characterizationNames = characterizationIds.Count == 0
                    ? new Dictionary<int, string>()
                    : await _context.SpecialNeedsCharacterizations
                        .AsNoTracking()
                        .Where(c => characterizationIds.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id, c => c.Name);

                var classesByYear = classes
                    .GroupBy(c => c.SchoolYearId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                preview.Schools = schools.Select(school =>
                {
                    classesByYear.TryGetValue(school.SchoolYearId, out var schoolClasses);
                    return new OpenSchoolYearSchoolDto
                    {
                        SchoolEntityId = school.EntityId,
                        SourceSchoolYearId = school.SchoolYearId,
                        Name = school.Name,
                        Symbol = school.Symbol,
                        AlreadyOpened = openedIds.Contains(school.EntityId),
                        Classes = (schoolClasses ?? new List<SchoolClass>())
                            .OrderBy(c => c.Level)
                            .ThenBy(c => c.ClassNumber)
                            .Select(c => new OpenSchoolYearClassDto
                            {
                                Level = c.Level,
                                ClassNumber = c.ClassNumber,
                                EndHour = c.EndHour,
                                CharacterizationId = c.CharacterizationId,
                                CharacterizationName = c.CharacterizationId.HasValue
                                    && characterizationNames.TryGetValue(c.CharacterizationId.Value, out var name)
                                        ? name
                                        : null
                            })
                            .ToList()
                    };
                }).ToList();

                return Ok(preview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building open-school-year preview");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת בתי הספר לפתיחת שנה" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Open([FromBody] OpenSchoolYearRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!TryGetCaller(session, out var caller, out var callerError))
                    return callerError!;

                var (targetYear, yearError) = await GetTargetYearAsync(session);
                if (yearError != null)
                    return yearError;

                if (request.Schools == null || request.Schools.Count == 0)
                    return BadRequest(new { success = false, message = "יש לבחור לפחות בית ספר אחד" });

                var previous = await GetPreviousYearAsync(targetYear!.Id);
                if (previous == null)
                    return BadRequest(new { success = false, message = "לא נמצאה שנת לימודים קודמת" });

                var attributeTypesReady = await _context.SchoolAttributeTypes
                    .AsNoTracking()
                    .AnyAsync(t => t.YearId == targetYear.Id);
                if (!attributeTypesReady)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "לשנה הנבחרת אין סוגי מאפיינים. יש להעתיק הגדרות מהשנה הקודמת במסך הגדרות שנת לימודים"
                    });
                }

                var duplicateEntity = request.Schools
                    .GroupBy(s => s.SchoolEntityId)
                    .FirstOrDefault(g => g.Count() > 1);
                if (duplicateEntity != null)
                    return BadRequest(new { success = false, message = "בית ספר מופיע יותר מפעם אחת" });

                var scoped = await GetScopedPreviousSchoolsAsync(previous.Id, caller.EntityId, caller.IsAdmin);
                var scopedByEntity = scoped.ToDictionary(s => s.EntityId);

                foreach (var schoolRequest in request.Schools)
                {
                    if (!scopedByEntity.ContainsKey(schoolRequest.SchoolEntityId))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "בית ספר אינו ברשימת השנה הקודמת או שאינו בהרשאתך"
                        });
                    }
                }

                var requestedIds = request.Schools.Select(s => s.SchoolEntityId).ToList();
                var openedIds = await GetOpenedSchoolIdsAsync(requestedIds, targetYear.Id, targetYear.HebrewYearText);
                if (openedIds.Count > 0)
                {
                    var openedName = scopedByEntity[openedIds.First()].Name;
                    return BadRequest(new
                    {
                        success = false,
                        message = $"לבית הספר {openedName} כבר קיימת שנת לימודים זו"
                    });
                }

                var characterizationIds = request.Schools
                    .SelectMany(s => s.Classes ?? new List<OpenSchoolYearClassRequest>())
                    .Where(c => c.CharacterizationId.HasValue)
                    .Select(c => c.CharacterizationId!.Value)
                    .Distinct()
                    .ToList();

                if (characterizationIds.Count > 0)
                {
                    var existingCharacterizations = await _context.SpecialNeedsCharacterizations
                        .AsNoTracking()
                        .Where(c => characterizationIds.Contains(c.Id))
                        .Select(c => c.Id)
                        .ToListAsync();
                    if (existingCharacterizations.Count != characterizationIds.Count)
                        return BadRequest(new { success = false, message = "אפיון לא נמצא" });
                }

                var normalizedClasses = new Dictionary<int, List<NormalizedClass>>();
                foreach (var schoolRequest in request.Schools)
                {
                    var schoolName = scopedByEntity[schoolRequest.SchoolEntityId].Name;
                    var classes = new List<NormalizedClass>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var classRequest in schoolRequest.Classes ?? new List<OpenSchoolYearClassRequest>())
                    {
                        if (!TryNormalizeClass(classRequest, out var normalized, out var classError))
                            return BadRequest(new { success = false, message = $"{schoolName}: {classError}" });

                        var key = $"{normalized!.Level}\u001f{normalized.ClassNumber}";
                        if (!seen.Add(key))
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = $"כיתה {normalized.Level} {normalized.ClassNumber} כבר קיימת בבית הספר {schoolName}"
                            });
                        }

                        classes.Add(normalized);
                    }

                    normalizedClasses[schoolRequest.SchoolEntityId] = classes;
                }

                var sourceYearIds = request.Schools
                    .Select(s => scopedByEntity[s.SchoolEntityId].SchoolYearId)
                    .Distinct()
                    .ToList();

                var sourceYears = await _context.SchoolYears
                    .AsNoTracking()
                    .Where(sy => sourceYearIds.Contains(sy.Id))
                    .ToDictionaryAsync(sy => sy.Id);

                var now = DateTime.UtcNow;
                var skipped = new List<SkippedSchoolAttributeDto>();
                var schoolsCreated = 0;
                var classesCreated = 0;
                var attributesCopied = 0;

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var openedAgain = await _context.SchoolYears
                        .Where(sy => requestedIds.Contains(sy.SchoolId)
                            && (sy.YearId == targetYear.Id || sy.YearName == targetYear.HebrewYearText))
                        .Select(sy => sy.SchoolId)
                        .ToListAsync();
                    if (openedAgain.Count > 0)
                    {
                        await transaction.RollbackAsync();
                        var openedName = scopedByEntity[openedAgain[0]].Name;
                        return BadRequest(new
                        {
                            success = false,
                            message = $"לבית הספר {openedName} כבר קיימת שנת לימודים זו"
                        });
                    }

                    var newYears = new Dictionary<int, SchoolYear>();
                    foreach (var schoolRequest in request.Schools)
                    {
                        var sourceSchool = scopedByEntity[schoolRequest.SchoolEntityId];
                        sourceYears.TryGetValue(sourceSchool.SchoolYearId, out var sourceYear);

                        var newYear = new SchoolYear
                        {
                            SchoolId = sourceSchool.EntityId,
                            YearId = targetYear.Id,
                            YearName = targetYear.HebrewYearText,
                            IsCurrent = true,
                            Status = 1,
                            StartDate = ShiftYear(sourceYear?.StartDate ?? now),
                            EndDate = ShiftYear(sourceYear?.EndDate ?? now.AddYears(1)),
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _context.SchoolYears.Add(newYear);
                        newYears[sourceSchool.EntityId] = newYear;
                    }

                    await _context.SaveChangesAsync();

                    var attributePlan = await BuildAttributeCopiesAsync(
                        previous.Id,
                        targetYear.Id,
                        request.Schools.Select(s => scopedByEntity[s.SchoolEntityId]).ToList(),
                        newYears,
                        caller.UserId,
                        now,
                        skipped);

                    foreach (var schoolRequest in request.Schools)
                    {
                        var sourceSchool = scopedByEntity[schoolRequest.SchoolEntityId];
                        var newYear = newYears[sourceSchool.EntityId];

                        _context.Schools.Add(CopySchool(sourceSchool, newYear.Id));

                        foreach (var classItem in normalizedClasses[sourceSchool.EntityId])
                        {
                            _context.SchoolClasses.Add(new SchoolClass
                            {
                                SchoolYearId = newYear.Id,
                                Name = $"{classItem.Level} {classItem.ClassNumber}",
                                Level = classItem.Level,
                                ClassNumber = classItem.ClassNumber,
                                EndHour = classItem.EndHour,
                                CharacterizationId = classItem.CharacterizationId,
                                CreatedAt = now,
                                UpdatedAt = now
                            });
                            classesCreated++;
                        }

                        schoolsCreated++;
                    }

                    _context.SchoolAttributes.AddRange(attributePlan);
                    attributesCopied = attributePlan.Count;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                _logger.LogInformation(
                    "Opened school year {Year} for {SchoolCount} schools, {ClassCount} classes, {AttributeCount} attributes",
                    targetYear.HebrewYearText, schoolsCreated, classesCreated, attributesCopied);

                return Ok(new OpenSchoolYearResultDto
                {
                    Success = true,
                    Message = $"נפתחה שנת לימודים עבור {schoolsCreated} בתי ספר",
                    SchoolsCreated = schoolsCreated,
                    ClassesCreated = classesCreated,
                    AttributesCopied = attributesCopied,
                    SkippedAttributes = skipped
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening school year");
                return StatusCode(500, new { success = false, message = "שגיאה בפתיחת שנת הלימודים" });
            }
        }

        private async Task<List<SchoolAttribute>> BuildAttributeCopiesAsync(
            int sourceYearId,
            int targetYearId,
            List<School> sourceSchools,
            Dictionary<int, SchoolYear> newYears,
            int userId,
            DateTime now,
            List<SkippedSchoolAttributeDto> skipped)
        {
            var sourceTypes = await _context.SchoolAttributeTypes
                .AsNoTracking()
                .Where(t => t.YearId == sourceYearId)
                .ToListAsync();
            var targetTypes = await _context.SchoolAttributeTypes
                .AsNoTracking()
                .Where(t => t.YearId == targetYearId)
                .ToListAsync();

            var sourceTypeById = sourceTypes.ToDictionary(t => t.Id);
            var targetByName = targetTypes
                .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var sourceTypeIds = sourceTypes.Select(t => t.Id).ToList();
            var targetTypeIds = targetTypes.Select(t => t.Id).ToList();

            var sourceValues = sourceTypeIds.Count == 0
                ? new List<SchoolAttributeTypeValue>()
                : await _context.SchoolAttributeTypeValues
                    .AsNoTracking()
                    .Where(v => sourceTypeIds.Contains(v.SchoolAttributeTypeId))
                    .ToListAsync();
            var targetValues = targetTypeIds.Count == 0
                ? new List<SchoolAttributeTypeValue>()
                : await _context.SchoolAttributeTypeValues
                    .AsNoTracking()
                    .Where(v => targetTypeIds.Contains(v.SchoolAttributeTypeId))
                    .ToListAsync();

            var sourceValueById = sourceValues.ToDictionary(v => v.Id);
            var targetValuesByType = targetValues
                .GroupBy(v => v.SchoolAttributeTypeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var sourceYearIds = sourceSchools.Select(s => s.SchoolYearId).Distinct().ToList();
            var attributes = await _context.SchoolAttributes
                .AsNoTracking()
                .Where(a => sourceYearIds.Contains(a.SchoolYearId) && a.IsLastVersion)
                .ToListAsync();

            var latestByType = attributes
                .GroupBy(a => new { a.SchoolYearId, a.SchoolAttributeTypeId })
                .Select(g => g.OrderByDescending(a => a.Version).ThenByDescending(a => a.Id).First())
                .GroupBy(a => a.SchoolYearId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var copies = new List<SchoolAttribute>();
            foreach (var sourceSchool in sourceSchools)
            {
                if (!latestByType.TryGetValue(sourceSchool.SchoolYearId, out var schoolAttributes))
                    continue;

                var newYearId = newYears[sourceSchool.EntityId].Id;
                foreach (var attribute in schoolAttributes)
                {
                    if (!sourceTypeById.TryGetValue(attribute.SchoolAttributeTypeId, out var sourceType)
                        || !targetByName.TryGetValue(sourceType.Name, out var targetType))
                    {
                        skipped.Add(new SkippedSchoolAttributeDto
                        {
                            SchoolEntityId = sourceSchool.EntityId,
                            SchoolName = sourceSchool.Name,
                            AttributeName = sourceType?.HebrewName ?? sourceType?.Name ?? attribute.SchoolAttributeTypeId.ToString(),
                            Reason = "אין סוג מאפיין תואם בשנה החדשה"
                        });
                        continue;
                    }

                    var value = attribute.Value;
                    if (string.Equals(sourceType.AttributeValueType, "List", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(attribute.Value))
                    {
                        value = RemapListValue(
                            attribute.Value,
                            sourceSchool,
                            sourceType,
                            targetType,
                            sourceValueById,
                            targetValuesByType,
                            skipped);
                    }

                    copies.Add(new SchoolAttribute
                    {
                        SchoolYearId = newYearId,
                        SchoolAttributeTypeId = targetType.Id,
                        Value = value,
                        Version = 1,
                        IsLastVersion = true,
                        UserId = userId,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            return copies;
        }

        private static string? RemapListValue(
            string sourceValue,
            School sourceSchool,
            SchoolAttributeType sourceType,
            SchoolAttributeType targetType,
            Dictionary<int, SchoolAttributeTypeValue> sourceValueById,
            Dictionary<int, List<SchoolAttributeTypeValue>> targetValuesByType,
            List<SkippedSchoolAttributeDto> skipped)
        {
            var attributeName = sourceType.HebrewName ?? sourceType.Name;
            if (!int.TryParse(sourceValue, out var optionId)
                || !sourceValueById.TryGetValue(optionId, out var sourceOption))
            {
                skipped.Add(new SkippedSchoolAttributeDto
                {
                    SchoolEntityId = sourceSchool.EntityId,
                    SchoolName = sourceSchool.Name,
                    AttributeName = attributeName,
                    Reason = "אין ערך רשימה תואם"
                });
                return null;
            }

            targetValuesByType.TryGetValue(targetType.Id, out var options);
            var match = options?.FirstOrDefault(o => o.IsValid && o.Value == sourceOption.Value)
                ?? options?.FirstOrDefault(o => o.Value == sourceOption.Value);

            if (match == null)
            {
                skipped.Add(new SkippedSchoolAttributeDto
                {
                    SchoolEntityId = sourceSchool.EntityId,
                    SchoolName = sourceSchool.Name,
                    AttributeName = attributeName,
                    Reason = "אין ערך רשימה תואם"
                });
                return null;
            }

            return match.Id.ToString();
        }

        private async Task<List<School>> GetScopedPreviousSchoolsAsync(int previousYearId, int sessionEntityId, bool isAdmin)
        {
            var schoolYearIds = await _context.SchoolYears
                .AsNoTracking()
                .Where(sy => sy.YearId == previousYearId)
                .Select(sy => sy.Id)
                .ToListAsync();

            if (schoolYearIds.Count == 0)
                return new List<School>();

            var ownedEntityIds = await _context.Entities
                .AsNoTracking()
                .Where(e => e.OwnerId == sessionEntityId)
                .Select(e => e.Id)
                .ToListAsync();

            var schools = await _context.Schools
                .AsNoTracking()
                .Where(s => schoolYearIds.Contains(s.SchoolYearId)
                    && s.IsLastVersion
                    && s.IsActive
                    && (isAdmin
                        || s.Owner == sessionEntityId
                        || (s.Owner.HasValue && ownedEntityIds.Contains(s.Owner.Value))))
                .ToListAsync();

            return schools
                .GroupBy(s => s.EntityId)
                .Select(g => g.OrderByDescending(s => s.Version).First())
                .OrderBy(s => s.Name)
                .ToList();
        }

        private async Task<HashSet<int>> GetOpenedSchoolIdsAsync(List<int> entityIds, int targetYearId, string targetYearName)
        {
            if (entityIds.Count == 0)
                return new HashSet<int>();

            var ids = await _context.SchoolYears
                .AsNoTracking()
                .Where(sy => entityIds.Contains(sy.SchoolId)
                    && (sy.YearId == targetYearId || sy.YearName == targetYearName))
                .Select(sy => sy.SchoolId)
                .ToListAsync();

            return ids.ToHashSet();
        }

        private async Task<HebrewYear?> GetPreviousYearAsync(int yearId)
        {
            return await _context.HebrewYears
                .AsNoTracking()
                .Where(y => y.Id < yearId)
                .OrderByDescending(y => y.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<(HebrewYear? Year, IActionResult? Error)> GetTargetYearAsync(UserSession session)
        {
            var selectedYearId = session.GetProperty("SelectedYearId");
            if (string.IsNullOrEmpty(selectedYearId) || !int.TryParse(selectedYearId, out var yearId))
                return (null, BadRequest(new { success = false, message = "לא נבחרה שנת לימודים" }));

            var year = await _context.HebrewYears.AsNoTracking().FirstOrDefaultAsync(y => y.Id == yearId);
            if (year == null)
                return (null, BadRequest(new { success = false, message = "שנת לימודים לא נמצאה" }));

            return (year, null);
        }

        private bool TryGetCaller(UserSession session, out Caller caller, out IActionResult? error)
        {
            caller = default;
            error = null;

            if (!int.TryParse(session.EntityId, out var entityId))
            {
                error = BadRequest(new { success = false, message = "Invalid session entity ID" });
                return false;
            }

            if (!int.TryParse(session.UserId, out var userId))
            {
                error = BadRequest(new { success = false, message = "Invalid user ID" });
                return false;
            }

            caller = new Caller(entityId, userId, session.UserId == "1");
            return true;
        }

        private static School CopySchool(School source, int schoolYearId)
        {
            return new School
            {
                EntityId = source.EntityId,
                SchoolYearId = schoolYearId,
                Version = 1,
                EntityTypeId = source.EntityTypeId,
                Name = source.Name,
                Street = source.Street,
                HouseNumber = source.HouseNumber,
                City = source.City,
                PostCode = source.PostCode,
                Council = source.Council,
                Phone = source.Phone,
                Email = source.Email,
                Principal = source.Principal,
                Inspector = source.Inspector,
                ContactPerson = source.ContactPerson,
                ApiConnectionId = source.ApiConnectionId,
                IsActive = source.IsActive,
                SchoolLogo = source.SchoolLogo?.ToArray(),
                Owner = source.Owner,
                CharacterizationId = source.CharacterizationId,
                EducationStage = source.EducationStage,
                Symbol = source.Symbol,
                IsLastVersion = true
            };
        }

        private static bool TryNormalizeClass(
            OpenSchoolYearClassRequest request,
            out NormalizedClass? normalized,
            out string? error)
        {
            normalized = null;
            error = null;

            if (string.IsNullOrWhiteSpace(request.Level) || string.IsNullOrWhiteSpace(request.ClassNumber))
            {
                error = "שדות חובה חסרים בכיתה";
                return false;
            }

            var level = NormalizeText(request.Level);
            var classNumber = NormalizeText(request.ClassNumber);
            if (level.Length > 3 || classNumber.Length > 3)
            {
                error = "שכבה ומספר כיתה מוגבלים ל-3 תווים";
                return false;
            }

            var name = $"{level} {classNumber}";
            if (name.Length > 6)
            {
                error = "שם הכיתה ארוך מדי";
                return false;
            }

            normalized = new NormalizedClass(level, classNumber, request.EndHour, request.CharacterizationId);
            return true;
        }

        private static string NormalizeText(string value)
        {
            return value.Trim().Replace("\"", "״").Replace("'", "׳");
        }

        private static DateTime ShiftYear(DateTime value)
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            return utc.AddYears(1);
        }

        private readonly record struct Caller(int EntityId, int UserId, bool IsAdmin);

        private sealed record NormalizedClass(string Level, string ClassNumber, TimeOnly? EndHour, int? CharacterizationId);
    }
}
