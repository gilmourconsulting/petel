using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class OrgUnitService
    {
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            InstitutionTypes.School,
            InstitutionTypes.Kindergarten
        };

        private static readonly HashSet<string> AllowedSchoolLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            SchoolLevels.Elementary,
            SchoolLevels.HighSchool
        };

        private readonly AssistDbContext _context;
        private readonly ILogger<OrgUnitService> _logger;

        public OrgUnitService(AssistDbContext context, ILogger<OrgUnitService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<OrgUnitDto>> ListOrgUnitsAsync(int entityId, string? typeFilter)
        {
            // entityId is used by the global query filter via ITenantContext; keep signature for controller.
            _ = entityId;

            var query = _context.Institutions.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                var type = typeFilter.Trim().ToLowerInvariant();
                if (!AllowedTypes.Contains(type))
                    throw new InvalidOperationException("סוג מוסד לא תקין");

                query = query.Where(e => e.InstitutionType == type);
            }

            var rows = await query
                .OrderBy(e => e.Name)
                .ToListAsync();

            return rows.Select(MapDto).ToList();
        }

        public async Task<int> CreateOrgUnitAsync(int entityId, int? userId, CreateOrgUnitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("שם מוסד הוא שדה חובה");

            var typeName = NormalizeType(request.OrgUnitType);
            var schoolLevel = NormalizeSchoolLevel(typeName, request.SchoolLevel);

            var name = request.Name.Trim();
            var exists = await _context.Institutions
                .AnyAsync(e => e.Name == name);

            if (exists)
                throw new InvalidOperationException("מוסד עם שם זה כבר קיים ברשות");

            var symbol = NormalizeSymbol(request.Symbol);
            if (symbol != null)
            {
                var symbolExists = await _context.Institutions
                    .AnyAsync(e => e.Symbol == symbol);
                if (symbolExists)
                    throw new InvalidOperationException("מוסד עם סמל זה כבר קיים ברשות");
            }

            var now = DateTime.UtcNow;
            var institution = new Institution
            {
                EntityId = entityId,
                Name = name,
                Symbol = symbol,
                InstitutionType = typeName,
                SchoolLevel = schoolLevel,
                IsSpecialEducation = request.IsSpecialEducation,
                IsActive = true,
                UserId = userId,
                UpdateUser = userId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Institutions.Add(institution);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created institution {Name} ({Type}) for entity {EntityId}",
                institution.Name, typeName, entityId);
            return institution.Id;
        }

        public async Task UpdateOrgUnitAsync(int entityId, int? userId, int id, UpdateOrgUnitRequest request)
        {
            _ = entityId;

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("שם מוסד הוא שדה חובה");

            var institution = await _context.Institutions.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new InvalidOperationException("מוסד לא נמצא");

            var name = request.Name.Trim();
            var duplicate = await _context.Institutions
                .AnyAsync(e => e.Name == name && e.Id != id);

            if (duplicate)
                throw new InvalidOperationException("מוסד עם שם זה כבר קיים ברשות");

            var symbol = NormalizeSymbol(request.Symbol);
            if (symbol != null)
            {
                var symbolDuplicate = await _context.Institutions
                    .AnyAsync(e => e.Symbol == symbol && e.Id != id);
                if (symbolDuplicate)
                    throw new InvalidOperationException("מוסד עם סמל זה כבר קיים ברשות");
            }

            var schoolLevel = NormalizeSchoolLevel(institution.InstitutionType, request.SchoolLevel);

            institution.Name = name;
            institution.Symbol = symbol;
            institution.SchoolLevel = schoolLevel;
            institution.IsSpecialEducation = request.IsSpecialEducation;
            institution.UpdateUser = userId;
            institution.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task SetOrgUnitActiveAsync(int entityId, int? userId, int id, bool isActive)
        {
            _ = entityId;

            var institution = await _context.Institutions.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new InvalidOperationException("מוסד לא נמצא");

            institution.IsActive = isActive;
            institution.UpdateUser = userId;
            institution.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private static OrgUnitDto MapDto(Institution e) => new()
        {
            Id = e.Id,
            Name = e.Name,
            Symbol = e.Symbol,
            OrgUnitType = e.InstitutionType,
            OrgUnitTypeDescription = GetTypeDescription(e.InstitutionType),
            SchoolLevel = e.SchoolLevel,
            IsSpecialEducation = e.IsSpecialEducation,
            IsActive = e.IsActive
        };

        private static string? NormalizeSymbol(string? symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return null;
            var trimmed = symbol.Trim();
            if (trimmed.Length > 20)
                throw new InvalidOperationException("סמל מוסד ארוך מדי");
            return trimmed;
        }

        private static string GetTypeDescription(string type) => type switch
        {
            InstitutionTypes.School => "בית ספר",
            InstitutionTypes.Kindergarten => "גן ילדים",
            _ => type
        };

        private static string NormalizeType(string? orgUnitType)
        {
            var typeName = (orgUnitType ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedTypes.Contains(typeName))
                throw new InvalidOperationException("סוג מוסד חייב להיות בית ספר או גן");
            return typeName;
        }

        private static string? NormalizeSchoolLevel(string institutionType, string? schoolLevel)
        {
            if (institutionType == InstitutionTypes.Kindergarten)
            {
                if (!string.IsNullOrWhiteSpace(schoolLevel))
                    throw new InvalidOperationException("רמת בית ספר אינה רלוונטית לגן ילדים");
                return null;
            }

            var level = (schoolLevel ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedSchoolLevels.Contains(level))
                throw new InvalidOperationException(
                    $"רמת בית ספר חייבת להיות {SchoolLevels.ElementaryDisplay} או {SchoolLevels.HighSchoolDisplay}");

            return level;
        }
    }
}
