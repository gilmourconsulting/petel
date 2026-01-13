using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;

namespace PetelApp.Api.Services
{
    public class AlertService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AlertService> _logger;

        public AlertService(AppDbContext context, ILogger<AlertService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create alert with distribution logic based on alert level and flags
        /// </summary>
        public async Task<(long alertId, int linksCreated, List<int> distributedTo)> CreateAlertAsync(
            CreateAlertDto dto,
            int userId,
            int entityId,
            int entityTypeId)
        {
            _logger.LogInformation(
                "📝 Creating alert: Type={Type}, Level={Level}, IsEvent={IsEvent}, EntityId={EntityId}",
                dto.AlertType, dto.AlertLevel, dto.IsEvent, entityId);

            // Validate alert type and level exist in cache
            if (!AlertDefinitionsCache.IsValidAlertType(dto.AlertType))
            {
                throw new ArgumentException($"Invalid alert type: {dto.AlertType}");
            }

            if (!AlertDefinitionsCache.IsValidAlertLevel(dto.AlertLevel))
            {
                throw new ArgumentException($"Invalid alert level: {dto.AlertLevel}");
            }

            // Validate event date requirement
            if (dto.IsEvent && !dto.EventDate.HasValue)
            {
                throw new ArgumentException("EventDate is required when IsEvent is true");
            }

            // Create alert
            var alert = new Alert
            {
                AlertType = dto.AlertType,
                AlertLevel = dto.AlertLevel,
                Description = dto.Description,
                Status = 1, // New status
                UserId = userId,
                IsEvent = dto.IsEvent,
                EventDate = dto.EventDate,
                CreatedAt = DateTime.UtcNow
            };

            _context.Alerts.Add(alert);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Alert created with Id={AlertId}", alert.Id);

            // Collect entity IDs for alert links
            var targetEntityIds = new List<int> { entityId }; // Always include current entity

            // Distribution logic based on alert level
            if (dto.AlertLevel == 2 && dto.DistributeToOwned)
            {
                // Level 2 (owner) - Get entities owned by current entity
                var ownedEntities = await _context.Entities
                    .Where(e => e.OwnerId == entityId && e.IsActive)
                    .Select(e => e.Id)
                    .ToListAsync();

                targetEntityIds.AddRange(ownedEntities);

                _logger.LogInformation(
                    "📢 Added {Count} owned entities for owner {EntityId}",
                    ownedEntities.Count, entityId);
            }

            if (dto.DistributeToSchools)
            {
                if (entityTypeId == 5) // School network
                {
                    // Get schools owned by this network
                    var networkSchools = await _context.Entities
                        .Where(e => e.OwnerId == entityId 
                                 && e.EntityTypeId == 4 // School type
                                 && e.IsActive)
                        .Select(e => e.Id)
                        .ToListAsync();

                    targetEntityIds.AddRange(networkSchools);

                    _logger.LogInformation(
                        "🏫 Added {Count} schools for network {EntityId}",
                        networkSchools.Count, entityId);
                }
                else if (entityTypeId == 6) // Owner
                {
                    // Get schools where the owner is an entity owned by current entity
                    var ownerSchools = await _context.Entities
                        .Where(school => school.EntityTypeId == 4 
                                      && school.IsActive
                                      && _context.Entities.Any(owner => 
                                          owner.OwnerId == entityId 
                                          && owner.Id == school.OwnerId
                                          && owner.IsActive))
                        .Select(e => e.Id)
                        .ToListAsync();

                    targetEntityIds.AddRange(ownerSchools);

                    _logger.LogInformation(
                        "🏫 Added {Count} schools via owned entities for owner {EntityId}",
                        ownerSchools.Count, entityId);
                }
            }

            // Remove duplicates
            targetEntityIds = targetEntityIds.Distinct().ToList();

            _logger.LogInformation(
                "📊 Creating alert links for {Count} entities", 
                targetEntityIds.Count);

            // Create alert links
            var alertLinks = targetEntityIds.Select(targetEntityId => new AlertLink
            {
                AlertId = alert.Id,
                AlertStatus = 1, // New status
                EntityId = targetEntityId,
                IsLastVersion = true
            }).ToList();

            _context.AlertLinks.AddRange(alertLinks);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Alert creation complete: AlertId={AlertId}, Links={LinkCount}",
                alert.Id, alertLinks.Count);

            return (alert.Id, alertLinks.Count, targetEntityIds);
        }

        /// <summary>
        /// Create a simple alert for a specific school without distribution logic
        /// </summary>
        public async Task<long> CreateSchoolAlertAsync(
            string description,
            int schoolId,
            int userId,
            bool isEvent = false,
            DateTime? eventDate = null)
        {
            _logger.LogInformation(
                "📝 Creating simple school alert: SchoolId={SchoolId}, Description={Description}",
                schoolId, description);

            // Create alert
            var alert = new Alert
            {
                AlertType = 1, // System alert
                AlertLevel = 1, // School level
                Description = description,
                Status = 1, // New status
                UserId = userId,
                IsEvent = isEvent,
                EventDate = eventDate,
                CreatedAt = DateTime.UtcNow
            };

            _context.Alerts.Add(alert);
            await _context.SaveChangesAsync();

            // Create alert link to the school
            var alertLink = new AlertLink
            {
                AlertId = alert.Id,
                AlertStatus = 1, // New status
                EntityId = schoolId,
                IsLastVersion = true
            };

            _context.AlertLinks.Add(alertLink);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Simple school alert created: AlertId={AlertId}, SchoolId={SchoolId}",
                alert.Id, schoolId);

            return alert.Id;
        }
    }
}
