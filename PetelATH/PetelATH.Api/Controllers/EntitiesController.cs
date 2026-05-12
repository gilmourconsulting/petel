// PetelATH.Api/Controllers/EntitiesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Models;
using PetelATH.Api.DTOs;
using PetelATH.Api.Session;
using System.Reflection.Metadata;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntitiesController : BaseController
    {
        private readonly AppDbContext _context;
   
        public EntitiesController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<EntitiesController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
     
        }

        /// <summary>
        /// Get all active entities for the login dropdown - NO session filtering
        /// Used by login.html to populate entity Selection before authentication
        /// </summary>
        [HttpGet("login")]
        public async Task<IActionResult> GetEntitiesForLogin()
        {
            try
            {
                var entities = await _context.Entities
                    .Where(e => e.IsActive)
                    .Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        entity_type_id = e.EntityTypeId
                    })
                    .OrderBy(e => e.name)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} entities for login dropdown", entities.Count);
                return Ok(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entities for login");
                return StatusCode(500, new { message = "שגיאה בטעינת רשימת הגופים", error = ex.Message });
            }
        }


        // Add new endpoint to get entity logo
        [HttpGet("{id}/logo")]
        public async Task<IActionResult> GetEntityLogo(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { message = "Authentication required" });
                }

                var entity = await _context.Entities
                    .AsNoTracking()
                    .Where(e => e.Id == id)
                    .Select(e => new { e.Id, e.EntityLogo })
                    .FirstOrDefaultAsync();

                if (entity == null)
                {
                    return NotFound(new { message = "גוף לא נמצא" });
                }

                // ✅ If logo is null or empty, return default logo
                if (entity.EntityLogo == null || entity.EntityLogo.Length == 0)
                {
                    _logger.LogInformation("No custom logo for entity {EntityId}, returning default", id);

                    // Read default logo from wwwroot/images folder
                    var defaultLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "", "images", "default_school.png");

                    if (System.IO.File.Exists(defaultLogoPath))
                    {
                        var defaultLogoBytes = await System.IO.File.ReadAllBytesAsync(defaultLogoPath);
                        return File(defaultLogoBytes, "image/png");
                    }
                    else
                    {
                        _logger.LogWarning("Default logo file not found at {Path}", defaultLogoPath);
                        return NotFound(new { message = "לוגו ברירת מחדל לא נמצא" });
                    }
                }

                // Return image as byte array
                return File(entity.EntityLogo, "image/png"); // Adjust MIME type if needed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entity logo for ID {EntityId}", id);
                return StatusCode(500, new { message = "שגיאה בטעינת הלוגו", error = ex.Message });
            }
        }

  [HttpGet("schools")]
