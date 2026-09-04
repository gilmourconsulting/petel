using Microsoft.EntityFrameworkCore;
using Petel.Core.Abstractions;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class EntitlementService
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly IAttributeCache _attributeCache;
        private readonly ILogger<EntitlementService> _logger;

        public EntitlementService(
            AssistDbContext context,
            SharedDbContext sharedContext,
            IAttributeCache attributeCache,
            ILogger<EntitlementService> logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _attributeCache = attributeCache;
            _logger = logger;
        }

        /// <summary>
        /// Returns last-version entitlements for the given year. kind is optional; omit or pass null/empty to return all.
        /// </summary>
        public async Task<List<EntitlementListItemDto>> ListEntitlementsAsync(int entityId, int yearId, string? kind)
        {
            var items = await _context.Entitlements
                .AsNoTracking()
                .Where(e => e.HebrewYearId == yearId && e.IsLastVersion)
                .OrderByDescending(e => e.Id)
                .ToListAsync();

            var allocatedHoursMap = await BuildAllocatedHoursByMasterAsync(items);

            return await MapListAsync(items, allocatedHoursMap);
        }

        /// <summary>
        /// Last-version entitlements whose date range overlaps [from, to] (inclusive).
        /// </summary>
        public async Task<List<EntitlementListItemDto>> ListEntitlementsOverlappingAsync(DateOnly from, DateOnly to)
        {
            var items = await _context.Entitlements
                .AsNoTracking()
                .Where(e => e.IsLastVersion && e.StartDate <= to && e.EndDate >= from)
                .OrderByDescending(e => e.Id)
                .ToListAsync();

            var allocatedHoursMap = await BuildAllocatedHoursByMasterAsync(items);
            return await MapListAsync(items, allocatedHoursMap);
        }

        public async Task<List<EntitlementAllocationDto>> ListAllocationsAsync(int entitlementId)
        {
            var entitlement = await _context.Entitlements.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == entitlementId)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            var versionIds = await GetVersionIdsForMasterAsync(entitlement.MasterEntitlementId);

            var allocations = await _context.EntitlementAllocations
                .AsNoTracking()
                .Where(a => versionIds.Contains(a.EntitlementId))
                .OrderByDescending(a => a.Id)
                .ToListAsync();

            if (allocations.Count == 0)
                return new List<EntitlementAllocationDto>();

            var personIds = allocations.Select(a => a.PersonId).Distinct().ToList();

            var nameMap = await _context.PersonDetails
                .AsNoTracking()
                .Where(pd => pd.IsLastVersion && personIds.Contains(pd.PersonId))
                .Select(pd => new { pd.PersonId, pd.FirstName, pd.LastName })
                .ToDictionaryAsync(pd => pd.PersonId, pd => $"{pd.FirstName} {pd.LastName}".Trim());

            return allocations.Select(a => new EntitlementAllocationDto
            {
                Id             = a.Id,
                EntitlementId  = a.EntitlementId,
                PersonId       = a.PersonId,
                PersonFullName = nameMap.TryGetValue(a.PersonId, out var n) ? n : string.Empty,
                StartDate      = a.StartDate,
                EndDate        = a.EndDate,
                Hours          = a.Hours,
                HoursUnit      = a.HoursUnit,
                IsActive       = a.IsActive
            }).ToList();
        }

        /// <summary>
        /// Person IDs that have at least one active allocation on an entitlement for the given Hebrew year.
        /// </summary>
        public async Task<HashSet<int>> GetAllocatedPersonIdsAsync(int yearId)
        {
            var ids = await (
                from a in _context.EntitlementAllocations.AsNoTracking()
                join e in _context.Entitlements.AsNoTracking() on a.EntitlementId equals e.Id
                where a.IsActive && e.HebrewYearId == yearId
                select a.PersonId
            ).Distinct().ToListAsync();

            return ids.ToHashSet();
        }

        public async Task<List<EntitlementAllocationDto>> ListAllocationsByPersonAsync(int personId, int? yearId = null)
        {
            var query =
                from a in _context.EntitlementAllocations.AsNoTracking()
                join e in _context.Entitlements.AsNoTracking() on a.EntitlementId equals e.Id
                where a.PersonId == personId
                select new { Allocation = a, Entitlement = e };

            if (yearId is > 0)
                query = query.Where(r => r.Entitlement.HebrewYearId == yearId.Value);

            var rows = await query.OrderByDescending(r => r.Allocation.Id).ToListAsync();

            if (rows.Count == 0)
                return new List<EntitlementAllocationDto>();

            var personName = await _context.PersonDetails
                .AsNoTracking()
                .Where(pd => pd.IsLastVersion && pd.PersonId == personId)
                .Select(pd => $"{pd.FirstName} {pd.LastName}".Trim())
                .FirstOrDefaultAsync() ?? string.Empty;

            var assistantTypeIds = rows.Select(r => r.Entitlement.AssistantTypeId).Distinct().ToList();
            var institutionIds = rows.Where(r => r.Entitlement.InstitutionId.HasValue)
                                .Select(r => r.Entitlement.InstitutionId!.Value).Distinct().ToList();
            var hebrewYearIds = rows.Select(r => r.Entitlement.HebrewYearId).Distinct().ToList();

            var assistantTypes = await _sharedContext.AssistantTypes
                .AsNoTracking()
                .Where(at => assistantTypeIds.Contains(at.Id))
                .ToDictionaryAsync(at => at.Id, at => at.DisplayName);

            var schools = institutionIds.Count == 0
                ? new Dictionary<int, string>()
                : await _context.Institutions
                    .AsNoTracking()
                    .Where(e => institutionIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, e => e.Name);

            var hebrewYears = await _sharedContext.HebrewYears
                .AsNoTracking()
                .Where(y => hebrewYearIds.Contains(y.Id))
                .ToDictionaryAsync(y => y.Id, y => y.YearName);

            return rows.Select(r =>
            {
                schools.TryGetValue(r.Entitlement.InstitutionId ?? 0, out var schoolName);
                assistantTypes.TryGetValue(r.Entitlement.AssistantTypeId, out var typeName);
                hebrewYears.TryGetValue(r.Entitlement.HebrewYearId, out var yearName);

                return new EntitlementAllocationDto
                {
                    Id                = r.Allocation.Id,
                    EntitlementId     = r.Allocation.EntitlementId,
                    PersonId          = r.Allocation.PersonId,
                    PersonFullName    = personName,
                    HebrewYearId      = r.Entitlement.HebrewYearId,
                    HebrewYearName    = yearName,
                    StartDate         = r.Allocation.StartDate,
                    EndDate           = r.Allocation.EndDate,
                    Hours             = r.Allocation.Hours,
                    HoursUnit         = r.Allocation.HoursUnit,
                    IsActive          = r.Allocation.IsActive,
                    AssistantTypeName = typeName,
                    SchoolName        = r.Entitlement.InstitutionId.HasValue ? schoolName : null
                };
            }).ToList();
        }

        public async Task<int> CreateAllocationAsync(int entityId, int? userId, int entitlementId, CreateEntitlementAllocationRequest request)
        {
            var entitlement = await _context.Entitlements.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == entitlementId)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            if (!entitlement.IsLastVersion || entitlement.IsCancelled)
                throw new InvalidOperationException("ניתן להקצות רק לזכאות פעילה בגרסה האחרונה");

            if (!entitlement.IsValid)
                throw new InvalidOperationException("לא ניתן להקצות לזכאות שאינה תקינה");

            var personExists = await _context.Persons.AsNoTracking()
                .AnyAsync(p => p.Id == request.PersonId);
            if (!personExists)
                throw new InvalidOperationException("אדם לא נמצא ברשות זו");

            ValidateHoursUnit(request.HoursUnit);

            if (request.Hours <= 0)
                throw new InvalidOperationException("מספר שעות חייב להיות גדול מאפס");

            var startDate = request.StartDate ?? entitlement.StartDate;
            var endDate   = request.EndDate   ?? entitlement.EndDate;

            if (startDate > endDate)
                throw new InvalidOperationException("תאריך התחלה חייב להיות לפני תאריך סיום");

            var now = DateTime.UtcNow;
            var allocation = new EntitlementAllocation
            {
                EntityId      = entityId,
                EntitlementId = entitlementId,
                PersonId      = request.PersonId,
                StartDate     = startDate,
                EndDate       = endDate,
                Hours         = request.Hours,
                HoursUnit     = request.HoursUnit,
                IsActive      = true,
                UserId        = userId,
                UpdateUser    = userId,
                CreatedAt     = now,
                UpdatedAt     = now
            };

            _context.EntitlementAllocations.Add(allocation);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created allocation {Id} for entitlement {EntitlementId}", allocation.Id, entitlementId);
            return allocation.Id;
        }

        public async Task DeactivateAllocationAsync(int? userId, int allocationId)
        {
            var allocation = await _context.EntitlementAllocations.FirstOrDefaultAsync(a => a.Id == allocationId)
                ?? throw new InvalidOperationException("הקצאה לא נמצאה");

            allocation.IsActive   = false;
            allocation.UpdateUser = userId;
            allocation.UpdatedAt  = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<EntitlementListItemDto?> GetEntitlementAsync(int id)
        {
            var item = await _context.Entitlements
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (item == null)
                return null;

            var allocatedHoursMap = await BuildAllocatedHoursByMasterAsync(new List<Entitlement> { item });
            var list = await MapListAsync(new List<Entitlement> { item }, allocatedHoursMap);
            return list.FirstOrDefault();
        }

        public async Task<List<EntitlementListItemDto>> GetHistoryAsync(int masterEntitlementId)
        {
            var items = await _context.Entitlements
                .AsNoTracking()
                .Where(e => e.MasterEntitlementId == masterEntitlementId)
                .OrderBy(e => e.Version)
                .ToListAsync();

            if (items.Count == 0)
                return new List<EntitlementListItemDto>();

            var allocatedHoursMap = await BuildAllocatedHoursByMasterAsync(items);
            return await MapListAsync(items, allocatedHoursMap);
        }

        public async Task<int> CreateEntitlementAsync(
            int entityId,
            int? userId,
            CreateEntitlementRequest request,
            EntitlementUploadValidity? uploadValidity = null)
        {
            ValidateHoursUnit(request.HoursUnit);

            var assistantType = await LoadAssistantTypeAsync(request.AssistantTypeId);
            var year = await LoadHebrewYearAsync(request.HebrewYearId);

            var startDate = request.StartDate ?? year.StartDate
                ?? throw new InvalidOperationException("תאריך התחלה נדרש — הגדר תאריכים לשנה העברית");
            var endDate = request.EndDate ?? year.EndDate
                ?? throw new InvalidOperationException("תאריך סיום נדרש — הגדר תאריכים לשנה העברית");

            ValidateDates(startDate, endDate, year);

            bool isPersonal = IsPersonalLevel(assistantType.Level);
            bool allowInvalid = uploadValidity != null;
            ValidateKindFields(
                isPersonal,
                request.InstitutionId,
                request.PupilIdNumber,
                request.PupilFirstName,
                request.PupilLastName,
                allowInvalid);

            var className = NormalizeOptionalText(request.ClassName);
            if (IsClassHelp(assistantType.Name) && string.IsNullOrWhiteSpace(className))
                throw new InvalidOperationException("שם כיתה נדרש לזכאות מסוג סייעת כיתתית");

            string? pupilIdNumber = null;
            if (isPersonal)
            {
                var (normalized, hardError, isInvalid) = EvaluatePupilId(request.PupilIdNumber);
                if (hardError != null)
                    throw new InvalidOperationException(hardError);
                if (isInvalid && !allowInvalid)
                    throw new InvalidOperationException("מספר תעודת זהות לא תקין — ספרת ביקורת שגויה");
                pupilIdNumber = normalized;
            }

            if (request.InstitutionId.HasValue)
                await ValidateInstitutionBelongsToTenantAsync(request.InstitutionId.Value);
            else if (!allowInvalid)
                throw new InvalidOperationException("יש לבחור בית ספר או גן");

            await ValidateClassClassificationAsync(request.ClassClassificationId);

            if (request.Hours <= 0)
                throw new InvalidOperationException("מספר שעות חייב להיות גדול מאפס");

            if (request.MinistryParticipationPct < 0 || request.MinistryParticipationPct > 100)
                throw new InvalidOperationException("אחוז השתתפות משרד החינוך חייב להיות בין 0 ל-100");

            await ValidateNoOverlapAsync(
                excludeMasterId: null,
                assistantType,
                startDate,
                endDate,
                request.InstitutionId,
                className,
                pupilIdNumber);

            var now = DateTime.UtcNow;
            var entitlement = new Entitlement
            {
                EntityId                 = entityId,
                HebrewYearId             = request.HebrewYearId,
                AssistantTypeId          = request.AssistantTypeId,
                StartDate                = startDate,
                EndDate                  = endDate,
                Hours                    = request.Hours,
                HoursUnit                = request.HoursUnit,
                MinistryParticipationPct = request.MinistryParticipationPct,
                InstitutionId            = request.InstitutionId,
                ClassName                = className,
                ClassClassificationId    = request.ClassClassificationId,
                PupilIdNumber            = pupilIdNumber,
                PupilFirstName           = isPersonal ? NormalizeRequiredText(request.PupilFirstName, "שם פרטי תלמיד") : null,
                PupilLastName            = isPersonal ? NormalizeRequiredText(request.PupilLastName, "שם משפחה תלמיד") : null,
                MasterEntitlementId      = 0,
                Version                  = 1,
                IsLastVersion            = true,
                IsCancelled              = false,
                IsActive                 = true,
                IsValid                  = uploadValidity?.IsValid ?? true,
                InvalidReasons           = uploadValidity?.ReasonsCsv,
                SourceInstitutionSymbol  = uploadValidity?.SourceInstitutionSymbol,
                SourceSupportCode        = uploadValidity?.SourceSupportCode,
                UserId                   = userId,
                UpdateUser               = userId,
                CreatedAt                = now,
                UpdatedAt                = now
            };

            _context.Entitlements.Add(entitlement);
            await _context.SaveChangesAsync();

            entitlement.MasterEntitlementId = entitlement.Id;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created entitlement {Id} for entity {EntityId}", entitlement.Id, entityId);
            return entitlement.Id;
        }

        public async Task UpdateEntitlementAsync(int entityId, int? userId, int id, UpdateEntitlementRequest request)
        {
            var entitlement = await _context.Entitlements.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            if (!entitlement.IsLastVersion)
                throw new InvalidOperationException("ניתן לערוך רק את הגרסה האחרונה של הזכאות");

            if (entitlement.IsCancelled)
                throw new InvalidOperationException("לא ניתן לערוך זכאות שבוטלה");

            // Repair rows created before master backfill / failed post-insert master assignment.
            if (entitlement.MasterEntitlementId <= 0)
                entitlement.MasterEntitlementId = entitlement.Id;

            // Existing entitlements may reference a type that was later deactivated.
            var assistantType = await LoadAssistantTypeAsync(entitlement.AssistantTypeId, requireActive: false);
            var year = await LoadHebrewYearAsync(entitlement.HebrewYearId, requireActive: false);

            if (request.MinistryParticipationPct < 0 || request.MinistryParticipationPct > 100)
                throw new InvalidOperationException("אחוז השתתפות משרד החינוך חייב להיות בין 0 ל-100");

            await ValidateClassClassificationAsync(request.ClassClassificationId);

            bool isPersonal = IsPersonalLevel(assistantType.Level);
            string? pupilFirstName = entitlement.PupilFirstName;
            string? pupilLastName = entitlement.PupilLastName;

            if (isPersonal)
            {
                pupilFirstName = NormalizeRequiredText(request.PupilFirstName, "שם פרטי תלמיד");
                pupilLastName = NormalizeRequiredText(request.PupilLastName, "שם משפחה תלמיד");
            }

            var datesChanged =
                entitlement.StartDate != request.StartDate ||
                entitlement.EndDate != request.EndDate;

            var changed =
                datesChanged ||
                entitlement.MinistryParticipationPct != request.MinistryParticipationPct ||
                entitlement.ClassClassificationId != request.ClassClassificationId ||
                (isPersonal && (
                    !string.Equals(entitlement.PupilFirstName, pupilFirstName, StringComparison.Ordinal) ||
                    !string.Equals(entitlement.PupilLastName, pupilLastName, StringComparison.Ordinal)));

            if (!changed)
                return;

            // Only re-validate year bounds when dates actually change (legacy rows may predate tighter year ranges).
            if (datesChanged)
                ValidateDates(request.StartDate, request.EndDate, year);

            await ValidateNoOverlapAsync(
                excludeMasterId: entitlement.MasterEntitlementId,
                assistantType,
                request.StartDate,
                request.EndDate,
                entitlement.InstitutionId,
                entitlement.ClassName,
                entitlement.PupilIdNumber);

            await CreateNewEntitlementVersionAsync(entitlement, userId, newVersion =>
            {
                newVersion.StartDate = request.StartDate;
                newVersion.EndDate = request.EndDate;
                newVersion.MinistryParticipationPct = request.MinistryParticipationPct;
                newVersion.ClassClassificationId = request.ClassClassificationId;
                if (isPersonal)
                {
                    newVersion.PupilFirstName = pupilFirstName;
                    newVersion.PupilLastName = pupilLastName;
                }
            });
        }

        /// <summary>
        /// Cancel creates a new version with is_cancelled=true; prior versions are preserved.
        /// </summary>
        public async Task DeactivateEntitlementAsync(int? userId, int id)
        {
            var entitlement = await _context.Entitlements.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            if (!entitlement.IsLastVersion)
                throw new InvalidOperationException("ניתן לבטל רק את הגרסה האחרונה של הזכאות");

            if (entitlement.IsCancelled)
                throw new InvalidOperationException("הזכאות כבר בוטלה");

            await CreateNewEntitlementVersionAsync(entitlement, userId, newVersion =>
            {
                newVersion.IsCancelled = true;
                newVersion.IsActive = false;
            });
        }

        /// <summary>
        /// Upload-driven version update — may change hours/classification/participation
        /// (fields immutable in the manual edit UI).
        /// </summary>
        public async Task ApplyUploadVersionAsync(
            int? userId,
            int entitlementId,
            decimal hours,
            string hoursUnit,
            decimal ministryParticipationPct,
            int? classClassificationId,
            int? institutionId = null,
            EntitlementUploadValidity? uploadValidity = null)
        {
            ValidateHoursUnit(hoursUnit);

            if (hours <= 0)
                throw new InvalidOperationException("מספר שעות חייב להיות גדול מאפס");

            if (ministryParticipationPct < 0 || ministryParticipationPct > 100)
                throw new InvalidOperationException("אחוז השתתפות משרד החינוך חייב להיות בין 0 ל-100");

            var entitlement = await _context.Entitlements.FirstOrDefaultAsync(e => e.Id == entitlementId)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            if (!entitlement.IsLastVersion)
                throw new InvalidOperationException("ניתן לערוך רק את הגרסה האחרונה של הזכאות");

            if (entitlement.IsCancelled)
                throw new InvalidOperationException("לא ניתן לערוך זכאות שבוטלה");

            if (entitlement.MasterEntitlementId <= 0)
                entitlement.MasterEntitlementId = entitlement.Id;

            if (institutionId.HasValue)
                await ValidateInstitutionBelongsToTenantAsync(institutionId.Value);

            await ValidateClassClassificationAsync(classClassificationId);

            await CreateNewEntitlementVersionAsync(entitlement, userId, newVersion =>
            {
                newVersion.Hours = hours;
                newVersion.HoursUnit = hoursUnit;
                newVersion.MinistryParticipationPct = ministryParticipationPct;
                newVersion.ClassClassificationId = classClassificationId;
                if (uploadValidity != null)
                {
                    newVersion.InstitutionId = institutionId;
                    ApplyUploadValidity(newVersion, uploadValidity);
                }
            });
        }

        /// <summary>
        /// Personal upload-driven version update — may change hours/institution/dates/names/participation
        /// (fields immutable or restricted in the manual edit UI).
        /// </summary>
        public async Task ApplyPersonalUploadVersionAsync(
            int? userId,
            int entitlementId,
            int? institutionId,
            decimal hours,
            string hoursUnit,
            decimal ministryParticipationPct,
            DateOnly startDate,
            DateOnly endDate,
            string pupilFirstName,
            string pupilLastName,
            EntitlementUploadValidity? uploadValidity = null)
        {
            ValidateHoursUnit(hoursUnit);

            if (hours <= 0)
                throw new InvalidOperationException("מספר שעות חייב להיות גדול מאפס");

            if (ministryParticipationPct < 0 || ministryParticipationPct > 100)
                throw new InvalidOperationException("אחוז השתתפות משרד החינוך חייב להיות בין 0 ל-100");

            var entitlement = await _context.Entitlements.FirstOrDefaultAsync(e => e.Id == entitlementId)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            if (!entitlement.IsLastVersion)
                throw new InvalidOperationException("ניתן לערוך רק את הגרסה האחרונה של הזכאות");

            if (entitlement.IsCancelled)
                throw new InvalidOperationException("לא ניתן לערוך זכאות שבוטלה");

            if (entitlement.MasterEntitlementId <= 0)
                entitlement.MasterEntitlementId = entitlement.Id;

            var assistantType = await LoadAssistantTypeAsync(entitlement.AssistantTypeId, requireActive: false);
            if (!IsPersonalLevel(assistantType.Level))
                throw new InvalidOperationException("גרסת העלאה אישית מותרת רק לזכאות אישית");

            var year = await LoadHebrewYearAsync(entitlement.HebrewYearId, requireActive: false);
            ValidateDates(startDate, endDate, year);

            if (institutionId.HasValue)
                await ValidateInstitutionBelongsToTenantAsync(institutionId.Value);

            var firstName = NormalizeRequiredText(pupilFirstName, "שם פרטי תלמיד");
            var lastName = NormalizeRequiredText(pupilLastName, "שם משפחה תלמיד");

            await ValidateNoOverlapAsync(
                excludeMasterId: entitlement.MasterEntitlementId,
                assistantType,
                startDate,
                endDate,
                institutionId,
                entitlement.ClassName,
                entitlement.PupilIdNumber);

            await CreateNewEntitlementVersionAsync(entitlement, userId, newVersion =>
            {
                newVersion.InstitutionId = institutionId;
                newVersion.Hours = hours;
                newVersion.HoursUnit = hoursUnit;
                newVersion.MinistryParticipationPct = ministryParticipationPct;
                newVersion.StartDate = startDate;
                newVersion.EndDate = endDate;
                newVersion.PupilFirstName = firstName;
                newVersion.PupilLastName = lastName;
                if (uploadValidity != null)
                    ApplyUploadValidity(newVersion, uploadValidity);
            });
        }

        public async Task ResolveValidityAsync(int? userId, int id, ResolveEntitlementValidityRequest request)
        {
            var reason = request.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("יש להזין סיבת אישור");

            var entitlement = await _context.Entitlements.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            if (!entitlement.IsLastVersion)
                throw new InvalidOperationException("ניתן לטפל רק בגרסה האחרונה של הזכאות");

            if (entitlement.IsCancelled)
                throw new InvalidOperationException("לא ניתן לטפל בזכאות שבוטלה");

            if (entitlement.IsValid)
                throw new InvalidOperationException("הזכאות כבר תקינה");

            if (entitlement.MasterEntitlementId <= 0)
                entitlement.MasterEntitlementId = entitlement.Id;

            var remaining = EntitlementInvalidReasons.Split(entitlement.InvalidReasons);
            var assistantType = await LoadAssistantTypeAsync(entitlement.AssistantTypeId, requireActive: false);
            bool isPersonal = IsPersonalLevel(assistantType.Level);

            string? pupilIdNumber = entitlement.PupilIdNumber;
            int? institutionId = entitlement.InstitutionId;

            if (remaining.Contains(EntitlementInvalidReasons.InvalidPupilId))
            {
                var candidate = !string.IsNullOrWhiteSpace(request.PupilIdNumber)
                    ? request.PupilIdNumber
                    : entitlement.PupilIdNumber;
                var (normalized, hardError, isInvalid) = EvaluatePupilId(candidate);
                if (hardError != null)
                    throw new InvalidOperationException(hardError);

                if (!isInvalid)
                {
                    pupilIdNumber = normalized;
                    remaining.Remove(EntitlementInvalidReasons.InvalidPupilId);
                }
                else if (request.ApproveInvalidPupilId && normalized?.Length == 9)
                {
                    pupilIdNumber = normalized;
                    remaining.Remove(EntitlementInvalidReasons.InvalidPupilId);
                }
                else
                    throw new InvalidOperationException("תעודת זהות עדיין אינה תקינה — תקנו אותה או אשרו עם 9 ספרות");
            }

            if (remaining.Contains(EntitlementInvalidReasons.InvalidSupportCode))
            {
                if (!request.ApproveSupportCode)
                    throw new InvalidOperationException("יש לאשר את קוד תומכת החינוך");
                remaining.Remove(EntitlementInvalidReasons.InvalidSupportCode);
            }

            if (remaining.Contains(EntitlementInvalidReasons.MissingInstitution))
            {
                if (!request.InstitutionId.HasValue || request.InstitutionId.Value <= 0)
                    throw new InvalidOperationException("יש לבחור מוסד");

                await ValidateInstitutionBelongsToTenantAsync(request.InstitutionId.Value);

                if (!isPersonal)
                {
                    var conflict = await _context.Entitlements.AsNoTracking()
                        .Where(e => e.IsLastVersion
                                    && !e.IsCancelled
                                    && e.MasterEntitlementId != entitlement.MasterEntitlementId
                                    && e.HebrewYearId == entitlement.HebrewYearId
                                    && e.AssistantTypeId == entitlement.AssistantTypeId
                                    && e.InstitutionId == request.InstitutionId.Value
                                    && e.ClassName == entitlement.ClassName)
                        .AnyAsync();
                    if (conflict)
                        throw new InvalidOperationException(
                            "קיימת כבר זכאות לאותו מוסד וסוג (וכיתה) — יש לבטל אחת מהן לפני הקישור");
                }

                institutionId = request.InstitutionId.Value;
                remaining.Remove(EntitlementInvalidReasons.MissingInstitution);
            }

            await ValidateNoOverlapAsync(
                excludeMasterId: entitlement.MasterEntitlementId,
                assistantType,
                entitlement.StartDate,
                entitlement.EndDate,
                institutionId,
                entitlement.ClassName,
                pupilIdNumber);

            var now = DateTime.UtcNow;
            await CreateNewEntitlementVersionAsync(entitlement, userId, newVersion =>
            {
                newVersion.PupilIdNumber = pupilIdNumber;
                newVersion.InstitutionId = institutionId;
                newVersion.InvalidReasons = EntitlementInvalidReasons.Join(remaining);
                newVersion.IsValid = remaining.Count == 0;
                newVersion.ValidityResolvedAt = now;
                newVersion.ValidityResolvedUser = userId;
                newVersion.ValidityResolvedReason = reason;
            });
        }

        // ─── private helpers ──────────────────────────────────────────────────────

        private async Task<Entitlement> CreateNewEntitlementVersionAsync(
            Entitlement existing,
            int? userId,
            Action<Entitlement> applyUpdates)
        {
            var now = DateTime.UtcNow;

            existing.IsLastVersion = false;
            existing.UpdateUser = userId;
            existing.UpdatedAt = now;

            var newVersion = new Entitlement
            {
                EntityId                 = existing.EntityId,
                HebrewYearId             = existing.HebrewYearId,
                AssistantTypeId          = existing.AssistantTypeId,
                StartDate                = existing.StartDate,
                EndDate                  = existing.EndDate,
                Hours                    = existing.Hours,
                HoursUnit                = existing.HoursUnit,
                MinistryParticipationPct = existing.MinistryParticipationPct,
                InstitutionId            = existing.InstitutionId,
                ClassName                = existing.ClassName,
                ClassClassificationId    = existing.ClassClassificationId,
                PupilIdNumber            = existing.PupilIdNumber,
                PupilFirstName           = existing.PupilFirstName,
                PupilLastName            = existing.PupilLastName,
                IsValid                  = existing.IsValid,
                InvalidReasons           = existing.InvalidReasons,
                SourceInstitutionSymbol  = existing.SourceInstitutionSymbol,
                SourceSupportCode        = existing.SourceSupportCode,
                ValidityResolvedAt       = existing.ValidityResolvedAt,
                ValidityResolvedUser     = existing.ValidityResolvedUser,
                ValidityResolvedReason   = existing.ValidityResolvedReason,
                MasterEntitlementId      = existing.MasterEntitlementId,
                Version                  = existing.Version + 1,
                IsLastVersion            = true,
                IsCancelled              = false,
                IsActive                 = true,
                UserId                   = userId,
                UpdateUser               = userId,
                CreatedAt                = now,
                UpdatedAt                = now
            };

            applyUpdates(newVersion);

            // Keep is_active in sync with cancelled for legacy UI filters
            if (newVersion.IsCancelled)
                newVersion.IsActive = false;

            _context.Entitlements.Add(newVersion);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created entitlement version {Version} (id={Id}) for master {MasterId}",
                newVersion.Version, newVersion.Id, newVersion.MasterEntitlementId);

            return newVersion;
        }

        private async Task ValidateNoOverlapAsync(
            int? excludeMasterId,
            AssistantType assistantType,
            DateOnly startDate,
            DateOnly endDate,
            int? institutionId,
            string? className,
            string? pupilIdNumber)
        {
            var candidates = await _context.Entitlements
                .AsNoTracking()
                .Where(e => e.IsLastVersion && !e.IsCancelled)
                .Where(e => excludeMasterId == null || e.MasterEntitlementId != excludeMasterId.Value)
                .Where(e => e.StartDate <= endDate && startDate <= e.EndDate)
                .ToListAsync();

            if (candidates.Count == 0)
                return;

            if (IsPersonalLevel(assistantType.Level))
            {
                if (!string.IsNullOrEmpty(pupilIdNumber) &&
                    candidates.Any(e => e.PupilIdNumber == pupilIdNumber))
                {
                    throw new InvalidOperationException(
                        "קיימת כבר זכאות אישית פעילה לאותו תלמיד בטווח תאריכים חופף");
                }
                return;
            }

            var candidateTypeIds = candidates.Select(c => c.AssistantTypeId).Distinct().ToList();
            var typeNames = await _sharedContext.AssistantTypes
                .AsNoTracking()
                .Where(at => candidateTypeIds.Contains(at.Id))
                .ToDictionaryAsync(at => at.Id, at => at.Name);

            if (IsClassHelp(assistantType.Name))
            {
                var conflict = candidates.Any(e =>
                    e.InstitutionId == institutionId &&
                    string.Equals(e.ClassName, className, StringComparison.Ordinal) &&
                    typeNames.TryGetValue(e.AssistantTypeId, out var name) &&
                    IsClassHelp(name));

                if (conflict)
                    throw new InvalidOperationException(
                        "קיימת כבר זכאות סייעת כיתתית פעילה לאותה כיתה באותו מוסד בטווח תאריכים חופף");
                return;
            }

            if (IsSchoolHelp(assistantType.Name))
            {
                var conflict = candidates.Any(e =>
                    e.InstitutionId == institutionId &&
                    typeNames.TryGetValue(e.AssistantTypeId, out var name) &&
                    IsSchoolHelp(name));

                if (conflict)
                    throw new InvalidOperationException(
                        "קיימת כבר זכאות סייעת מוסדית פעילה לאותו מוסד בטווח תאריכים חופף");
            }
        }

        private async Task<Dictionary<int, decimal>> BuildAllocatedHoursByMasterAsync(List<Entitlement> items)
        {
            if (items.Count == 0)
                return new Dictionary<int, decimal>();

            var masterIds = items.Select(i => i.MasterEntitlementId).Distinct().ToList();
            var versions = await _context.Entitlements
                .AsNoTracking()
                .Where(e => masterIds.Contains(e.MasterEntitlementId))
                .Select(e => new { e.Id, e.MasterEntitlementId })
                .ToListAsync();

            var versionIds = versions.Select(v => v.Id).ToList();
            var hoursByVersion = versionIds.Count == 0
                ? new Dictionary<int, decimal>()
                : await _context.EntitlementAllocations
                    .AsNoTracking()
                    .Where(a => a.IsActive && versionIds.Contains(a.EntitlementId))
                    .GroupBy(a => a.EntitlementId)
                    .Select(g => new { EntitlementId = g.Key, TotalHours = g.Sum(a => a.Hours) })
                    .ToDictionaryAsync(x => x.EntitlementId, x => x.TotalHours);

            var hoursByMaster = versions
                .GroupBy(v => v.MasterEntitlementId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(v => hoursByVersion.TryGetValue(v.Id, out var h) ? h : 0m));

            // Map to each item id using its master (so list/detail keyed by row id works)
            return items.ToDictionary(
                i => i.Id,
                i => hoursByMaster.TryGetValue(i.MasterEntitlementId, out var h) ? h : 0m);
        }

        private async Task<List<int>> GetVersionIdsForMasterAsync(int masterEntitlementId)
        {
            return await _context.Entitlements
                .AsNoTracking()
                .Where(e => e.MasterEntitlementId == masterEntitlementId)
                .Select(e => e.Id)
                .ToListAsync();
        }

        private async Task<List<EntitlementListItemDto>> MapListAsync(
            List<Entitlement> items,
            Dictionary<int, decimal>? allocatedHoursMap = null)
        {
            if (items.Count == 0)
                return new List<EntitlementListItemDto>();

            var assistantTypeIds = items.Select(i => i.AssistantTypeId).Distinct().ToList();
            var institutionIds = items.Where(i => i.InstitutionId.HasValue)
                                 .Select(i => i.InstitutionId!.Value).Distinct().ToList();
            var classificationIds = items.Where(i => i.ClassClassificationId.HasValue)
                                    .Select(i => i.ClassClassificationId!.Value).Distinct().ToList();

            var assistantTypes = await _sharedContext.AssistantTypes
                .AsNoTracking()
                .Where(at => assistantTypeIds.Contains(at.Id))
                .ToDictionaryAsync(at => at.Id, at => at);

            var schools = institutionIds.Count == 0
                ? new Dictionary<int, (string Name, string TypeName, string? SchoolLevel)>()
                : await _context.Institutions
                    .AsNoTracking()
                    .Where(e => institutionIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, e => (Name: e.Name, TypeName: e.InstitutionType, SchoolLevel: e.SchoolLevel));

            var classifications = classificationIds.Count == 0
                ? new Dictionary<int, string>()
                : (await _sharedContext.ClassClassifications
                    .AsNoTracking()
                    .Where(c => classificationIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync())
                    .ToDictionary(c => c.Id, c => $"{c.Id} - {c.Name}");

            var hebrewYearIds = items.Select(i => i.HebrewYearId).Distinct().ToList();
            var hebrewYearNames = await _sharedContext.HebrewYears
                .AsNoTracking()
                .Where(y => hebrewYearIds.Contains(y.Id))
                .ToDictionaryAsync(y => y.Id, y => y.YearName);

            return items.Select(item =>
            {
                schools.TryGetValue(item.InstitutionId ?? 0, out var school);
                assistantTypes.TryGetValue(item.AssistantTypeId, out var atype);
                string? classificationName = null;
                if (item.ClassClassificationId.HasValue)
                    classifications.TryGetValue(item.ClassClassificationId.Value, out classificationName);

                var allocatedHours = allocatedHoursMap != null && allocatedHoursMap.TryGetValue(item.Id, out var h) ? h : 0m;
                var allocationStatus = allocatedHours <= 0m ? "none"
                    : allocatedHours >= item.Hours    ? "full"
                    : "partial";

                return new EntitlementListItemDto
                {
                    Id                       = item.Id,
                    MasterEntitlementId      = item.MasterEntitlementId,
                    Version                  = item.Version,
                    HebrewYearId             = item.HebrewYearId,
                    HebrewYearName           = hebrewYearNames.TryGetValue(item.HebrewYearId, out var yearName)
                                                ? yearName : string.Empty,
                    AssistantTypeId          = item.AssistantTypeId,
                    AssistantTypeName        = atype?.DisplayName ?? string.Empty,
                    AssistantTypeLevel       = atype?.Level,
                    AssistantTypeCode        = atype?.Name,
                    StartDate                = item.StartDate,
                    EndDate                  = item.EndDate,
                    Hours                    = item.Hours,
                    HoursUnit                = item.HoursUnit,
                    MinistryParticipationPct = item.MinistryParticipationPct,
                    InstitutionId            = item.InstitutionId,
                    SchoolName               = item.InstitutionId.HasValue ? school.Name : null,
                    OrgUnitType              = item.InstitutionId.HasValue ? school.TypeName : null,
                    SchoolLevel              = item.InstitutionId.HasValue ? school.SchoolLevel : null,
                    ClassName                = item.ClassName,
                    ClassClassificationId    = item.ClassClassificationId,
                    ClassClassificationName  = classificationName,
                    PupilIdNumber            = item.PupilIdNumber,
                    PupilFirstName           = item.PupilFirstName,
                    PupilLastName            = item.PupilLastName,
                    IsCancelled              = item.IsCancelled,
                    IsActive                 = item.IsActive && !item.IsCancelled,
                    IsValid                  = item.IsValid,
                    InvalidReasons           = item.InvalidReasons,
                    InvalidReasonsDisplay    = EntitlementInvalidReasons.ToHebrewList(item.InvalidReasons),
                    SourceInstitutionSymbol  = item.SourceInstitutionSymbol,
                    SourceSupportCode        = item.SourceSupportCode,
                    ValidityResolvedReason   = item.ValidityResolvedReason,
                    ValidityResolvedAt       = item.ValidityResolvedAt,
                    AllocatedHours           = allocatedHours,
                    AllocationStatus         = allocationStatus
                };
            }).ToList();
        }

        private async Task ValidateClassClassificationAsync(int? classClassificationId)
        {
            if (!classClassificationId.HasValue)
                return;

            // Allow inactive classification if it is already stored on the entitlement (clearing/changing still ok).
            var exists = await _sharedContext.ClassClassifications
                .AsNoTracking()
                .AnyAsync(c => c.Id == classClassificationId.Value);

            if (!exists)
                throw new InvalidOperationException("סיווג כיתה לא תקין");
        }

        private async Task<AssistantType> LoadAssistantTypeAsync(int assistantTypeId, bool requireActive = true)
        {
            var query = _sharedContext.AssistantTypes.AsNoTracking().Where(at => at.Id == assistantTypeId);
            if (requireActive)
                query = query.Where(at => at.IsActive);

            var atype = await query.FirstOrDefaultAsync();

            if (atype == null)
                throw new InvalidOperationException(
                    requireActive ? "סוג סייעת לא תקין או לא פעיל" : "סוג סייעת לא נמצא");

            return atype;
        }

        private async Task<HebrewYear> LoadHebrewYearAsync(int yearId, bool requireActive = true)
        {
            var year = await _sharedContext.HebrewYears.AsNoTracking().FirstOrDefaultAsync(y => y.Id == yearId);
            if (year == null)
                throw new InvalidOperationException("שנה עברית לא נמצאה");
            if (requireActive && !year.IsActive)
                throw new InvalidOperationException("שנה עברית לא פעילה");
            return year;
        }

        private async Task ValidateInstitutionBelongsToTenantAsync(int institutionId)
        {
            var valid = await _context.Institutions
                .AsNoTracking()
                .AnyAsync(e => e.Id == institutionId && e.IsActive);

            if (!valid)
                throw new InvalidOperationException("מוסד חינוך לא תקין עבור הרשות");
        }

        private static void ValidateHoursUnit(string hoursUnit)
        {
            if (hoursUnit != HoursUnits.Weekly && hoursUnit != HoursUnits.Monthly)
                throw new InvalidOperationException("יחידת שעות חייבת להיות שבועית או חודשית");
        }

        private static void ValidateDates(DateOnly startDate, DateOnly endDate, HebrewYear year)
        {
            if (startDate > endDate)
                throw new InvalidOperationException("תאריך התחלה חייב להיות לפני תאריך סיום");

            if (year.StartDate.HasValue && startDate < year.StartDate.Value)
                throw new InvalidOperationException("תאריך התחלה חייב להיות בתוך שנת הלימודים");

            if (year.EndDate.HasValue && endDate > year.EndDate.Value)
                throw new InvalidOperationException("תאריך סיום חייב להיות בתוך שנת הלימודים");

            if (year.StartDate.HasValue && endDate < year.StartDate.Value)
                throw new InvalidOperationException("תאריך סיום חייב להיות בתוך שנת הלימודים");

            if (year.EndDate.HasValue && startDate > year.EndDate.Value)
                throw new InvalidOperationException("תאריך התחלה חייב להיות בתוך שנת הלימודים");
        }

        private static void ValidateKindFields(
            bool isPersonal,
            int? institutionId,
            string? pupilIdNumber,
            string? pupilFirstName,
            string? pupilLastName,
            bool allowInvalid = false)
        {
            if (!allowInvalid && !institutionId.HasValue)
                throw new InvalidOperationException("יש לבחור בית ספר או גן");

            if (isPersonal)
            {
                if (string.IsNullOrWhiteSpace(pupilIdNumber))
                    throw new InvalidOperationException("תעודת זהות תלמיד נדרשת");
                if (string.IsNullOrWhiteSpace(pupilFirstName))
                    throw new InvalidOperationException("שם פרטי תלמיד נדרש");
                if (string.IsNullOrWhiteSpace(pupilLastName))
                    throw new InvalidOperationException("שם משפחה תלמיד נדרש");
            }
        }

        /// <summary>
        /// Empty after digit-strip is a hard error. Checksum / length failures are invalid (soft)
        /// when the caller allows import-with-flag.
        /// </summary>
        public (string? Normalized, string? HardError, bool IsInvalid) EvaluatePupilId(string? raw)
        {
            var digits = IsraeliIdHelper.DigitsOnly(raw);
            if (string.IsNullOrEmpty(digits))
                return (null, "תעודת זהות תלמיד נדרשת", false);

            var normalized = digits.Length < 9
                ? digits.PadLeft(9, '0')
                : digits.Length > 9 ? digits[^9..] : digits;

            if (normalized.Length != 9 || !normalized.All(char.IsDigit))
                return (normalized, null, true);

            var attr = _attributeCache.GetAttributeValue("validate_israeli_id_checksum");
            if (bool.TryParse(attr, out bool doCheck) && doCheck && !IsraeliIdHelper.IsValidIsraeliId(normalized))
                return (normalized, null, true);

            return (normalized, null, false);
        }

        private static void ApplyUploadValidity(Entitlement target, EntitlementUploadValidity validity)
        {
            target.IsValid = validity.IsValid;
            target.InvalidReasons = validity.ReasonsCsv;
            target.SourceInstitutionSymbol = validity.SourceInstitutionSymbol;
            target.SourceSupportCode = validity.SourceSupportCode;
        }

        private static bool IsPersonalLevel(string? level)
            => string.Equals(level, "personal", StringComparison.OrdinalIgnoreCase);

        private static bool IsClassHelp(string? typeName)
            => string.Equals(typeName, "class_help", StringComparison.OrdinalIgnoreCase);

        private static bool IsSchoolHelp(string? typeName)
            => string.Equals(typeName, "school_help", StringComparison.OrdinalIgnoreCase);

        private static string? NormalizeOptionalText(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string NormalizeRequiredText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{fieldName} נדרש");
            return value.Trim();
        }
    }
}
