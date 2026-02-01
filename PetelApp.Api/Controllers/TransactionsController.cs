using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Models;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : BaseController
    {
        private readonly AppDbContext _context;

        public TransactionsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<TransactionsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get transactions for an account with optional filters
        /// </summary>
        [HttpGet("account/{accountId}")]
        public async Task<IActionResult> GetTransactionsByAccount(
            int accountId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? transactionTypeId,
            [FromQuery] int? schoolYearId,
            [FromQuery] int? relatedStudentId,
            [FromQuery] decimal? minAmount,
            [FromQuery] decimal? maxAmount)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                // Verify account exists and user has access
                var account = await _context.TransactionAccounts
                    .FirstOrDefaultAsync(a => a.Id == accountId);

                if (account == null)
                {
                    return NotFound(new { success = false, message = "חשבון לא נמצא" });
                }

                var query = _context.Transactions
                    .AsNoTracking()
                    .Include(t => t.TransactionType)
                    .Include(t => t.RelatedStudent)
                    .Include(t => t.SchoolYear)
                    .Include(t => t.User)
                    .Where(t => t.AccountId == accountId);

                // Apply filters
                if (startDate.HasValue)
                    query = query.Where(t => t.TransactionDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(t => t.TransactionDate <= endDate.Value);

                if (transactionTypeId.HasValue)
                    query = query.Where(t => t.TransactionTypeId == transactionTypeId.Value);

                if (schoolYearId.HasValue)
                    query = query.Where(t => t.SchoolYearId == schoolYearId.Value);

                if (relatedStudentId.HasValue)
                    query = query.Where(t => t.RelatedStudentId == relatedStudentId.Value);

                if (minAmount.HasValue)
                    query = query.Where(t => t.Amount >= minAmount.Value);

                if (maxAmount.HasValue)
                    query = query.Where(t => t.Amount <= maxAmount.Value);

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.Id)
                    .Select(t => new
                    {
                        t.Id,
                        t.AccountId,
                        t.TransactionTypeId,
                        TransactionTypeName = t.TransactionType.Name,
                        TransactionTypeDescription = t.TransactionType.Description,
                        IsCredit = t.TransactionType.IsCredit,
                        t.TransactionDate,
                        t.Amount,
                        t.Description,
                        t.RelatedTransactionId,
                        t.RelatedStudentId,
                        RelatedStudentName = t.RelatedStudent != null 
                            ? $"{t.RelatedStudent.FirstName} {t.RelatedStudent.LastName}" 
                            : null,
                        t.SchoolYearId,
                        SchoolYearName = t.SchoolYear != null ? t.SchoolYear.HebrewYearText : null,
                        t.UserId,
                        Username = t.User.Username,
                        t.CreatedAt
                    })
                    .ToListAsync();

                return Ok(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transactions for account {AccountId}", accountId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת עסקאות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get transaction details with full details breakdown
        /// </summary>
        [HttpGet("{transactionId}/details")]
        public async Task<IActionResult> GetTransactionWithDetails(int transactionId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var transaction = await _context.Transactions
                    .AsNoTracking()
                    .Include(t => t.TransactionType)
                    .Include(t => t.RelatedStudent)
                    .Include(t => t.SchoolYear)
                    .Include(t => t.User)
                    .Include(t => t.TransactionDetails)
                        .ThenInclude(td => td.DetailType)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                if (transaction == null)
                {
                    return NotFound(new { success = false, message = "עסקה לא נמצאה" });
                }

                var result = new
                {
                    Transaction = new
                    {
                        transaction.Id,
                        transaction.AccountId,
                        transaction.TransactionTypeId,
                        TransactionTypeName = transaction.TransactionType.Name,
                        TransactionTypeDescription = transaction.TransactionType.Description,
                        IsCredit = transaction.TransactionType.IsCredit,
                        transaction.TransactionDate,
                        transaction.Amount,
                        transaction.Description,
                        transaction.RelatedTransactionId,
                        transaction.RelatedStudentId,
                        RelatedStudentName = transaction.RelatedStudent != null 
                            ? $"{transaction.RelatedStudent.FirstName} {transaction.RelatedStudent.LastName}" 
                            : null,
                        transaction.SchoolYearId,
                        SchoolYearName = transaction.SchoolYear != null ? transaction.SchoolYear.HebrewYearText : null,
                        transaction.UserId,
                        Username = transaction.User.Username,
                        transaction.CreatedAt
                    },
                    Details = transaction.TransactionDetails.Select(td => new
                    {
                        td.Id,
                        td.TransactionId,
                        td.DetailTypeId,
                        DetailTypeName = td.DetailType.Name,
                        DetailTypeDescription = td.DetailType.Description,
                        td.Description,
                        td.Amount
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transaction details for transaction {TransactionId}", transactionId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת פרטי עסקה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Create a new transaction with details
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

                // Validation: Must have at least one detail
                if (request.Details == null || request.Details.Count == 0)
                {
                    return BadRequest(new 
                    { 
                        success = false, 
                        message = "חובה להזין לפחות פירוט אחד לעסקה" 
                    });
                }

                // Validation: Sum of details must equal transaction amount
                var detailsSum = request.Details.Sum(d => d.Amount);
                if (Math.Abs(detailsSum - request.Amount) > 0.01m) // Allow for rounding differences
                {
                    return BadRequest(new 
                    { 
                        success = false, 
                        message = $"סכום הפירוטים ({detailsSum:N2}) חייב להיות שווה לסכום העסקה ({request.Amount:N2})" 
                    });
                }

                // Verify account exists
                var account = await _context.TransactionAccounts
                    .FirstOrDefaultAsync(a => a.Id == request.AccountId);

                if (account == null)
                {
                    return NotFound(new { success = false, message = "חשבון לא נמצא" });
                }

                // Create transaction
                var transaction = new Transaction
                {
                    AccountId = request.AccountId,
                    TransactionTypeId = request.TransactionTypeId,
                    TransactionDate = request.TransactionDate,
                    Amount = request.Amount,
                    Description = request.Description,
                    RelatedTransactionId = request.RelatedTransactionId,
                    RelatedStudentId = request.RelatedStudentId,
                    SchoolYearId = request.SchoolYearId,
                    UserId = userId ?? 0, // Should never be null due to session check
                    CreatedAt = DateTime.UtcNow,
                    CreatedUser = userId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdateUser = userId
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                // Create transaction details
                foreach (var detailRequest in request.Details)
                {
                    var detail = new TransactionDetail
                    {
                        TransactionId = transaction.Id,
                        DetailTypeId = detailRequest.DetailTypeId,
                        Description = detailRequest.Description,
                        Amount = detailRequest.Amount,
                        CreatedAt = DateTime.UtcNow,
                        CreatedUser = userId,
                        UpdatedAt = DateTime.UtcNow,
                        UpdateUser = userId
                    };

                    _context.TransactionDetails.Add(detail);
                }

                await _context.SaveChangesAsync();

                // Update account balance
                var transactionType = await _context.TransactionTypes
                    .FirstOrDefaultAsync(tt => tt.Id == request.TransactionTypeId);

                if (transactionType != null)
                {
                    if (transactionType.IsCredit)
                        account.Balance += request.Amount;
                    else
                        account.Balance -= request.Amount;

                    account.UpdatedAt = DateTime.UtcNow;
                    account.UpdateUser = userId;
                    await _context.SaveChangesAsync();
                }

                return Ok(new 
                { 
                    success = true, 
                    transactionId = transaction.Id,
                    message = "העסקה נוצרה בהצלחה" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת עסקה",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all transaction types
        /// </summary>
        [HttpGet("types")]
        public async Task<IActionResult> GetTransactionTypes()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var types = await _context.TransactionTypes
                    .AsNoTracking()
                    .Where(tt => tt.IsActive)
                    .OrderBy(tt => tt.Description)
                    .Select(tt => new
                    {
                        tt.Id,
                        tt.Name,
                        tt.Description,
                        tt.IsCredit,
                        tt.IsActive
                    })
                    .ToListAsync();

                return Ok(types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transaction types");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת סוגי עסקאות",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all transaction detail types
        /// </summary>
        [HttpGet("detail-types")]
        public async Task<IActionResult> GetTransactionDetailTypes()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var types = await _context.TransactionDetailTypes
                    .AsNoTracking()
                    .Where(tdt => tdt.IsActive)
                    .OrderBy(tdt => tdt.Description)
                    .Select(tdt => new
                    {
                        tdt.Id,
                        tdt.Name,
                        tdt.Description,
                        tdt.IsActive
                    })
                    .ToListAsync();

                return Ok(types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transaction detail types");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת סוגי פירוט עסקאות",
                    error = ex.Message
                });
            }
        }
    }

    public class CreateTransactionRequest
    {
        public int AccountId { get; set; }
        public int TransactionTypeId { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Today;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? RelatedTransactionId { get; set; }
        public int? RelatedStudentId { get; set; }
        public int? SchoolYearId { get; set; }
        public List<CreateTransactionDetailRequest> Details { get; set; } = new();
    }

    public class CreateTransactionDetailRequest
    {
        public int DetailTypeId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