public async Task<IActionResult> GetSchools([FromQuery] int? yearId = null)
{
    try
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            _logger.LogWarning("No valid session found for schools request");
            return Unauthorized(new { success = false, message = "Authentication required" });
        }

        if (!int.TryParse(session.EntityId, out int sessionEntityId))
        {
            _logger.LogError("Invalid EntityId in session: {EntityId}", session.EntityId);
            return BadRequest(new { success = false, message = "Invalid session entity ID" });
        }

        // ✅ ADD: Check if user is admin (userId = 1)
        bool isAdmin = session.UserId == "1";
        _logger.LogInformation("GetSchools request - IsAdmin: {IsAdmin}, SessionEntityId: {EntityId}", 
            isAdmin, sessionEntityId);

        // ✅ Get SelectedYearId from session if not provided in query
        if (!yearId.HasValue)
        {
            var SelectedYearIdStr = session.GetProperty("SelectedYearId");
            if (!string.IsNullOrEmpty(SelectedYearIdStr) && int.TryParse(SelectedYearIdStr, out int SelectedYearId))
            {
                yearId = SelectedYearId;
            }
        }

        if (!yearId.HasValue)
        {
            _logger.LogError("No year ID provided or found in session");
            return BadRequest(new { success = false, message = "Year ID required" });
        }

        _logger.LogInformation("Loading schools from schools table for year {YearId}", yearId.Value);

        // ✅ STEP 1: Get all school_year IDs for the Selected Hebrew year
        var schoolYearIds = await _context.SchoolYears
            .AsNoTracking()
            .Where(sy => sy.YearId == yearId.Value)
            .Select(sy => sy.Id)
            .ToListAsync();

        _logger.LogInformation("Found {Count} school years for year ID {YearId}", 
            schoolYearIds.Count, yearId.Value);

        if (!schoolYearIds.Any())
        {
            _logger.LogWarning("No school years found for year ID {YearId}", yearId.Value);
            return Ok(new List<SchoolDto>()); // Return empty list
        }

        // ✅ STEP 1.5: Get entity IDs owned by current user's entity (for hierarchical filtering)
        var ownedEntityIds = await _context.Entities
            .AsNoTracking()
            .Where(e => e.Owner.Id == sessionEntityId)
            .Select(e => e.Id)
            .ToListAsync();
            
        _logger.LogInformation("Found {Count} entities owned by current user", ownedEntityIds.Count);

        // ✅ step 2: query schools table with proper filtering logic
        // - if admin (userId = 1): show all schools
        // - if not admin: show schools where owner matches current entity OR owner is owned by current entity
        var schoolsquery = await _context.Schools
            .AsNoTracking()
            .Where(s => schoolYearIds.Contains(s.SchoolYearId) &&
                       s.IsLastVersion &&
                       s.IsActive &&
                       (isAdmin || 
                        s.Owner == sessionEntityId || 
                        (s.Owner.HasValue && ownedEntityIds.Contains(s.Owner.Value))))
            .Select(s => new
            {
              s.EntityId,
                s.Name,
                s.Symbol,
                s.Street,
                s.HouseNumber,
                s.City,
                s.PostCode,
                s.Owner,
                PrincipalFirstName = s.PrincipalPerson != null ? s.PrincipalPerson.FirstName : null,
                PrincipalLastName = s.PrincipalPerson != null ? s.PrincipalPerson.LastName : null,
                InspectorFirstName = s.InspectorPerson != null ? s.InspectorPerson.FirstName : null,
                InspectorLastName = s.InspectorPerson != null ? s.InspectorPerson.LastName : null,
                ContactFirstName = s.ContactPersonPerson != null ? s.ContactPersonPerson.FirstName : null,
                ContactLastName = s.ContactPersonPerson != null ? s.ContactPersonPerson.LastName : null,
                CharacterizationName = s.Characterization != null ? s.Characterization.Name : null,
                s.EducationStage,
                s.IsActive,
                s.SchoolYearId
            })
            
            .ToListAsync();

        // ✅ load owner entities names
        var ownerids = schoolsquery.Select(s => s.Owner)
            .Where(o => o.HasValue)
            .Select(o => o.Value)
            .Distinct()
            .ToList();
        var ownernames = await _context.Entities
            .AsNoTracking()
            .Where(e => ownerids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name);
        // ✅ format data in memory (after database query)
        var schools = schoolsquery.Select(s => new SchoolDto
        {
            Id = s.EntityId,
            Name = s.Name ?? string.Empty,
            Symbol = s.Symbol,
            Address = FormatSchoolAddress(s.Street, s.HouseNumber, s.City, s.PostCode),
            PrincipalName = FormatPersonName(s.PrincipalFirstName, s.PrincipalLastName),
            InspectorName = FormatPersonName(s.InspectorFirstName, s.InspectorLastName),
            ContactPerson = FormatPersonName(s.ContactFirstName, s.ContactLastName),
            CharacterizationName = s.CharacterizationName,
            EducationStage = s.EducationStage,
            IsActive = s.IsActive,
            SchoolYearId = s.SchoolYearId,
            OwnerId = s.Owner,
            OwnerName = s.Owner.HasValue && ownernames.ContainsKey(s.Owner.Value) ? ownernames[s.Owner.Value] : null
        }).ToList();

        _logger.LogInformation("Loaded {Count} schools for year {YearId}", schools.Count, yearId.Value);

        return Ok(schools);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading schools from schools table");
        return StatusCode(500, new
        {
            success = false,
            message = "שגיאה בטעילת רשימת בתי הספר",
            error = ex.Message
        });
    }
}


