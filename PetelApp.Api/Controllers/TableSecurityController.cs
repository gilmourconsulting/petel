[ApiController]
[Route("api/[controller]")]
public class TableSecurityController : BaseController
{
    private readonly UserSessionService _userSessionService;
    private readonly ILogger<TableSecurityController> _logger;

    public TableSecurityController(
        UserSessionService userSessionService,
        ILogger<TableSecurityController> logger)
    {
        _userSessionService = userSessionService;
        _logger = logger;
    }

    [HttpPost("permissions")]
    public async Task<IActionResult> ValidateTablePermissions([FromBody] TablePermissionRequest request)
    {
        try
        {
            var userId = _userSessionService.GetUserId();
            var tenantId = GetTenantId();
            
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
            {
                return Unauthorized("Invalid session");
            }

            var permissions = new List<ColumnPermission>();
            
            foreach (var column in request.Columns)
            {
                var canUpdate = await ValidateColumnUpdatePermission(
                    userId, tenantId, request.TableName, column.Key);
                
                permissions.Add(new ColumnPermission
                {
                    ColumnKey = column.Key,
                    CanUpdate = canUpdate,
                    CanView = true // Assume view is always allowed if they can see the table
                });
            }

            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating table permissions");
            return StatusCode(500, "Permission validation failed");
        }
    }

    [HttpPost("validate-update")]
    public async Task<IActionResult> ValidateUpdate([FromBody] UpdateValidationRequest request)
    {
        try
        {
            var userId = _userSessionService.GetUserId();
            var tenantId = GetTenantId();
            
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
            {
                return Unauthorized("Invalid session");
            }

            // Validate the user has permission to update this column
            var canUpdate = await ValidateColumnUpdatePermission(
                userId, tenantId, request.TableName, request.ColumnKey);
            
            if (!canUpdate)
            {
                return Ok(new UpdateValidationResponse 
                { 
                    Success = false, 
                    Message = "אין הרשאה לעדכן עמודה זו" 
                });
            }

            // Validate the data (business rules, data types, etc.)
            var validationResult = await ValidateUpdateData(request);
            
            if (!validationResult.IsValid)
            {
                return Ok(new UpdateValidationResponse 
                { 
                    Success = false, 
                    Message = validationResult.ErrorMessage 
                });
            }

            // Log the update attempt
            _logger.LogInformation("User {UserId} updating {Table}.{Column} from '{OldValue}' to '{NewValue}'",
                userId, request.TableName, request.ColumnKey, request.OldValue, request.NewValue);

            return Ok(new UpdateValidationResponse { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating update");
            return Ok(new UpdateValidationResponse 
            { 
                Success = false, 
                Message = "שגיאה בעדכון" 
            });
        }
    }

    [HttpPost("validate-export")]
    public async Task<IActionResult> ValidateExport([FromBody] ExportValidationRequest request)
    {
        try
        {
            var userId = _userSessionService.GetUserId();
            var tenantId = GetTenantId();
            
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
            {
                return Unauthorized("Invalid session");
            }

            var canExport = await ValidateExportPermission(userId, tenantId, request.TableName);
            
            // Log export attempt
            _logger.LogInformation("User {UserId} attempted to export {Table}. Allowed: {Allowed}",
                userId, request.TableName, canExport);

            return Ok(new { CanExport = canExport });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating export");
            return Ok(new { CanExport = false });
        }
    }

    private async Task<bool> ValidateColumnUpdatePermission(string userId, string tenantId, string tableName, string columnKey)
    {
        // Implement your permission logic here
        // This could check user roles, specific column permissions, etc.
        
        // Example: Check if user has admin role or specific column permissions
        // You might want to check against a permissions table in your database
        
        return true; // Placeholder - implement your logic
    }

    private async Task<ValidationResult> ValidateUpdateData(UpdateValidationRequest request)
    {
        // Implement data validation rules
        // Check data types, business rules, constraints, etc.
        
        return new ValidationResult { IsValid = true }; // Placeholder
    }

    private async Task<bool> ValidateExportPermission(string userId, string tenantId, string tableName)
    {
        // Implement export permission logic
        return true; // Placeholder
    }
}

// DTOs
public class TablePermissionRequest
{
    public string TableName { get; set; }
    public List<ColumnRequest> Columns { get; set; }
}

public class ColumnRequest
{
    public string Key { get; set; }
    public string RequestedPermission { get; set; }
}

public class ColumnPermission
{
    public string ColumnKey { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanView { get; set; }
}

public class UpdateValidationRequest
{
    public string RowId { get; set; }
    public string ColumnKey { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string TableName { get; set; }
    public string SessionToken { get; set; }
}

public class UpdateValidationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
}

public class ExportValidationRequest
{
    public string TableName { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
}