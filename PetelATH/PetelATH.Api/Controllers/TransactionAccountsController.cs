using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Models;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    /// <summary>
    /// Controller for managing transaction accounts between entities.
    /// Example: School network managing external student fee accounts with councils.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionAccountsController : BaseController
    {
        private readonly AppDbContext _context;

        public TransactionAccountsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<TransactionAccountsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        #region GET Endpoints

        /// <summary>
        /// Get all transaction accounts for the current entity
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAccounts()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var entityId = int.Parse(session.EntityId);

                var accounts = await _context.TransactionAccounts
                    .AsNoTracking()
                    .Include(ta => ta.OwnerEntity)
                    .Include(ta => ta.RelatedEntity)
                    .Include(ta => ta.AccountType)
                    .Where(ta => ta.OwnerEntityId == entityId)
                    .OrderBy(ta => ta.AccountName)
                    .Select(ta => new
                    {
                        ta.Id,
                        ta.OwnerEntityId,
                        OwnerEntityName = ta.OwnerEntity.Name,
                        ta.RelatedEntityId,
                        RelatedEntityName = ta.RelatedEntity.Name,
                        ta.AccountTypeId,
                        AccountTypeName = ta.AccountType.Description,
                        ta.AccountName,
                        ta.Description,
                        ta.Balance,
                        ta.IsActive,
                        ta.CreatedAt,
                        ta.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = accounts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction accounts");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת חשבונות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get a specific transaction account by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAccount(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var entityId = int.Parse(session.EntityId);

                var account = await _context.TransactionAccounts
                    .AsNoTracking()
                    .Include(ta => ta.OwnerEntity)
                    .Include(ta => ta.RelatedEntity)
                    .Include(ta => ta.AccountType)
                    .Where(ta => ta.Id == id && ta.OwnerEntityId == entityId)
                    .Select(ta => new
                    {
                        ta.Id,
                        ta.OwnerEntityId,
                        OwnerEntityName = ta.OwnerEntity.Name,
                        ta.RelatedEntityId,
                        RelatedEntityName = ta.RelatedEntity.Name,
                        ta.AccountTypeId,
                        AccountTypeName = ta.AccountType.Description,
                        ta.AccountName,
                        ta.Description,
                        ta.Balance,
                        ta.IsActive,
                        ta.CreatedAt,
                        ta.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (account == null)
                {
                    return NotFound(new { success = false, message = "חשבון לא נמצא" });
                }

                return Ok(new { success = true, data = account });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction account {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת חשבון",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get accounts by related entity (e.g., all accounts for a specific council)
        /// </summary>
        [HttpGet("by-related-entity/{relatedEntityId}")]
        public async Task<IActionResult> GetAccountsByRelatedEntity(int relatedEntityId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var entityId = int.Parse(session.EntityId);

                var accounts = await _context.TransactionAccounts
                    .AsNoTracking()
                    .Include(ta => ta.AccountType)
                    .Where(ta => ta.OwnerEntityId == entityId && ta.RelatedEntityId == relatedEntityId)
                    .Select(ta => new
                    {
                        ta.Id,
                        ta.AccountTypeId,
                        AccountTypeName = ta.AccountType.Description,
                        ta.AccountName,
                        ta.Description,
                        ta.Balance,
                        ta.IsActive
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = accounts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving accounts for related entity {RelatedEntityId}", relatedEntityId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת חשבונות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get available account types for transaction accounts
        /// </summary>
        [HttpGet("account-types")]
        public async Task<IActionResult> GetAccountTypes()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var accountTypes = await _context.TransactionAccountTypes
                    .AsNoTracking()
                    .Where(at => at.IsActive)
                    .OrderBy(at => at.SortOrder)
                    .Select(at => new
                    {
                        at.Id,
                        at.Name,
                        at.Description
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = accountTypes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving account types");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת סוגי חשבונות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get available entities for transaction accounts (all active non-school entities)
        /// </summary>
        [HttpGet("available-entities")]
        public async Task<IActionResult> GetAvailableEntities([FromQuery] int? entityTypeId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var query = _context.Entities
                    .AsNoTracking()
                    .Where(e => e.IsActive && e.EntityTypeId != 1 && e.EntityTypeId != 4); // Exclude school types

                // Optional filter by entity type (e.g., only councils)
                if (entityTypeId.HasValue)
                {
                    query = query.Where(e => e.EntityTypeId == entityTypeId.Value);
                }

                var entities = await query
                    .OrderBy(e => e.Name)
                    .Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        entity_type_id = e.EntityTypeId
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = entities });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available entities");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת ישויות",
                    error = ex.Message
                });
            }
        }

        #endregion

        #region POST Endpoints

        /// <summary>
        /// Create a new transaction account
        /// Automatically creates entity for council if it doesn't exist
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var entityId = int.Parse(session.EntityId);
                int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

                // Validate account type exists
                var accountType = await _context.TransactionAccountTypes
                    .FirstOrDefaultAsync(at => at.Id == request.AccountTypeId && at.IsActive);

                if (accountType == null)
                {
                    return BadRequest(new { success = false, message = "סוג חשבון לא תקין" });
                }

                // Check if related entity exists
                var relatedEntity = await _context.Entities
                    .FirstOrDefaultAsync(e => e.Id == request.RelatedEntityId);

                if (relatedEntity == null)
                {
                    return BadRequest(new { success = false, message = "ישות קשורה לא נמצאה" });
                }

                // Check for duplicate account
                var existingAccount = await _context.TransactionAccounts
                    .FirstOrDefaultAsync(ta =>
                        ta.OwnerEntityId == entityId &&
                        ta.RelatedEntityId == request.RelatedEntityId &&
                        ta.AccountTypeId == request.AccountTypeId);

                if (existingAccount != null)
                {
                    return BadRequest(new { success = false, message = "חשבון כבר קיים עבור ישות וסוג זה" });
                }

                var account = new TransactionAccount
                {
                    OwnerEntityId = entityId,
                    RelatedEntityId = request.RelatedEntityId,
                    AccountTypeId = request.AccountTypeId,
                    AccountName = request.AccountName,
                    Description = request.Description,
                    Balance = 0.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedUser = userId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdateUser = userId
                };

                _context.TransactionAccounts.Add(account);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "חשבון נוצר בהצלחה",
                    accountId = account.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction account");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת חשבון",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Create an entity for a council and optionally create an account
        /// </summary>
        [HttpPost("create-council-entity")]
        public async Task<IActionResult> CreateCouncilEntity([FromBody] CreateCouncilEntityRequest request)
        {
            try
            {
                _logger.LogInformation("CreateCouncilEntity called with CouncilId: {CouncilId}", request?.CouncilId);

                if (request == null)
                {
                    _logger.LogError("Request body is null");
                    return BadRequest(new { success = false, message = "נתוני הבקשה חסרים" });
                }

                if (request.CouncilId <= 0)
                {
                    _logger.LogError("Invalid CouncilId: {CouncilId}", request.CouncilId);
                    return BadRequest(new { success = false, message = "מזהה מועצה לא תקין" });
                }

                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

                // Validate council exists
                var council = await _context.Councils
                    .FirstOrDefaultAsync(c => c.Id == request.CouncilId);

                if (council == null)
                {
                    return BadRequest(new { success = false, message = "מועצה לא נמצאה" });
                }

                // Check if entity already exists for this council
                var existingEntity = await _context.Entities
                    .FirstOrDefaultAsync(e => e.CouncilId == request.CouncilId);

                if (existingEntity != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "ישות כבר קיימת עבור מועצה זו",
                        entityId = existingEntity.Id
                    });
                }

                // Get council entity type (type 2)
                var councilEntityType = await _context.EntityTypes
                    .FirstOrDefaultAsync(et => et.Id == 2);

                if (councilEntityType == null)
                {
                    return BadRequest(new { success = false, message = "סוג ישות מועצה לא נמצא" });
                }

                // Create entity for council
                var councilEntity = new Entity
                {
                    Name = council.Name,
                    EntityTypeId = 2, // Council entity type
                    CouncilId = request.CouncilId,
                    IsActive = true
                };

                _context.Entities.Add(councilEntity);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "ישות מועצה נוצרה בהצלחה",
                    data = new
                    {
                        id = councilEntity.Id,
                        entityName = councilEntity.Name,
                        entityTypeId = councilEntity.EntityTypeId,
                        isActive = councilEntity.IsActive
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating council entity");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת ישות מועצה",
                    error = ex.Message
                });
            }
        }

        #endregion

        #region PUT Endpoints

        /// <summary>
        /// Update an existing transaction account
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var entityId = int.Parse(session.EntityId);
                int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

                var account = await _context.TransactionAccounts
                    .FirstOrDefaultAsync(ta => ta.Id == id && ta.OwnerEntityId == entityId);

                if (account == null)
                {
                    return NotFound(new { success = false, message = "חשבון לא נמצא" });
                }

                account.AccountName = request.AccountName;
                account.Description = request.Description;
                account.IsActive = request.IsActive;
                account.UpdatedAt = DateTime.UtcNow;
                account.UpdateUser = userId;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "חשבון עודכן בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction account {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון חשבון",
                    error = ex.Message
                });
            }
        }

        #endregion

        #region DELETE Endpoints

        /// <summary>
        /// Delete a transaction account (soft delete by setting IsActive = false)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var entityId = int.Parse(session.EntityId);

                var account = await _context.TransactionAccounts
                    .FirstOrDefaultAsync(ta => ta.Id == id && ta.OwnerEntityId == entityId);

                if (account == null)
                {
                    return NotFound(new { success = false, message = "חשבון לא נמצא" });
                }

                // Soft delete
                account.IsActive = false;
                account.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "חשבון הוסר בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction account {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת חשבון",
                    error = ex.Message
                });
            }
        }

        #endregion
    }

    #region Request Models

    public class CreateAccountRequest
    {
        public int RelatedEntityId { get; set; }
        public int AccountTypeId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateAccountRequest
    {
        public string AccountName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateCouncilEntityRequest
    {
        public int CouncilId { get; set; }
    }

    #endregion
}
