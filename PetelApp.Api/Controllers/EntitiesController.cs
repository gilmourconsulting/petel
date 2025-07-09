// PetelApp.Api/Controllers/EntitiesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntitiesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EntitiesController> _logger;

        public EntitiesController(AppDbContext context, ILogger<EntitiesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all active entities for the login dropdown
        /// This endpoint is public and doesn't require tenant context
        /// since users need to see available entities before selecting one
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetEntities()
        {
            try
            {
                _logger.LogInformation("Fetching active entities for login dropdown");

                var entities = await _context.Entities
                    .Where(e => e.IsActive == true)
                    .Select(e => new EntityDto
                    {
                        Id = e.Id,
                        Name = e.Name,
                        Description = e.PrincipalName ?? string.Empty,
                        Address = e.Address ?? string.Empty,
                        Phone = e.Phone ?? string.Empty,
                        Email = e.Email ?? string.Empty
                    })
                    .OrderBy(e => e.Name)
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {Count} active entities", entities.Count);

                return Ok(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entities");
                
                return StatusCode(500, new ErrorResponse
                {
                    Success = false,
                    Message = "שגיאה בטעינת הישויות",
                    Details = "אנא נסה שוב או פנה למנהל המערכת"
                });
            }
        }

        /// <summary>
        /// Get a specific entity by ID
        /// Requires tenant context for security
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEntity(int id)
        {
            try
            {
                // This endpoint might be used in authenticated parts of the app
                // so we can validate tenant access if needed
                
                var entity = await _context.Entities
                    .Include(e => e.EntityType)
                    .Where(e => e.Id == id && e.IsActive == true)
                    .Select(e => new EntityDetailDto
                    {
                        Id = e.Id,
                        Name = e.Name,
                        Description = e.PrincipalName ?? string.Empty,
                        Address = e.Address ?? string.Empty,
                        Phone = e.Phone ?? string.Empty,
                        Email = e.Email ?? string.Empty,
                        EntityTypeName = e.EntityType.Name,
                        CreatedDate = e.CreatedAt,
                        IsActive = e.IsActive,
                        UserCount = e.Users.Count(u => u.IsActive == true)
                    })
                    .FirstOrDefaultAsync();

                if (entity == null)
                {
                    return NotFound(new ErrorResponse
                    {
                        Success = false,
                        Message = "הישות לא נמצאה",
                        Details = "הישות המבוקשת לא קיימת או לא פעילה"
                    });
                }

                return Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity {EntityId}", id);
                
                return StatusCode(500, new ErrorResponse
                {
                    Success = false,
                    Message = "שגיאה בטעינת פרטי הישות"
                });
            }
        }

        /// <summary>
        /// Validate if a user has access to a specific entity
        /// Used for additional security checks
        /// </summary>
        [HttpGet("{entityId}/validate-access/{userId}")]
        public async Task<IActionResult> ValidateUserAccess(int entityId, int userId)
        {
            try
            {
                var hasAccess = await _context.Users
                    .AnyAsync(u => u.Id == userId && 
                                  u.EntityId == entityId && 
                                  u.IsActive == true);

                if (!hasAccess)
                {
                    return Ok(new ValidationResponse
                    {
                        HasAccess = false,
                        Message = "המשתמש אינו רשאי לגשת לישות זו"
                    });
                }

                // Get additional user info for the response
                var userInfo = await _context.Users
                    .Where(u => u.Id == userId && u.EntityId == entityId)
                    .Select(u => new
                    {
                        Username = u.Username,
                        FullName = u.FirstName + " " + u.LastName,
                        LastLogin = u.LastLogin
                    })
                    .FirstOrDefaultAsync();

                return Ok(new ValidationResponse
                {
                    HasAccess = true,
                    Message = "גישה מאושרת",
                    UserInfo = userInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user {UserId} access to entity {EntityId}", userId, entityId);
                
                return StatusCode(500, new ErrorResponse
                {
                    Success = false,
                    Message = "שגיאה בבדיקת הרשאות"
                });
            }
        }
    }

    // DTOs for API responses
    public class EntityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class EntityDetailDto : EntityDto
    {
        public string EntityTypeName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
        public int UserCount { get; set; }
    }

    public class ValidationResponse
    {
        public bool HasAccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? UserInfo { get; set; }
    }

    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}