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
        /// Create new alert/event
        /// Creates alert and alert_links for all active entities (system level) or specific entity
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateAlert([FromBody] CreateAlertDto dto)
        {
            try
            {
                var session = GetCurrentSession();
                
                // Validate alert type, level, and status exist in cache
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
                    EventDate = dto.EventDate
                };

                _context.Alerts.Add(alert);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "✅ Alert created: Id={AlertId}, Type={Type}, Level={Level}, IsEvent={IsEvent}",
                    alert.Id, dto.AlertType, dto.AlertLevel, dto.IsEvent);

                // Create alert links based on alert level
                if (dto.AlertLevel == 1) // System level
                {
                    // Get all active entities
                    var activeEntities = await _context.Entities
                        .Where(e => e.IsActive)
                        .Select(e => e.Id)
                        .ToListAsync();

                    var alertLinks = activeEntities.Select(entityId => new AlertLink
                    {
                        AlertId = alert.Id,
                        AlertStatus = 1, // New status
                        EntityId = entityId,
                        IsLastVersion = true
                    }).ToList();

                    _context.AlertLinks.AddRange(alertLinks);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "✅ Created {Count} alert links for system-level alert {AlertId}",
                        alertLinks.Count, alert.Id);
                }
                else // School or schoolchain level
                {
                    // Create link for current entity only
                    var alertLink = new AlertLink
                    {
                        AlertId = alert.Id,
                        AlertStatus = 1, // New status
                        EntityId = int.Parse(session.EntityId),
                        IsLastVersion = true
                    };

                    _context.AlertLinks.Add(alertLink);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "✅ Created alert link for entity {EntityId}, alert {AlertId}",
                        session.EntityId, alert.Id);
                }

                return Ok(new
                {
                    alertId = alert.Id,
                    message = "Alert created successfully",
                    linksCreated = dto.AlertLevel == 1 
                        ? await _context.Entities.CountAsync(e => e.IsActive)
                        : 1
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating alert");
                return StatusCode(500, "Error creating alert");
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