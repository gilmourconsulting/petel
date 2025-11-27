using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Controllers;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Services;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlertsController : BaseController
    {
        private readonly AppDbContext _context;

        public AlertsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<AlertsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

   /// <summary>
/// Create new alert/event with distribution logic
/// Creates alert and alert_links based on alert level and distribution flags
/// </summary>
[HttpPost]
public async Task<IActionResult> CreateAlert([FromBody] CreateAlertDto dto)
{
    try {
        var session = GetCurrentSession();
        if (session == null)
        {
            return Unauthorized("Session not found");
        }

        var entityId = int.Parse(session.EntityId);
        var entityTypeId = int.Parse(session.EntityTypeId);

        _logger.LogInformation(
            "📝 Creating alert: Type={Type}, Level={Level}, IsEvent={IsEvent}, EntityId={EntityId}, EntityTypeId={EntityTypeId}",
            dto.AlertType, dto.AlertLevel, dto.IsEvent, entityId, entityTypeId);

        // Validate alert type and level exist in cache
        if (!AlertDefinitionsCache.IsValidAlertType(dto.AlertType))
        {
            return BadRequest($"Invalid alert type: {dto.AlertType}");
        }

        if (!AlertDefinitionsCache.IsValidAlertLevel(dto.AlertLevel))
        {
            return BadRequest($"Invalid alert level: {dto.AlertLevel}");
        }

        // Validate event date requirement
        if (dto.IsEvent && !dto.EventDate.HasValue)
        {
            return BadRequest("EventDate is required when IsEvent is true");
        }

        // Create alert
        var alert = new Alert
        {
            AlertType = dto.AlertType,
            AlertLevel = dto.AlertLevel,
            Description = dto.Description,
            Status = 1, // New status
            UserId = int.Parse(session.UserId),
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
                // SQL equivalent: SELECT s.id FROM entities s 
                // WHERE s.entity_type_id = 4 AND s.owner_id IN 
                // (SELECT id FROM entities WHERE owner_id = @currentEntityId)
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

        return Ok(new
        {
            alertId = alert.Id,
            message = $"{(dto.IsEvent ? "אירוע" : "התראה")} נוצר בהצלחה",
            linksCreated = alertLinks.Count,
            distributedTo = targetEntityIds
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Error creating alert");
        return StatusCode(500, "שגיאה ביצירת ההתראה");
    }
}



/// <summary>
/// Get alerts/events for an entity with entity-specific status from alert_links
/// </summary>
[HttpGet("entity/{entityId}")]
public async Task<IActionResult> GetAlertsByEntity(
    int entityId,
    [FromQuery] bool isEvent = false)
{
    try
    {
        _logger.LogInformation(
            "📊 Getting alerts for entity {EntityId}, isEvent={IsEvent}",
            entityId, isEvent);

        // ✅ Join alerts with alert_links to get entity-specific status
        var alerts = await _context.AlertLinks
            .Where(al => al.EntityId == entityId && al.IsLastVersion)
            .Join(
                _context.Alerts,
                link => link.AlertId,
                alert => alert.Id,
                (link, alert) => new
                {
                    Alert = alert,
                    Link = link
                })
            .Where(x => x.Alert.IsEvent == isEvent)
            .OrderByDescending(x => x.Alert.CreatedAt)
            .Select(x => new
            {
                id = x.Alert.Id,
                alertType = x.Alert.AlertType,
                alertLevel = x.Alert.AlertLevel,
                description = x.Alert.Description,
                status = x.Link.AlertStatus,  // ✅ Status from alert_links, not alerts
                userId = x.Alert.UserId,
                isEvent = x.Alert.IsEvent,
                eventDate = x.Alert.EventDate,
                createdAt = x.Alert.CreatedAt,
                linkId = x.Link.Id
            })
            .ToListAsync();

        _logger.LogInformation(
            "✅ Found {Count} alerts for entity {EntityId}",
            alerts.Count, entityId);

        return Ok(alerts);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Error getting alerts for entity {EntityId}", entityId);
        return StatusCode(500, "Error loading alerts");
    }
}

/// <summary>
/// Get all alert definitions from memory cache
/// Returns alert types, statuses, and levels loaded at startup
/// </summary>
[HttpGet("definitions")]
public IActionResult GetAlertDefinitions()
{
    try
    {
        var definitions = new
        {
            note = "Alert definitions loaded at startup into memory cache (AlertDefinitionsCache)",
            alertTypes = AlertDefinitionsCache.AlertTypes.Values
                .OrderBy(a => a.Id)
                .Select(a => new
                {
                    id = a.Id,
                    typeName = a.Name
                })
                .ToList(),
            alertStatuses = AlertDefinitionsCache.AlertStatuses.Values
                .OrderBy(a => a.Id)
                .Select(a => new
                {
                    id = a.Id,
                    statusName = a.Name
                })
                .ToList(),
            alertLevels = AlertDefinitionsCache.AlertLevels.Values
                .OrderBy(a => a.Id)
                .Select(a => new
                {
                    id = a.Id,
                    levelName = a.Name
                })
                .ToList()
        };

        _logger.LogInformation(
            "📋 Retrieved alert definitions from cache: {TypeCount} types, {StatusCount} statuses, {LevelCount} levels",
            definitions.alertTypes.Count,
            definitions.alertStatuses.Count,
            definitions.alertLevels.Count
        );

        return Ok(definitions);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Error fetching alert definitions from cache");
        return StatusCode(500, "Error fetching alert definitions");
    }
}

/// <summary>
/// Update alert status for specific entity
/// Creates new alert_link version and marks previous as not last version
/// </summary>
[HttpPut("status")]
        public async Task<IActionResult> UpdateAlertStatus([FromBody] UpdateAlertStatusDto dto)
        {
            try
            {
                // Validate status exists in cache
                if (!AlertDefinitionsCache.IsValidAlertStatus(dto.Status))
                {
                    return BadRequest($"Invalid alert status: {dto.Status}");
                }

                // Find current alert link
                var currentLink = await _context.AlertLinks
                    .Where(al => al.AlertId == dto.AlertId
                               && al.EntityId == dto.EntityId
                               && al.IsLastVersion)
                    .FirstOrDefaultAsync();

                if (currentLink == null)
                {
                    return NotFound($"No alert link found for AlertId={dto.AlertId}, EntityId={dto.EntityId}");
                }

                // Mark current link as not last version
                currentLink.IsLastVersion = false;
                _context.AlertLinks.Update(currentLink);

                // Create new alert link with updated status
                var newLink = new AlertLink
                {
                    AlertId = dto.AlertId,
                    AlertStatus = dto.Status,
                    EntityId = dto.EntityId,
                    IsLastVersion = true
                };

                _context.AlertLinks.Add(newLink);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "✅ Updated alert status: AlertId={AlertId}, EntityId={EntityId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
                    dto.AlertId, dto.EntityId, currentLink.AlertStatus, dto.Status);

                return Ok(new
                {
                    message = "Alert status updated successfully",
                    alertId = dto.AlertId,
                    entityId = dto.EntityId,
                    newStatus = dto.Status,
                    previousStatus = currentLink.AlertStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating alert status");
                return StatusCode(500, "Error updating alert status");
            }
        }
    }
    
}