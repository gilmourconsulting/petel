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

        public async Task<int> CreateEntitlementAsync(int entityId, int? userId, CreateEntitlementRequest request)
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
            ValidateKindFields(isPersonal, request.InstitutionId, request.PupilIdNumber, request.PupilFirstName, request.PupilLastName);

            var className = NormalizeOptionalText(request.ClassName);
            if (IsClassHelp(assistantType.Name) && string.IsNullOrWhiteSpace(className))
                throw new InvalidOperationException("שם כיתה נדרש לזכאות מסוג סייעת כיתתית");

            if (isPersonal)
                ValidatePupilIdNumber(request.PupilIdNumber!);

            await ValidateInstitutionBelongsToTenantAsync(request.InstitutionId!.Value);
            await ValidateClassClassificationAsync(request.ClassClassificationId);

            if (request.Hours <= 0)
                throw new InvalidOperationException("מספר שעות חייב להיות גדול מאפס");

            if (request.MinistryParticipationPct < 0 || request.MinistryParticipationPct > 100)
                throw new InvalidOperationException("אחוז השתתפות משרד החינוך חייב להיות בין 0 ל-100");

            var pupilIdNumber = isPersonal ? NormalizeRequiredText(request.PupilIdNumber, "תעודת זהות תלמיד") : null;

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
            int? classClassificationId)
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

            await ValidateClassClassificationAsync(classClassificationId);

            await CreateNewEntitlementVersionAsync(entitlement, userId, newVersion =>
            {
                newVersion.Hours = hours;
                newVersion.HoursUnit = hoursUnit;
                newVersion.MinistryParticipationPct = ministryParticipationPct;
                newVersion.ClassClassificationId = classClassificationId;
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
                ? new Dictionary<int, (string Name, string TypeName)>()
                : await _context.Institutions
                    .AsNoTracking()
                    .Where(e => institutionIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, e => (Name: e.Name, TypeName: e.InstitutionType));

            var classifications = classificationIds.Count == 0
                ? new Dictionary<int, string>()
                : (await _sharedContext.ClassClassifications
                    .AsNoTracking()
                    .Where(c => classificationIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync())
                    .ToDictionary(c => c.Id, c => $"{c.Id} - {c.Name}");

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
                    ClassName                = item.ClassName,
                    ClassClassificationId    = item.ClassClassificationId,
                    ClassClassificationName  = classificationName,
                    PupilIdNumber            = item.PupilIdNumber,
                    PupilFirstName           = item.PupilFirstName,
                    PupilLastName            = item.PupilLastName,
                    IsCancelled              = item.IsCancelled,
                    IsActive                 = item.IsActive && !item.IsCancelled,
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
            string? pupilLastName)
        {
            if (!institutionId.HasValue)
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

        private void ValidatePupilIdNumber(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber) || idNumber.Length != 9 || !idNumber.All(char.IsDigit))
                throw new InvalidOperationException("תעודת זהות חייבת להכיל בדיוק 9 ספרות");

            var raw = _attributeCache.GetAttributeValue("validate_israeli_id_checksum");
            if (bool.TryParse(raw, out bool doCheck) && doCheck && !IsraeliIdHelper.IsValidIsraeliId(idNumber))
                throw new InvalidOperationException("מספר תעודת זהות לא תקין — ספרת ביקורת שגויה");
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