/// <summary>
/// Get the distributor name of the highest owner in the entity hierarchy
/// Used to determine which system logo to display
/// </summary>
[HttpGet("{id}/distributor")]
public async Task<IActionResult> GetDistributorLogo(int id)
{
    try
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        _logger.LogInformation("Getting distributor logo for entity {EntityId}", id);

        // Find the highest owner (root entity) in the hierarchy
        var currentEntity = await _context.Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (currentEntity == null)
        {
            return NotFound(new { message = "גוף לא נמצא" });
        }

        // Traverse up the ownership hierarchy to find the root owner
        var rootEntity = currentEntity;
        var visitedIds = new HashSet<int> { id }; // Prevent infinite loops
        var maxIterations = 50; // Safety limit
        var iterations = 0;

        while (rootEntity.OwnerId.HasValue && iterations < maxIterations)
        {
            iterations++;
            
            if (visitedIds.Contains(rootEntity.OwnerId.Value))
            {
                _logger.LogWarning("Circular ownership detected for entity {EntityId}", id);
                break;
            }

            var ownerEntity = await _context.Entities
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == rootEntity.OwnerId.Value);

            if (ownerEntity == null)
            {
                _logger.LogWarning("Owner entity {OwnerId} not found", rootEntity.OwnerId.Value);
                break;
            }

            visitedIds.Add(rootEntity.OwnerId.Value);
            rootEntity = ownerEntity;
        }

        _logger.LogInformation("Root entity for {EntityId} is {RootEntityId} with distributor '{Distributor}'", 
            id, rootEntity.Id, rootEntity.Distributor ?? "null");



        return Ok(new
        {
            distributor = rootEntity.Distributor ?? ""
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting distributor logo for entity {EntityId}", id);
        return StatusCode(500, new { 
            message = "שגיאה בטעינת מפיץ", 
            error = ex.Message 
        });
    }
}

// ✅ Helper method to format address
private static string FormatSchoolAddress(string? street, string? houseNumber, string? city, string? postCode)
{
    var parts = new List<string>();

    if (!string.IsNullOrWhiteSpace(street))
    {
        var streetPart = street.Trim();
        if (!string.IsNullOrWhiteSpace(houseNumber))
        {
            streetPart += " " + houseNumber.Trim();
        }
        parts.Add(streetPart);
    }

    if (!string.IsNullOrWhiteSpace(city))
    {
        parts.Add(city.Trim());
    }

    if (!string.IsNullOrWhiteSpace(postCode) && !IsAllZeros(postCode))
    {
        parts.Add(postCode.Trim());
    }

    return string.Join(", ", parts);
}

// ✅ Helper method to format person name
private static string FormatPersonName(string? firstName, string? lastName)
{
    var first = firstName?.Trim() ?? string.Empty;
    var last = lastName?.Trim() ?? string.Empty;

    return $"{first} {last}".Trim();
}


private static bool IsAllZeros(string value)
{
    return !string.IsNullOrWhiteSpace(value) && value.Trim().All(c => c == '0');
}


