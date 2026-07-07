using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class EntitlementService
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly ILogger<EntitlementService> _logger;

        public EntitlementService(
            AssistDbContext context,
            SharedDbContext sharedContext,
            ILogger<EntitlementService> logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _logger = logger;
        }

        public async Task<List<EntitlementListItemDto>> ListEntitlementsAsync(int entityId, int yearId, string kind)
        {
            ValidateKind(kind);

            var items = await _context.Entitlements
                .AsNoTracking()
                .Where(e => e.HebrewYearId == yearId && e.EntitlementKind == kind)
                .OrderByDescending(e => e.Id)
                .ToListAsync();

            return await MapListAsync(items);
        }

        public async Task<EntitlementListItemDto?> GetEntitlementAsync(int id)
        {
            var item = await _context.Entitlements
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (item == null)
                return null;

            var list = await MapListAsync(new List<Entitlement> { item });
            return list.FirstOrDefault();
        }

        public async Task<int> CreateEntitlementAsync(int entityId, int? userId, CreateEntitlementRequest request)
        {
            ValidateKind(request.EntitlementKind);
            ValidateHoursUnit(request.HoursUnit);

            var year = await LoadHebrewYearAsync(request.HebrewYearId);
            await ValidateAssistantTypeAsync(request.AssistantTypeId);

            var startDate = request.StartDate ?? year.StartDate
                ?? throw new InvalidOperationException("תאריך התחלה נדרש — הגדר תאריכים לשנה העברית");
            var endDate = request.EndDate ?? year.EndDate
                ?? throw new InvalidOperationException("תאריך סיום נדרש — הגדר תאריכים לשנה העברית");

            ValidateDates(startDate, endDate, year);
            ValidateKindFields(request.EntitlementKind, request.SchoolEntityId, request.PupilExternalId, request.ClassName);

            if (request.EntitlementKind == EntitlementKinds.Institutional)
                await ValidateSchoolBelongsToTenantAsync(entityId, request.SchoolEntityId!.Value);

            if (request.Hours <= 0)
                throw new InvalidOperationException("מספר שעות חייב להיות גדול מאפס");

            if (request.MinistryParticipationPct < 0 || request.MinistryParticipationPct > 100)
                throw new InvalidOperationException("אחוז השתתפות משרד החינוך חייב להיות בין 0 ל-100");

            var now = DateTime.UtcNow;
            var entitlement = new Entitlement
            {
                EntityId = entityId,
                HebrewYearId = request.HebrewYearId,
                AssistantTypeId = request.AssistantTypeId,
                EntitlementKind = request.EntitlementKind,
                StartDate = startDate,
                EndDate = endDate,
                Hours = request.Hours,
                HoursUnit = request.HoursUnit,
                MinistryParticipationPct = request.MinistryParticipationPct,
                SchoolEntityId = request.EntitlementKind == EntitlementKinds.Institutional ? request.SchoolEntityId : null,
                ClassName = request.EntitlementKind == EntitlementKinds.Institutional
                    ? NormalizeOptionalText(request.ClassName)
                    : null,
                PupilExternalId = request.EntitlementKind == EntitlementKinds.Personal
                    ? NormalizeRequiredText(request.PupilExternalId, "מזהה תלמיד חיצוני")
                    : null,
                IsActive = true,
                UserId = userId,
                UpdateUser = userId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Entitlements.Add(entitlement);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created entitlement {Id} for entity {EntityId}", entitlement.Id, entityId);
            return entitlement.Id;
        }

        public async Task UpdateEntitlementAsync(int entityId, int? userId, int id, UpdateEntitlementRequest request)
        {
            ValidateHoursUnit(request.HoursUnit);

            var entitlement = await _context.Entitlements.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            var year = await LoadHebrewYearAsync(entitlement.HebrewYearId);
            await ValidateAssistantTypeAsync(request.AssistantTypeId);

            ValidateDates(request.StartDate, request.EndDate, year);
            ValidateKindFields(entitlement.EntitlementKind, request.SchoolEntityId, request.PupilExternalId, request.ClassName);

            if (entitlement.EntitlementKind == EntitlementKinds.Institutional)
                await ValidateSchoolBelongsToTenantAsync(entityId, request.SchoolEntityId!.Value);

            if (request.Hours <= 0)
                throw new InvalidOperationException("מספר שעות חייב להיות גדול מאפס");

            if (request.MinistryParticipationPct < 0 || request.MinistryParticipationPct > 100)
                throw new InvalidOperationException("אחוז השתתפות משרד החינוך חייב להיות בין 0 ל-100");

            entitlement.AssistantTypeId = request.AssistantTypeId;
            entitlement.StartDate = request.StartDate;
            entitlement.EndDate = request.EndDate;
            entitlement.Hours = request.Hours;
            entitlement.HoursUnit = request.HoursUnit;
            entitlement.MinistryParticipationPct = request.MinistryParticipationPct;
            entitlement.SchoolEntityId = entitlement.EntitlementKind == EntitlementKinds.Institutional
                ? request.SchoolEntityId
                : null;
            entitlement.ClassName = entitlement.EntitlementKind == EntitlementKinds.Institutional
                ? NormalizeOptionalText(request.ClassName)
                : null;
            entitlement.PupilExternalId = entitlement.EntitlementKind == EntitlementKinds.Personal
                ? NormalizeRequiredText(request.PupilExternalId, "מזהה תלמיד חיצוני")
                : null;
            entitlement.UpdateUser = userId;
            entitlement.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task DeactivateEntitlementAsync(int? userId, int id)
        {
            var entitlement = await _context.Entitlements.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new InvalidOperationException("זכאות לא נמצאה");

            entitlement.IsActive = false;
            entitlement.UpdateUser = userId;
            entitlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task<List<EntitlementListItemDto>> MapListAsync(List<Entitlement> items)
        {
            if (items.Count == 0)
                return new List<EntitlementListItemDto>();

            var assistantTypeIds = items.Select(i => i.AssistantTypeId).Distinct().ToList();
            var schoolIds = items.Where(i => i.SchoolEntityId.HasValue).Select(i => i.SchoolEntityId!.Value).Distinct().ToList();

            var assistantTypes = await _sharedContext.AssistantTypes
                .AsNoTracking()
                .Where(at => assistantTypeIds.Contains(at.Id))
                .ToDictionaryAsync(at => at.Id, at => at.DisplayName);

            var schools = schoolIds.Count == 0
                ? new Dictionary<int, (string Name, string? TypeName)>()
                : await _sharedContext.Entities
                    .AsNoTracking()
                    .Where(e => schoolIds.Contains(e.Id))
                    .Select(e => new { e.Id, e.Name, TypeName = e.EntityType != null ? e.EntityType.Name : null })
                    .ToDictionaryAsync(e => e.Id, e => (e.Name, e.TypeName));

            return items.Select(item =>
            {
                schools.TryGetValue(item.SchoolEntityId ?? 0, out var school);
                assistantTypes.TryGetValue(item.AssistantTypeId, out var typeName);

                return new EntitlementListItemDto
                {
                    Id = item.Id,
                    HebrewYearId = item.HebrewYearId,
                    EntitlementKind = item.EntitlementKind,
                    AssistantTypeId = item.AssistantTypeId,
                    AssistantTypeName = typeName ?? string.Empty,
                    StartDate = item.StartDate,
                    EndDate = item.EndDate,
                    Hours = item.Hours,
                    HoursUnit = item.HoursUnit,
                    MinistryParticipationPct = item.MinistryParticipationPct,
                    SchoolEntityId = item.SchoolEntityId,
                    SchoolName = item.SchoolEntityId.HasValue ? school.Name : null,
                    OrgUnitType = item.SchoolEntityId.HasValue ? school.TypeName : null,
                    ClassName = item.ClassName,
                    PupilExternalId = item.PupilExternalId,
                    IsActive = item.IsActive
                };
            }).ToList();
        }

        private async Task<HebrewYear> LoadHebrewYearAsync(int yearId)
        {
            var year = await _sharedContext.HebrewYears.AsNoTracking().FirstOrDefaultAsync(y => y.Id == yearId);
            if (year == null)
                throw new InvalidOperationException("שנה עברית לא נמצאה");
            if (!year.IsActive)
                throw new InvalidOperationException("שנה עברית לא פעילה");
            return year;
        }

        private async Task ValidateAssistantTypeAsync(int assistantTypeId)
        {
            var exists = await _sharedContext.AssistantTypes
                .AsNoTracking()
                .AnyAsync(at => at.Id == assistantTypeId && at.IsActive);

            if (!exists)
                throw new InvalidOperationException("סוג סייעת לא תקין או לא פעיל");
        }

        private async Task ValidateSchoolBelongsToTenantAsync(int entityId, int schoolEntityId)
        {
            var valid = await _sharedContext.Entities
                .AsNoTracking()
                .AnyAsync(e => e.Id == schoolEntityId
                            && e.ParentEntityId == entityId
                            && e.IsActive
                            && e.EntityType != null
                            && (e.EntityType.Name == "school" || e.EntityType.Name == "kindergarten"));

            if (!valid)
                throw new InvalidOperationException("מוסד חינוך לא תקין עבור הרשות");
        }

        private static void ValidateKind(string kind)
        {
            if (kind != EntitlementKinds.Institutional && kind != EntitlementKinds.Personal)
                throw new InvalidOperationException("סוג זכאות לא תקין");
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
            string kind,
            int? schoolEntityId,
            string? pupilExternalId,
            string? className)
        {
            if (kind == EntitlementKinds.Institutional)
            {
                if (!schoolEntityId.HasValue)
                    throw new InvalidOperationException("יש לבחור בית ספר או גן");
            }
            else if (kind == EntitlementKinds.Personal)
            {
                if (string.IsNullOrWhiteSpace(pupilExternalId))
                    throw new InvalidOperationException("מזהה תלמיד חיצוני נדרש");
            }

            _ = className;
        }

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
