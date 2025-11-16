using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Session;
using PetelApp.Api.Controllers;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TablePermissionsController : BaseController
    {
        public TablePermissionsController(
            UserSessionService userSessionService,
            ILogger<TablePermissionsController> logger)
            : base(userSessionService, logger)
        {
        }

        [HttpPost]
        public IActionResult ValidatePermissions([FromBody] TablePermissionRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                
                // Stub: Return all columns as updatable for now
                var permissions = request.Columns.Select(col => new
                {
                    columnKey = col.Key,
                    canUpdate = !col.RequestedPermission.Equals("readonly", StringComparison.OrdinalIgnoreCase)
                }).ToList();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating table permissions");
                return StatusCode(500, new { message = "שגיאה באימות הרשאות" });
            }
        }

        [HttpPost("validateUpdate")]
        public IActionResult ValidateUpdate([FromBody] UpdateValidationRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                
                // Stub: Approve all updates for now
                return Ok(new
                {
                    success = true,
                    message = "עדכון אושר"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating update");
                return StatusCode(500, new { message = "שגיאה באימות עדכון" });
            }
        }

        [HttpPost("saveTableChanges")]
        public IActionResult SaveTableChanges([FromBody] SaveChangesRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                
                // Stub: Return success for now
                return Ok(new
                {
                    success = true,
                    message = "שינויים נשמרו בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving table changes");
                return StatusCode(500, new { message = "שגיאה בשמירת שינויים" });
            }
        }

        [HttpPost("validateExport")]
        public IActionResult ValidateExport([FromBody] ExportValidationRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                
                // Stub: Allow all exports for now
                return Ok(new
                {
                    canExport = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating export");
                return StatusCode(500, new { message = "שגיאה באימות ייצוא" });
            }
        }
    }

    // Request models
    public class TablePermissionRequest
    {
        public string TableName { get; set; } = string.Empty;
        public List<ColumnPermission> Columns { get; set; } = new();
    }

    public class ColumnPermission
    {
        public string Key { get; set; } = string.Empty;
        public string RequestedPermission { get; set; } = string.Empty;
    }

    public class UpdateValidationRequest
    {
        public object RowId { get; set; } = null!;
        public string ColumnKey { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
    }

    public class SaveChangesRequest
    {
        public string TableName { get; set; } = string.Empty;
        public List<ChangeRecord> Changes { get; set; } = new();
        public object OriginalData { get; set; } = null!;
    }

    public class ChangeRecord
    {
        public object RowId { get; set; } = null!;
        public string ColumnKey { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
    }

    public class ExportValidationRequest
    {
        public string TableName { get; set; } = string.Empty;
    }
}