// List non-school entities
        [HttpGet("non-schools")]
        public async Task<IActionResult> GetNonSchoolEntities()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var notAllowedTypes = new[] { 1,4 }; // school entity types

                var entities = await _context.Entities
                    .AsNoTracking()
                    .Where(e => e.IsActive && !notAllowedTypes.Contains(e.EntityTypeId))
                    .Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        entityTypeId = e.EntityTypeId,
                        entityTypeDescription = _context.EntityTypes
                            .Where(t => t.Id == e.EntityTypeId)
                            .Select(t => t.Name)
                            .FirstOrDefault(),
                        ownerId = e.OwnerId,
                        ownerName = _context.Entities
                            .Where(o => o.Id == e.OwnerId)
                            .Select(o => o.Name)
                            .FirstOrDefault(),
                        isActive = e.IsActive
                    })
                    .OrderBy(e => e.name)
                    .ToListAsync();

                return Ok(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading non-school entities");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת ישויות", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateEntity([FromBody] CreateEntityDto dto)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (string.IsNullOrWhiteSpace(dto.Name))
                    return BadRequest(new { success = false, message = "שם ישות נדרש" });

                // Validate allowed type
                var allowedTypes = new[] { 3, 5, 6 };
                if (!allowedTypes.Contains(dto.EntityTypeId))
                    return BadRequest(new { success = false, message = "סוג ישות לא נתמך" });

                // Owner can be provided; otherwise default to current session entity
                int ownerId;
                if (dto.OwnerId.HasValue)
                    ownerId = dto.OwnerId.Value;
                else if (!int.TryParse(session.EntityId, out ownerId))
                    return BadRequest(new { success = false, message = "בעלות לא תקינה" });

                var newEntity = new Entity
                {
                    Name = dto.Name.Trim(),
                    EntityTypeId = dto.EntityTypeId,
                    OwnerId = ownerId,
                    IsActive = true
                };

                _context.Entities.Add(newEntity);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "ישות נוצרה בהצלחה",
                    data = new { entityId = newEntity.Id, name = newEntity.Name }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating entity");
                return StatusCode(500, new { success = false, message = "שגיאה ביצירת ישות", error = ex.Message });
            }
        }



        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEntity(int id, [FromBody] UpdateEntityDto dto)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == id);
                if (entity == null)
                    return NotFound(new { success = false, message = "ישות לא נמצאה" });

                if (!string.IsNullOrWhiteSpace(dto.Name))
                    entity.Name = dto.Name.Trim();

                if (dto.EntityTypeId.HasValue)
                {
                    var allowedTypes = new[] { 3, 5, 6 };
                    if (!allowedTypes.Contains(dto.EntityTypeId.Value))
                        return BadRequest(new { success = false, message = "סוג ישות לא נתמך" });
                    entity.EntityTypeId = dto.EntityTypeId.Value;
                }

                if (dto.OwnerId.HasValue)
                    entity.OwnerId = dto.OwnerId.Value;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "ישות עודכנה בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity {EntityId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון ישות", error = ex.Message });
            }
        }

        // Soft delete (set inactive)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEntity(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == id);
                if (entity == null)
                    return NotFound(new { success = false, message = "ישות לא נמצאה" });

                entity.IsActive = false;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "הישות סומנה כלא פעילה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity {EntityId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה במחיקת ישות", error = ex.Message });
            }
        }

        // Get entity details by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEntity(int id)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { message = "Authentication required" });
                }

                var entity = await _context.Entities
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (entity == null)
                {
                    return NotFound(new { message = "גוף לא נמצא" });
                }

                return Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entity with ID {EntityId}", id);
                return StatusCode(500, new { message = "שגיאה בטעינת פרטי הגוף", error = ex.Message });
            }
        }

        /// <summary>
        /// Create a new school entity with all related records (entity, school_year, school)
        /// </summary>
        [HttpPost("create-school")]
        public async Task<IActionResult> CreateSchool([FromBody] CreateSchoolDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "Authentication required" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    return BadRequest(new { success = false, message = "Invalid session entity ID" });
                }

                if (!int.TryParse(session.UserId, out int userId))
                {
                    return BadRequest(new { success = false, message = "Invalid user ID" });
                }

                _logger.LogInformation("Creating new school: {Name} for owner {OwnerId}", dto.Name, dto.OwnerId);

                // Verify owner exists
                var ownerEntity = await _context.Entities.FindAsync(dto.OwnerId);
                if (ownerEntity == null)
                {
                    return BadRequest(new { success = false, message = "גוף בעלים לא נמצא" });
                }

                // Get Selected year details from session
                var SelectedYearId = session.GetProperty("SelectedYearId");
                var SelectedYearValue = session.GetProperty("SelectedYearValue");

                if (string.IsNullOrEmpty(SelectedYearId) || string.IsNullOrEmpty(SelectedYearValue))
                {
                    return BadRequest(new { success = false, message = "לא נבחרה שנת לימודים" });
                }

                if (!int.TryParse(SelectedYearId, out int yearId))
                {
                    return BadRequest(new { success = false, message = "Invalid year ID" });
                }

                // STEP 1: Create Entity record
                var newEntity = new Entity
                {
                    Name = dto.Name,
                    EntityTypeId = dto.EntityTypeId,
                    OwnerId = dto.OwnerId,
                    IsActive = true
                };

                _context.Entities.Add(newEntity);
                await _context.SaveChangesAsync(); // Save to get generated ID

                _logger.LogInformation("Created entity with ID {EntityId}", newEntity.Id);

                // STEP 2: Create SchoolYear record
                var newSchoolYear = new SchoolYear
                {
                    SchoolId = newEntity.Id,
                    YearId = yearId,
                    YearName = SelectedYearValue,
                    IsCurrent = true,
                    Status = 1,
                    StartDate = DateTime.UtcNow, // TODO: Get actual dates from hebrew_years table
                    EndDate = DateTime.UtcNow.AddYears(1)
                };

                _context.SchoolYears.Add(newSchoolYear);
                await _context.SaveChangesAsync(); // Save to get generated ID

                _logger.LogInformation("Created school year with ID {SchoolYearId}", newSchoolYear.Id);

                // STEP 3: Create School record (version 1)
                var newSchool = new School
                {
                    EntityId = newEntity.Id,
                    SchoolYearId = newSchoolYear.Id,
                    EntityTypeId = dto.EntityTypeId,
                    Name = dto.Name,
                    Owner = dto.OwnerId,
                    IsActive = true,
                    IsLastVersion = true,
                    Version = 1
                };

                _context.Schools.Add(newSchool);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created school record with ID {SchoolId}", newSchool.Id);

                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "בית הספר נוצר בהצלחה",
                    data = new
                    {
                        entityId = newEntity.Id,
                        schoolYearId = newSchoolYear.Id,
                        schoolId = newSchool.Id,
                        name = newEntity.Name
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating school");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת בית הספר",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all entity types for dropdowns
        /// Used by school creation and entity management forms
        /// </summary>
        [HttpGet("entity-types")]
        public async Task<IActionResult> GetEntityTypes()
        {
            try
            {
                _logger.LogInformation("🔍 GetEntityTypes endpoint called");

                // Session check using BaseController method
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("⚠️ No session found for GetEntityTypes request");
                    return Unauthorized(new { success = false, message = "Authentication required" });
                }

                _logger.LogInformation("✅ Session found: UserId={UserId}, EntityId={EntityId}",
                    session.UserId, session.EntityId);

                var entityTypes = await _context.EntityTypes
                    .AsNoTracking()
                    .OrderBy(et => et.Name)
                    .Select(et => new
                    {
                        id = et.Id,
                        name = et.Name
                    })
                    .ToListAsync();

                _logger.LogInformation("✅ Loaded {Count} entity types", entityTypes.Count);

                return Ok(entityTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading entity types");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת סוגי גופים",
                    error = ex.Message
                });
            }
        }

