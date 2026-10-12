using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Models;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    /// <summary>
    /// CRUD for configuration lookup tables surfaced on the Settings screen.
    /// "Delete" is always a logical delete (is_active = false) so existing
    /// references never fail; inactive rows are still returned here (for the
    /// admin grid) but callers that build drop-downs should filter is_active.
    /// Lookups are system-wide, so no entity scoping is applied.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class LookupsController : BaseController
    {
        private readonly AppDbContext _context;

        public LookupsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<LookupsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        // ===================================================================
        // GET api/lookups/{table}
        // Returns all rows (including inactive) projected to a common shape.
        // ===================================================================
        [HttpGet("{table}")]
        public async Task<IActionResult> GetAll(string table)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            try
            {
                List<LookupItemDto> items = table switch
                {
                    "statuses" => await _context.Statuses.AsNoTracking()
                        .OrderBy(e => e.SortOrder).ThenBy(e => e.Id)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            Object = e.Object,
                            SortOrder = e.SortOrder,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "entity-types" => await _context.EntityTypes.AsNoTracking()
                        .OrderBy(e => e.Id)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "council-types" => await _context.CouncilTypes.AsNoTracking()
                        .OrderBy(e => e.SortOrder).ThenBy(e => e.Name)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            SortOrder = e.SortOrder,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "districts" => await _context.Districts.AsNoTracking()
                        .OrderBy(e => e.SortOrder).ThenBy(e => e.Name)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            SortOrder = e.SortOrder,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "transaction-account-types" => await _context.TransactionAccountTypes.AsNoTracking()
                        .OrderBy(e => e.SortOrder).ThenBy(e => e.Name)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            Description = e.Description,
                            SortOrder = e.SortOrder,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "transaction-types" => await _context.TransactionTypes.AsNoTracking()
                        .OrderBy(e => e.Name)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            Description = e.Description,
                            IsCredit = e.IsCredit,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "transaction-detail-types" => await _context.TransactionDetailTypes.AsNoTracking()
                        .OrderBy(e => e.Name)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            Description = e.Description,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "alert-types" => await _context.AlertTypes.AsNoTracking()
                        .OrderBy(e => e.Id)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            Description = e.Description,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "alert-statuses" => await _context.AlertStatuses.AsNoTracking()
                        .OrderBy(e => e.Id)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            Description = e.Description,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "alert-levels" => await _context.AlertLevels.AsNoTracking()
                        .OrderBy(e => e.Id)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            Description = e.Description,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "document-status-types" => await _context.DocumentStatusTypes.AsNoTracking()
                        .OrderBy(e => e.Id)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    "special-needs-characterizations" => await _context.SpecialNeedsCharacterizations.AsNoTracking()
                        .OrderBy(e => e.Name)
                        .Select(e => new LookupItemDto
                        {
                            Id = e.Id,
                            Name = e.Name,
                            IsActive = e.IsActive
                        }).ToListAsync(),

                    _ => null!
                };

                if (items == null)
                    return NotFound(new { success = false, message = "טבלת הגדרות לא מוכרת" });

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lookup table {Table}", table);
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת נתונים", error = ex.Message });
            }
        }

        // ===================================================================
        // POST api/lookups/{table}
        // ===================================================================
        [HttpPost("{table}")]
        public async Task<IActionResult> Create(string table, [FromBody] LookupSaveRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request?.Name))
                return BadRequest(new { success = false, message = "שם הוא שדה חובה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
            var now = DateTime.UtcNow;

            try
            {
                switch (table)
                {
                    case "statuses":
                        _context.Statuses.Add(new Status
                        {
                            Name = request.Name,
                            Object = request.Object,
                            SortOrder = request.SortOrder,
                            IsActive = true,
                            UserId = userId,
                            CreatedAt = now
                        });
                        break;

                    case "entity-types":
                        _context.EntityTypes.Add(new EntityType
                        {
                            Name = request.Name,
                            IsActive = true,
                            CreatedAt = now
                        });
                        break;

                    case "council-types":
                        _context.CouncilTypes.Add(new CouncilType
                        {
                            Name = request.Name,
                            SortOrder = request.SortOrder ?? 0,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedUser = userId,
                            UpdatedAt = now,
                            UpdateUser = userId
                        });
                        break;

                    case "districts":
                        _context.Districts.Add(new District
                        {
                            Name = request.Name,
                            SortOrder = request.SortOrder ?? 0,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedUser = userId,
                            UpdatedAt = now,
                            UpdateUser = userId
                        });
                        break;

                    case "transaction-account-types":
                        _context.TransactionAccountTypes.Add(new TransactionAccountType
                        {
                            Name = request.Name,
                            Description = request.Description ?? string.Empty,
                            SortOrder = request.SortOrder ?? 0,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedUser = userId,
                            UpdatedAt = now,
                            UpdateUser = userId
                        });
                        break;

                    case "transaction-types":
                        _context.TransactionTypes.Add(new TransactionType
                        {
                            Name = request.Name,
                            Description = request.Description ?? string.Empty,
                            IsCredit = request.IsCredit ?? false,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedUser = userId,
                            UpdatedAt = now,
                            UpdateUser = userId
                        });
                        break;

                    case "transaction-detail-types":
                        _context.TransactionDetailTypes.Add(new TransactionDetailType
                        {
                            Name = request.Name,
                            Description = request.Description ?? string.Empty,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedUser = userId,
                            UpdatedAt = now,
                            UpdateUser = userId
                        });
                        break;

                    case "alert-types":
                        _context.AlertTypes.Add(new AlertType
                        {
                            Name = request.Name,
                            Description = request.Description,
                            IsActive = true,
                            UserId = userId ?? 0
                        });
                        break;

                    case "alert-statuses":
                        _context.AlertStatuses.Add(new AlertStatus
                        {
                            Name = request.Name,
                            Description = request.Description,
                            IsActive = true,
                            UserId = userId ?? 0
                        });
                        break;

                    case "alert-levels":
                        _context.AlertLevels.Add(new AlertLevel
                        {
                            Name = request.Name,
                            Description = request.Description,
                            IsActive = true,
                            UserId = userId ?? 0
                        });
                        break;

                    case "document-status-types":
                        _context.DocumentStatusTypes.Add(new DocumentStatusType
                        {
                            Name = request.Name,
                            IsActive = true
                        });
                        break;

                    case "special-needs-characterizations":
                        _context.SpecialNeedsCharacterizations.Add(new SpecialNeedsCharacterization
                        {
                            Name = request.Name,
                            IsActive = true
                        });
                        break;

                    default:
                        return NotFound(new { success = false, message = "טבלת הגדרות לא מוכרת" });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "הרשומה נוצרה בהצלחה" });
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return BadRequest(new { success = false, message = "ערך זה כבר קיים" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating row in lookup table {Table}", table);
                return StatusCode(500, new { success = false, message = "שגיאה ביצירת רשומה", error = ex.Message });
            }
        }

        // ===================================================================
        // PUT api/lookups/{table}/{id}
        // ===================================================================
        [HttpPut("{table}/{id:int}")]
        public async Task<IActionResult> Update(string table, int id, [FromBody] LookupSaveRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request?.Name))
                return BadRequest(new { success = false, message = "שם הוא שדה חובה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
            var now = DateTime.UtcNow;
            short sid = (short)id;

            try
            {
                switch (table)
                {
                    case "statuses":
                        {
                            var e = await _context.Statuses.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.Object = request.Object;
                            e.SortOrder = request.SortOrder;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            break;
                        }
                    case "entity-types":
                        {
                            var e = await _context.EntityTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            break;
                        }
                    case "council-types":
                        {
                            var e = await _context.CouncilTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.SortOrder = request.SortOrder ?? e.SortOrder;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "districts":
                        {
                            var e = await _context.Districts.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.SortOrder = request.SortOrder ?? e.SortOrder;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "transaction-account-types":
                        {
                            var e = await _context.TransactionAccountTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.Description = request.Description ?? e.Description;
                            e.SortOrder = request.SortOrder ?? e.SortOrder;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "transaction-types":
                        {
                            var e = await _context.TransactionTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.Description = request.Description ?? e.Description;
                            if (request.IsCredit.HasValue) e.IsCredit = request.IsCredit.Value;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "transaction-detail-types":
                        {
                            var e = await _context.TransactionDetailTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.Description = request.Description ?? e.Description;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "alert-types":
                        {
                            var e = await _context.AlertTypes.FindAsync(sid);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.Description = request.Description;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            break;
                        }
                    case "alert-statuses":
                        {
                            var e = await _context.AlertStatuses.FindAsync(sid);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.Description = request.Description;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            break;
                        }
                    case "alert-levels":
                        {
                            var e = await _context.AlertLevels.FindAsync(sid);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            e.Description = request.Description;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            break;
                        }
                    case "document-status-types":
                        {
                            var e = await _context.DocumentStatusTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            break;
                        }
                    case "special-needs-characterizations":
                        {
                            var e = await _context.SpecialNeedsCharacterizations.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.Name = request.Name;
                            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
                            break;
                        }
                    default:
                        return NotFound(new { success = false, message = "טבלת הגדרות לא מוכרת" });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "הרשומה עודכנה בהצלחה" });
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return BadRequest(new { success = false, message = "ערך זה כבר קיים" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating row {Id} in lookup table {Table}", id, table);
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון רשומה", error = ex.Message });
            }
        }

        // ===================================================================
        // DELETE api/lookups/{table}/{id}  (logical delete: is_active = false)
        // ===================================================================
        [HttpDelete("{table}/{id:int}")]
        public async Task<IActionResult> SoftDelete(string table, int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
            var now = DateTime.UtcNow;
            short sid = (short)id;

            try
            {
                switch (table)
                {
                    case "statuses":
                        {
                            var e = await _context.Statuses.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            break;
                        }
                    case "entity-types":
                        {
                            var e = await _context.EntityTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            break;
                        }
                    case "council-types":
                        {
                            var e = await _context.CouncilTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "districts":
                        {
                            var e = await _context.Districts.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "transaction-account-types":
                        {
                            var e = await _context.TransactionAccountTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "transaction-types":
                        {
                            var e = await _context.TransactionTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "transaction-detail-types":
                        {
                            var e = await _context.TransactionDetailTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            e.UpdatedAt = now;
                            e.UpdateUser = userId;
                            break;
                        }
                    case "alert-types":
                        {
                            var e = await _context.AlertTypes.FindAsync(sid);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            break;
                        }
                    case "alert-statuses":
                        {
                            var e = await _context.AlertStatuses.FindAsync(sid);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            break;
                        }
                    case "alert-levels":
                        {
                            var e = await _context.AlertLevels.FindAsync(sid);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            break;
                        }
                    case "document-status-types":
                        {
                            var e = await _context.DocumentStatusTypes.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            break;
                        }
                    case "special-needs-characterizations":
                        {
                            var e = await _context.SpecialNeedsCharacterizations.FindAsync(id);
                            if (e == null) return NotFoundRow();
                            e.IsActive = false;
                            break;
                        }
                    default:
                        return NotFound(new { success = false, message = "טבלת הגדרות לא מוכרת" });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "הרשומה הוסרה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft-deleting row {Id} in lookup table {Table}", id, table);
                return StatusCode(500, new { success = false, message = "שגיאה בהסרת רשומה", error = ex.Message });
            }
        }

        private IActionResult NotFoundRow() =>
            NotFound(new { success = false, message = "רשומה לא נמצאה" });

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
    }

    /// <summary>
    /// Common shape returned for every lookup row. Fields not relevant to a
    /// particular table are left null.
    /// </summary>
    public class LookupItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Object { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsCredit { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Writable fields accepted when creating/updating a lookup row.
    /// </summary>
    public class LookupSaveRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Object { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsCredit { get; set; }
        public bool? IsActive { get; set; }
    }
}
