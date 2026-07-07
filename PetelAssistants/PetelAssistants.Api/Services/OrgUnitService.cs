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
            "school",
            "kindergarten"
        };

        private readonly SharedDbContext _sharedContext;
        private readonly ILogger<OrgUnitService> _logger;

        public OrgUnitService(SharedDbContext sharedContext, ILogger<OrgUnitService> logger)
        {
            _sharedContext = sharedContext;
            _logger = logger;
        }

        public async Task<List<OrgUnitDto>> ListOrgUnitsAsync(int entityId, string? typeFilter)
        {
            var query = _sharedContext.Entities
                .AsNoTracking()
                .Include(e => e.EntityType)
                .Where(e => e.ParentEntityId == entityId);

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                var type = typeFilter.Trim().ToLowerInvariant();
                if (!AllowedTypes.Contains(type))
                    throw new InvalidOperationException("סוג מוסד לא תקין");

                query = query.Where(e => e.EntityType != null && e.EntityType.Name == type);
            }
            else
            {
                query = query.Where(e => e.EntityType != null
                                      && (e.EntityType.Name == "school" || e.EntityType.Name == "kindergarten"));
            }

            return await query
                .OrderBy(e => e.Name)
                .Select(e => new OrgUnitDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    OrgUnitType = e.EntityType != null ? e.EntityType.Name : string.Empty,
                    OrgUnitTypeDescription = e.EntityType != null ? e.EntityType.Description : null,
                    IsActive = e.IsActive
                })
                .ToListAsync();
        }

        public async Task<int> CreateOrgUnitAsync(int entityId, CreateOrgUnitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("שם מוסד הוא שדה חובה");

            var typeName = request.OrgUnitType.Trim().ToLowerInvariant();
            if (!AllowedTypes.Contains(typeName))
                throw new InvalidOperationException("סוג מוסד חייב להיות בית ספר או גן");

            var entityType = await _sharedContext.EntityTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(et => et.Name == typeName && et.IsActive)
                ?? throw new InvalidOperationException("סוג מוסד לא נמצא");

            var exists = await _sharedContext.Entities
                .AnyAsync(e => e.ParentEntityId == entityId && e.Name == request.Name.Trim());

            if (exists)
                throw new InvalidOperationException("מוסד עם שם זה כבר קיים ברשות");

            var orgUnit = new Entity
            {
                Name = request.Name.Trim(),
                EntityTypeId = entityType.Id,
                ParentEntityId = entityId,
                IsActive = true
            };

            _sharedContext.Entities.Add(orgUnit);
            await _sharedContext.SaveChangesAsync();

            _logger.LogInformation("Created org unit {Name} ({Type}) for entity {EntityId}", orgUnit.Name, typeName, entityId);
            return orgUnit.Id;
        }

        public async Task UpdateOrgUnitAsync(int entityId, int id, UpdateOrgUnitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("שם מוסד הוא שדה חובה");

            var orgUnit = await _sharedContext.Entities
                .FirstOrDefaultAsync(e => e.Id == id && e.ParentEntityId == entityId)
                ?? throw new InvalidOperationException("מוסד לא נמצא");

            var duplicate = await _sharedContext.Entities
                .AnyAsync(e => e.ParentEntityId == entityId && e.Name == request.Name.Trim() && e.Id != id);

            if (duplicate)
                throw new InvalidOperationException("מוסד עם שם זה כבר קיים ברשות");

            orgUnit.Name = request.Name.Trim();
            await _sharedContext.SaveChangesAsync();
        }

        public async Task SetOrgUnitActiveAsync(int entityId, int id, bool isActive)
        {
            var orgUnit = await _sharedContext.Entities
                .FirstOrDefaultAsync(e => e.Id == id && e.ParentEntityId == entityId)
                ?? throw new InvalidOperationException("מוסד לא נמצא");

            orgUnit.IsActive = isActive;
            await _sharedContext.SaveChangesAsync();
        }
    }
}
