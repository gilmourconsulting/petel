using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs.Meitar;
using PetelAssistants.Api.Models;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeitarDataController : BaseController
    {
        private readonly AssistDbContext _context;
        private readonly IMeitarDataService _meitarDataService;

        public MeitarDataController(
            AssistDbContext context,
            IMeitarDataService meitarDataService,
            UserSessionService sessionService,
            ILogger<MeitarDataController> logger)
            : base(sessionService, logger)
        {
            _context = context;
            _meitarDataService = meitarDataService;
        }

        [HttpGet("period-exists")]
        public async Task<IActionResult> PeriodExists([FromQuery] int year, [FromQuery] int month)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            var (exists, rowCount, totalCalculated) = await GetPeriodStatsAsync(year, month);
            return Ok(new
            {
                success = true,
                exists,
                rowCount,
                totalCalculated
            });
        }

        [HttpPost("retrieve")]
        public async Task<IActionResult> Retrieve([FromBody] MeitarRetrieveRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            if (request.PeriodMonth < 1 || request.PeriodMonth > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            try
            {
                var symbolCode = await _meitarDataService.GetSymbolCodeForEntityAsync(entityId);
                if (string.IsNullOrWhiteSpace(symbolCode))
                    return BadRequest(new { success = false, message = "לא הוגדר קוד מוטב (symbol_code) לרשות הנוכחית." });

                var (exists, existingCount, _) = await GetPeriodStatsAsync(request.PeriodYear, request.PeriodMonth);
                if (exists && !request.ReplaceExisting)
                {
                    return Conflict(new
                    {
                        success = false,
                        periodExists = true,
                        rowCount = existingCount,
                        message = "קיימים נתוני מייתר לתקופה זו. יש לאשר החלפה."
                    });
                }

                var queryResult = await _meitarDataService.QueryMutavimForSymbolAndPeriodAsync(
                    symbolCode,
                    request.PeriodYear,
                    request.PeriodMonth);

                if (!queryResult.Success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = queryResult.Message ?? "שליפת נתוני מייתר נכשלה."
                    });
                }

                var now = DateTime.UtcNow;
                var process = new MeitarRetrieveProcess
                {
                    EntityId = entityId,
                    PeriodYear = request.PeriodYear,
                    PeriodMonth = request.PeriodMonth,
                    Source = "meitar",
                    CreatedAt = now,
                    UserId = userId,
                    UpdatedAt = now,
                    UpdateUser = userId
                };
                _context.MeitarRetrieveProcesses.Add(process);
                await _context.SaveChangesAsync();

                if (request.ReplaceExisting && exists)
                {
                    var oldRows = await _context.MeitarMutavim
                        .Where(m => m.PeriodYear == request.PeriodYear && m.PeriodMonth == request.PeriodMonth)
                        .ToListAsync();
                    if (oldRows.Count > 0)
                    {
                        _context.MeitarMutavim.RemoveRange(oldRows);
                        await _context.SaveChangesAsync();
                    }
                }

                decimal sum = 0;
                var inserted = 0;
                var skipped = 0;

                foreach (var row in queryResult.Rows)
                {
                    if (!TryMapRow(row, out var mapped))
                    {
                        skipped++;
                        continue;
                    }

                    sum += mapped.CalculatedAmount;
                    _context.MeitarMutavim.Add(new MeitarMutavim
                    {
                        EntityId = entityId,
                        PeriodYear = request.PeriodYear,
                        PeriodMonth = request.PeriodMonth,
                        BeneficiaryCode = mapped.BeneficiaryCode,
                        CalcDate = mapped.CalcDate,
                        TopicCode = mapped.TopicCode,
                        TopicDescription = mapped.TopicDescription,
                        CalculatedAmount = mapped.CalculatedAmount,
                        ProcessId = process.Id,
                        CreatedAt = now,
                        UserId = userId,
                        UpdatedAt = now,
                        UpdateUser = userId
                    });
                    inserted++;
                }

                process.RowCount = inserted;
                process.TotalCalculatedSum = sum;
                process.UpdatedAt = DateTime.UtcNow;
                process.UpdateUser = userId;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    processId = process.Id,
                    rowCount = inserted,
                    skipped,
                    totalCalculated = sum,
                    message = skipped > 0
                        ? $"נשלפו {inserted} שורות ({skipped} דולגו)."
                        : $"נשלפו {inserted} שורות בהצלחה."
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Meitar retrieve validation/business failure");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Meitar retrieve failed");
                return StatusCode(500, new { success = false, message = "שגיאה בשליפת נתוני מייתר" });
            }
        }

        private async Task<(bool Exists, int RowCount, decimal TotalCalculated)> GetPeriodStatsAsync(int year, int month)
        {
            var rows = await _context.MeitarMutavim
                .AsNoTracking()
                .Where(m => m.PeriodYear == year && m.PeriodMonth == month)
                .Select(m => m.CalculatedAmount)
                .ToListAsync();

            return (rows.Count > 0, rows.Count, rows.Sum());
        }

        private static bool TryMapRow(JsonElement row, out MappedMutavimRow mapped)
        {
            mapped = default!;
            if (row.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryGetString(row, "beneficiaryCode", out var beneficiary) &&
                !TryGetString(row, "BeneficiaryCode", out beneficiary))
                return false;

            if (!TryGetDateOnly(row, "calcDate", out var calcDate) &&
                !TryGetDateOnly(row, "CalcDate", out calcDate))
                return false;

            TryGetString(row, "topicCode", out var topicCode);
            if (topicCode == null)
                TryGetString(row, "TopicCode", out topicCode);

            TryGetString(row, "topicDescription", out var topicDescription);
            if (topicDescription == null)
                TryGetString(row, "TopicDescription", out topicDescription);

            if (!TryGetDecimal(row, "calculatedAmount", out var amount) &&
                !TryGetDecimal(row, "CalculatedAmount", out amount))
                amount = 0;

            mapped = new MappedMutavimRow(
                beneficiary!,
                calcDate,
                topicCode,
                topicDescription,
                amount);
            return true;
        }

        private static bool TryGetString(JsonElement row, string name, out string? value)
        {
            value = null;
            if (!row.TryGetProperty(name, out var prop))
                return false;
            if (prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return false;

            value = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetDecimal(JsonElement row, string name, out decimal value)
        {
            value = 0;
            if (!row.TryGetProperty(name, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out value))
                return true;

            if (prop.ValueKind == JsonValueKind.String &&
                decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            return false;
        }

        private static bool TryGetDateOnly(JsonElement row, string name, out DateOnly value)
        {
            value = default;
            if (!row.TryGetProperty(name, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return false;

                if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
                    return true;

                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    value = DateOnly.FromDateTime(dt);
                    return true;
                }

                return false;
            }

            if (prop.ValueKind == JsonValueKind.Object &&
                prop.TryGetProperty("year", out var y) &&
                prop.TryGetProperty("month", out var m) &&
                prop.TryGetProperty("day", out var d) &&
                y.TryGetInt32(out var year) &&
                m.TryGetInt32(out var month) &&
                d.TryGetInt32(out var day))
            {
                value = new DateOnly(year, month, day);
                return true;
            }

            return false;
        }

        private sealed record MappedMutavimRow(
            string BeneficiaryCode,
            DateOnly CalcDate,
            string? TopicCode,
            string? TopicDescription,
            decimal CalculatedAmount);
    }
}