/// <summary>
/// Get filtered entities for owner dropdown based on user permissions
/// - Admin (userId = 1): All active entities with types 2,3,5,6
/// - School entity (types 1,4): Only current entity (locked dropdown)
/// - Network entity (types 2,3,5,6): Only networks owned by current entity
/// Used by: school creation modal, entity details editing
/// </summary>
[HttpGet("owner-options")]
public async Task<IActionResult> GetOwnerOptions()
{
    try
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            _logger.LogWarning("No session found for owner options request");
            return Unauthorized(new { success = false, message = "נדרש אימות" });
        }

        if (!int.TryParse(session.EntityId, out int sessionEntityId))
        {
            _logger.LogError("Invalid EntityId in session: {EntityId}", session.EntityId);
            return BadRequest(new { success = false, message = "Invalid session entity ID" });
        }

        if (!int.TryParse(session.EntityTypeId, out int entityTypeId))
        {
            _logger.LogError("Invalid EntityTypeId in session: {EntityTypeId}", session.EntityTypeId);
            return BadRequest(new { success = false, message = "Invalid entity type ID" });
        }

        bool isAdmin = session.UserId == "1";
        
        _logger.LogInformation("GetOwnerOptions - UserId: {UserId}, IsAdmin: {IsAdmin}, EntityId: {EntityId}, EntityTypeId: {EntityTypeId}", 
            session.UserId, isAdmin, sessionEntityId, entityTypeId);

        List<object> ownerOptions;
        bool isLocked = false;

        if (isAdmin)
        {
            // Admin: Return all active entities with network types (2,3,5,6)
            var allowedTypes = new[] { 2, 3, 5, 6 };
            
            ownerOptions = await _context.Entities
                .AsNoTracking()
                .Where(e => e.IsActive && allowedTypes.Contains(e.EntityTypeId))
                .OrderBy(e => e.Name)
                .Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    entityTypeId = e.EntityTypeId
                })
                .Cast<object>()
                .ToListAsync();

            _logger.LogInformation("Admin user: returning {Count} network entities", ownerOptions.Count);
        }
        else if (entityTypeId == 1 || entityTypeId == 4)
        {
            // School entity: Only return current entity (locked)
            var currentEntity = await _context.Entities
                .AsNoTracking()
                .Where(e => e.Id == sessionEntityId)
                .Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    entityTypeId = e.EntityTypeId
                })
                .FirstOrDefaultAsync();

            if (currentEntity == null)
            {
                return BadRequest(new { success = false, message = "גוף נוכחי לא נמצא" });
            }

            ownerOptions = new List<object> { currentEntity };
            isLocked = true;
            
            _logger.LogInformation("School entity: returning locked current entity {EntityId}", sessionEntityId);
        }
        else if (entityTypeId == 5) 
        {      ownerOptions = await _context.Entities
                .AsNoTracking()
                .Where(e => e.IsActive && 
                           e.Id == sessionEntityId)
                .Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    entityTypeId = e.EntityTypeId
                })
                .Cast<object>()
                .ToListAsync();
            
            _logger.LogInformation("Own entity: returning {Count}  networks", ownerOptions.Count);
  
        }
        else if (entityTypeId == 2 || entityTypeId == 3 ||  entityTypeId == 6)
        {
            // Network entity: Return networks owned by current entity (types 2,3,5,6)
            var allowedTypes = new[] { 2, 3, 5, 6 };
            
            ownerOptions = await _context.Entities
                .AsNoTracking()
                .Where(e => e.IsActive && 
                           e.OwnerId == sessionEntityId && 
                           allowedTypes.Contains(e.EntityTypeId))
                .OrderBy(e => e.Name)
                .Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    entityTypeId = e.EntityTypeId
                })
                .Cast<object>()
                .ToListAsync();

            _logger.LogInformation("Network entity: returning {Count} owned networks", ownerOptions.Count);
        }
        else
        {
            // Unknown entity type: return empty list
            ownerOptions = new List<object>();
            _logger.LogWarning("Unknown entity type {EntityTypeId}, returning empty list", entityTypeId);
        }

        return Ok(new
        {
            success = true,
            ownerOptions = ownerOptions,
            isLocked = isLocked
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading owner options");
        return StatusCode(500, new
        {
            success = false,
            message = "שגיאה בטעינת אפשרויות בעלים",
            error = ex.Message
        });
    }
}
        /// <summary>
        /// Get council summary by year - shows number of students and total requested amount per council
        /// </summary>
        [HttpGet("councils/summary")]
        public async Task<IActionResult> GetCouncilSummary([FromQuery] int? yearId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogWarning("No valid session found for council summary request");
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                if (!int.TryParse(session.EntityId, out int sessionEntityId))
                {
                    _logger.LogError("Invalid EntityId in session: {EntityId}", session.EntityId);
                    return BadRequest(new { success = false, message = "Invalid session entity ID" });
                }

                // Check if user is admin (userId = 1)
                bool isAdmin = session.UserId == "1";
                _logger.LogInformation("GetCouncilSummary request - IsAdmin: {IsAdmin}, SessionEntityId: {EntityId}", 
                    isAdmin, sessionEntityId);
        
                // Get SelectedYearId from session if not provided
                if (!yearId.HasValue)
                {
                    var selectedYearIdStr = session.GetProperty("SelectedYearId");
                    if (!string.IsNullOrEmpty(selectedYearIdStr) && int.TryParse(selectedYearIdStr, out int selectedYearId))
                    {
                        yearId = selectedYearId;
                    }
                }
        
                if (!yearId.HasValue)
                {
                    _logger.LogError("No year ID provided or found in session");
                    return BadRequest(new { success = false, message = "נדרש מזהה שנה" });
                }
        
                _logger.LogInformation("Loading council summary for year {YearId}", yearId.Value);

                // Get entity IDs owned by current user's entity (for hierarchical filtering)
                var ownedEntityIds = await _context.Entities
                    .AsNoTracking()
                    .Where(e => e.Owner.Id == sessionEntityId)
                    .Select(e => e.Id)
                    .ToListAsync();
                    
                _logger.LogInformation("Found {Count} entities owned by current user for council filtering", ownedEntityIds.Count);
        
                // Apply ownership filtering and aggregate results
                // The view may have multiple rows per council (one per owner), so we need to group
                var councilData = await _context.CouncilSummaryVw
                    .AsNoTracking()
                    .Where(cs => cs.YearId == yearId.Value &&
                           (isAdmin || 
                            cs.OwnerId == sessionEntityId || 
                            (cs.OwnerId.HasValue && ownedEntityIds.Contains(cs.OwnerId.Value))))
                    .ToListAsync();

                // Aggregate by council (sum students and costs across all owners)
                var councilSummary = councilData
                    .GroupBy(cs => new { cs.CouncilId, cs.CouncilName })
                    .Select(g => new
                    {
                        id = g.Key.CouncilId,
                        councilName = g.Key.CouncilName ?? "לא ידוע",
                        numberOfStudents = g.Sum(x => x.NumberOfStudents),
                        totalRequested = g.Sum(x => x.TotalRequestedAmount),
                        totalRequestedFormatted = g.Sum(x => x.TotalRequestedAmount).ToString("N2") + " ₪"
                    })
                    .OrderBy(cs => cs.councilName)
                    .ToList();
        
                _logger.LogInformation("Found {Count} councils with students for year {YearId} after ownership filtering", 
                    councilSummary.Count, yearId.Value);
        
                return Ok(new
                {
                    success = true,
                    yearId = yearId.Value,
                    data = councilSummary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading council summary");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת סיכום רשויות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Batch create entities for councils that appear in school_students but don't have an entity yet
        /// Only creates entities for councils without existing entities
        /// </summary>
        [HttpPost("batch-create-councils")]
        public async Task<IActionResult> BatchCreateCouncilEntities()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                _logger.LogInformation("🔄 Starting batch creation of council entities");

                // Step 1: Get all councils that appear in school_students
                var councilsInStudents = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(ss => ss.SendingCouncil.HasValue && ss.IsLastVersion)
                    .Select(ss => ss.SendingCouncil.Value)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation("Found {Count} councils in school_students", councilsInStudents.Count);

                // Step 2: Get councils that don't have entities yet
                var existingCouncilEntityIds = await _context.Entities
                    .AsNoTracking()
                    .Where(e => e.EntityTypeId == 2 && e.IsActive)  // Entity type 2 is council
                    .Select(e => e.CouncilId.HasValue ? e.CouncilId.Value : -1)
                    .ToListAsync();

                var councilsNeedingEntities = councilsInStudents
                    .Where(c => !existingCouncilEntityIds.Contains(c))
                    .ToList();

                _logger.LogInformation("Found {Count} councils needing entities", councilsNeedingEntities.Count);

                if (!councilsNeedingEntities.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "כל הרשויות כבר קיימות",
                        createdCount = 0,
                        skippedCount = councilsInStudents.Count
                    });
                }

                // Step 3: Get council details
                var councilDetails = await _context.Councils
                    .AsNoTracking()
                    .Where(c => councilsNeedingEntities.Contains(c.Id))
                    .ToListAsync();

                _logger.LogInformation("Retrieved details for {Count} councils", councilDetails.Count);

                // Step 4: Create entities for each council
                int createdCount = 0;
                foreach (var council in councilDetails)
                {
                    var newEntity = new Entity
                    {
                        Name = council.Name,
                        EntityTypeId = 2,  // Council type
                        CouncilId = council.Id,
                        IsActive = true,
                        OwnerId = null  // No owner for system-created council entities
                    };

                    _context.Entities.Add(newEntity);
                    _logger.LogInformation("Creating entity for council: {CouncilName} (ID: {CouncilId})", 
                        council.Name, council.Id);
                    createdCount++;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Successfully created {Count} council entities", createdCount);

                return Ok(new
                {
                    success = true,
                    message = $"נוצרו {createdCount} ישויות עבור רשויות",
                    createdCount = createdCount,
                    skippedCount = councilsInStudents.Count - createdCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in batch creation of council entities");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת ישויות רשויות",
                    error = ex.Message
                });
            }
        }

    }


}