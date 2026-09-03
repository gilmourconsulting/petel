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
        private readonly SharedDbContext _sharedContext;
        private readonly IMeitarDataService _meitarDataService;
        private readonly MonthlyImportComparisonService _comparisonService;

        public MeitarDataController(
            AssistDbContext context,
            SharedDbContext sharedContext,
            IMeitarDataService meitarDataService,
            MonthlyImportComparisonService comparisonService,
            UserSessionService sessionService,
            ILogger<MeitarDataController> logger)
            : base(sessionService, logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _meitarDataService = meitarDataService;
            _comparisonService = comparisonService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] string? dateField)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            var byEffectiveDate = string.Equals(dateField, "effective", StringComparison.OrdinalIgnoreCase);

            var query = _context.MeitarMutavim.AsNoTracking();

            query = byEffectiveDate
                ? query.Where(m => m.EffectiveDate != null &&
                                   m.EffectiveDate.Value.Year == year &&
                                   m.EffectiveDate.Value.Month == month)
                : query.Where(m => m.CalcDate.Year == year && m.CalcDate.Month == month);

            var items = await query
                .OrderBy(m => m.BeneficiaryCode)
                .ThenBy(m => m.TopicCode)
                .Select(m => new MeitarMutavimListItemDto
                {
                    Id = m.Id,
                    PeriodYear = m.PeriodYear,
                    PeriodMonth = m.PeriodMonth,
                    BeneficiaryCode = m.BeneficiaryCode,
                    CalcDate = m.CalcDate,
                    EffectiveDate = m.EffectiveDate,
                    TopicCode = m.TopicCode,
                    TopicDescription = m.TopicDescription,
                    UnitCount = m.UnitCount,
                    Cost = m.Cost,
                    ParticipationPercent = m.ParticipationPercent,
                    CalculatedAmount = m.CalculatedAmount,
                    PreviousCalculatedAmount = m.PreviousCalculatedAmount,
                    CalculatedDifference = m.CalculatedDifference
                })
                .ToListAsync();

            return Ok(new { success = true, data = items });
        }

        [HttpGet("mucarim")]
        public async Task<IActionResult> GetAllMucarim(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] string? dateField)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            var byEffectiveDate = string.Equals(dateField, "effective", StringComparison.OrdinalIgnoreCase);

            var query = _context.MeitarMucarim.AsNoTracking();

            query = byEffectiveDate
                ? query.Where(m => m.EffectiveDate != null &&
                                   m.EffectiveDate.Value.Year == year &&
                                   m.EffectiveDate.Value.Month == month)
                : query.Where(m => m.CalcDate.Year == year && m.CalcDate.Month == month);

            var items = await query
                .OrderBy(m => m.InstitutionCode)
                .ThenBy(m => m.TopicCode)
                .Select(m => new MeitarMucarimListItemDto
                {
                    Id = m.Id,
                    PeriodYear = m.PeriodYear,
                    PeriodMonth = m.PeriodMonth,
                    BeneficiaryCode = m.BeneficiaryCode,
                    CalcDate = m.CalcDate,
                    EffectiveDate = m.EffectiveDate,
                    InstitutionCode = m.InstitutionCode,
                    InstitutionName = m.InstitutionName,
                    TopicCode = m.TopicCode,
                    TopicDescription = m.TopicDescription,
                    Status = m.Status,
                    UnitCount = m.UnitCount,
                    Percent = m.Percent,
                    Cost = m.Cost,
                    CalculatedAmount = m.CalculatedAmount,
                    PreviousCalculatedAmount = m.PreviousCalculatedAmount,
                    CalculatedDifference = m.CalculatedDifference,
                    UnitDescription = m.UnitDescription
                })
                .ToListAsync();

            return Ok(new { success = true, data = items });
        }

        [HttpGet("sharatim")]
        public async Task<IActionResult> GetAllSharatim(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] string? dateField)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            var byEffectiveDate = string.Equals(dateField, "effective", StringComparison.OrdinalIgnoreCase);

            var query = _context.MeitarSharatim.AsNoTracking();

            query = byEffectiveDate
                ? query.Where(m => m.EffectiveDate.Year == year && m.EffectiveDate.Month == month)
                : query.Where(m => m.CalcDate.Year == year && m.CalcDate.Month == month);

            var items = await query
                .OrderBy(m => m.InstitutionCode)
                .ThenBy(m => m.TopicCode)
                .Select(m => new MeitarSharatimListItemDto
                {
                    Id = m.Id,
                    PeriodYear = m.PeriodYear,
                    PeriodMonth = m.PeriodMonth,
                    CalcDate = m.CalcDate,
                    EffectiveDate = m.EffectiveDate,
                    InstitutionCode = m.InstitutionCode,
                    InstitutionName = m.InstitutionName,
                    TopicCode = m.TopicCode,
                    ClassCount = m.ClassCount
                })
                .ToListAsync();

            return Ok(new { success = true, data = items });
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

            var symbolCode = await _meitarDataService.GetSymbolCodeForEntityAsync(entityId);
            if (string.IsNullOrWhiteSpace(symbolCode))
                return BadRequest(new { success = false, message = "לא הוגדר קוד מוטב (symbol_code) לרשות הנוכחית." });

            var result = await RetrieveOnePeriodAsync(
                entityId, userId, symbolCode, request.PeriodYear, request.PeriodMonth, request.ReplaceExisting);

            if (result.Skipped)
            {
                return Conflict(new
                {
                    success = false,
                    periodExists = true,
                    rowCount = result.ExistingRowCount,
                    message = "קיימים נתוני מיתר לתקופה זו. יש לאשר החלפה."
                });
            }

            if (!result.Success)
            {
                if (result.IsBadRequest)
                    return BadRequest(new { success = false, message = result.ErrorMessage });

                return StatusCode(500, new { success = false, message = result.ErrorMessage });
            }

            return Ok(new
            {
                success = true,
                processId = result.ProcessId,
                rowCount = result.RowCount,
                skipped = result.SkippedRows,
                totalCalculated = result.TotalCalculated,
                mucarim = new
                {
                    rowCount = result.MucarimRowCount,
                    skipped = result.MucarimSkipped,
                    totalCalculated = result.MucarimTotalCalculated,
                    error = result.MucarimError
                },
                sharatim = new
                {
                    rowCount = result.SharatimRowCount,
                    skipped = result.SharatimSkipped,
                    totalClassCount = result.SharatimTotalClassCount,
                    error = result.SharatimError
                },
                message = result.Message
            });
        }

        [HttpGet("period-exists-range")]
        public async Task<IActionResult> PeriodExistsRange(
            [FromQuery] int fromYear, [FromQuery] int fromMonth,
            [FromQuery] int toYear, [FromQuery] int toMonth)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!TryBuildPeriodRange(fromYear, fromMonth, toYear, toMonth, out var periods, out var rangeError))
                return BadRequest(new { success = false, message = rangeError });

            var periodResults = new List<object>();
            var anyExists = false;
            var totalExistingRows = 0;

            foreach (var (year, month) in periods)
            {
                var (exists, rowCount, totalCalculated) = await GetPeriodStatsAsync(year, month);
                if (exists)
                {
                    anyExists = true;
                    totalExistingRows += rowCount;
                }

                periodResults.Add(new { year, month, exists, rowCount, totalCalculated });
            }

            return Ok(new
            {
                success = true,
                periods = periodResults,
                anyExists,
                totalExistingRows,
                periodCount = periods.Count
            });
        }

        [HttpPost("retrieve-range")]
        public async Task<IActionResult> RetrieveRange([FromBody] MeitarRetrieveRangeRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            if (!TryBuildPeriodRange(request.FromYear, request.FromMonth, request.ToYear, request.ToMonth, out var periods, out var rangeError))
                return BadRequest(new { success = false, message = rangeError });

            var symbolCode = await _meitarDataService.GetSymbolCodeForEntityAsync(entityId);
            if (string.IsNullOrWhiteSpace(symbolCode))
                return BadRequest(new { success = false, message = "לא הוגדר קוד מוטב (symbol_code) לרשות הנוכחית." });

            var periodResults = new List<object>();
            var succeededCount = 0;
            var failedCount = 0;
            var skippedCount = 0;

            foreach (var (year, month) in periods)
            {
                var result = await RetrieveOnePeriodAsync(entityId, userId, symbolCode, year, month, request.ReplaceExisting);

                if (result.Skipped)
                {
                    skippedCount++;
                    periodResults.Add(new
                    {
                        year,
                        month,
                        success = false,
                        skipped = true,
                        message = $"קיימים כבר {result.ExistingRowCount} שורות לתקופה זו — דולג."
                    });
                    continue;
                }

                if (!result.Success)
                {
                    failedCount++;
                    periodResults.Add(new { year, month, success = false, skipped = false, message = result.ErrorMessage });
                    continue;
                }

                succeededCount++;
                periodResults.Add(new
                {
                    year,
                    month,
                    success = true,
                    skipped = false,
                    rowCount = result.RowCount,
                    mucarimRowCount = result.MucarimRowCount,
                    sharatimRowCount = result.SharatimRowCount,
                    message = result.Message
                });
            }

            var message = $"טווח {periods.Count} תקופות: הושלמו {succeededCount}, נכשלו {failedCount}, דולגו {skippedCount}.";

            return Ok(new
            {
                success = failedCount == 0,
                periodCount = periods.Count,
                succeededCount,
                failedCount,
                skippedCount,
                periods = periodResults,
                message
            });
        }

        private static bool TryBuildPeriodRange(
            int fromYear, int fromMonth, int toYear, int toMonth,
            out List<(int Year, int Month)> periods, out string? error)
        {
            const int maxPeriods = 24;
            periods = new List<(int, int)>();
            error = null;

            if (fromMonth < 1 || fromMonth > 12 || toMonth < 1 || toMonth > 12)
            {
                error = "חודש לא תקין";
                return false;
            }

            var fromKey = fromYear * 12 + (fromMonth - 1);
            var toKey = toYear * 12 + (toMonth - 1);

            if (fromKey > toKey)
            {
                error = "תקופת ההתחלה חייבת להיות לפני או שווה לתקופת הסיום";
                return false;
            }

            var count = toKey - fromKey + 1;
            if (count > maxPeriods)
            {
                error = $"טווח התקופות ארוך מדי (מקסימום {maxPeriods} חודשים)";
                return false;
            }

            for (var key = fromKey; key <= toKey; key++)
            {
                var year = key / 12;
                var month = key % 12 + 1;
                periods.Add((year, month));
            }

            return true;
        }

        private async Task<PeriodRetrieveResult> RetrieveOnePeriodAsync(
            int entityId, int? userId, string symbolCode, int year, int month, bool replaceExisting)
        {
            var (exists, existingCount, _) = await GetPeriodStatsAsync(year, month);
            if (exists && !replaceExisting)
            {
                return PeriodRetrieveResult.Skip(existingCount);
            }

            try
            {
                var queryResult = await _meitarDataService.QueryMutavimForSymbolAndPeriodAsync(symbolCode, year, month);

                if (!queryResult.Success)
                {
                    return PeriodRetrieveResult.Failure(
                        queryResult.Message ?? "שליפת נתוני מיתר נכשלה.", isBadRequest: true);
                }

                var now = DateTime.UtcNow;
                var process = new MeitarRetrieveProcess
                {
                    EntityId = entityId,
                    PeriodYear = year,
                    PeriodMonth = month,
                    Source = "meitar",
                    CreatedAt = now,
                    UserId = userId,
                    UpdatedAt = now,
                    UpdateUser = userId
                };
                _context.MeitarRetrieveProcesses.Add(process);
                await _context.SaveChangesAsync();

                if (replaceExisting && exists)
                {
                    var oldRows = await _context.MeitarMutavim
                        .Where(m => m.PeriodYear == year && m.PeriodMonth == month)
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
                        PeriodYear = year,
                        PeriodMonth = month,
                        BeneficiaryCode = mapped.BeneficiaryCode,
                        CalcDate = mapped.CalcDate,
                        EffectiveDate = mapped.EffectiveDate,
                        TopicCode = mapped.TopicCode,
                        TopicDescription = mapped.TopicDescription,
                        UnitCount = mapped.UnitCount,
                        Cost = mapped.Cost,
                        ParticipationPercent = mapped.ParticipationPercent,
                        CalculatedAmount = mapped.CalculatedAmount,
                        PreviousCalculatedAmount = mapped.PreviousCalculatedAmount,
                        CalculatedDifference = mapped.CalculatedDifference,
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

                // MUCARIM is best-effort: a missing filter config or a failed query must not fail MUTAVIM.
                decimal mucarimSum = 0;
                var mucarimInserted = 0;
                var mucarimSkipped = 0;
                string? mucarimError = null;

                try
                {
                    if (replaceExisting && exists)
                    {
                        var oldMucarimRows = await _context.MeitarMucarim
                            .Where(m => m.PeriodYear == year && m.PeriodMonth == month)
                            .ToListAsync();
                        if (oldMucarimRows.Count > 0)
                        {
                            _context.MeitarMucarim.RemoveRange(oldMucarimRows);
                            await _context.SaveChangesAsync();
                        }
                    }

                    var mucarimResult = await _meitarDataService.QueryMucarimForSymbolAndPeriodAsync(symbolCode, year, month);

                    _logger.LogInformation(
                        "MUCARIM retrieve for process {ProcessId}: Success={Success}, RowCount={RowCount}, Message={Message}",
                        process.Id, mucarimResult.Success, mucarimResult.RowCount, mucarimResult.Message);

                    if (!mucarimResult.Success)
                    {
                        mucarimError = mucarimResult.Message ?? "שליפת נתוני מוכרים (MUCARIM) נכשלה.";
                    }
                    else
                    {
                        foreach (var row in mucarimResult.Rows)
                        {
                            if (!TryMapMucarimRow(row, out var mappedMucarim))
                            {
                                mucarimSkipped++;
                                continue;
                            }

                            mucarimSum += mappedMucarim.CalculatedAmount;
                            _context.MeitarMucarim.Add(new MeitarMucarim
                            {
                                EntityId = entityId,
                                PeriodYear = year,
                                PeriodMonth = month,
                                BeneficiaryCode = mappedMucarim.BeneficiaryCode,
                                CalcDate = mappedMucarim.CalcDate,
                                EffectiveDate = mappedMucarim.EffectiveDate,
                                InstitutionCode = mappedMucarim.InstitutionCode,
                                InstitutionName = mappedMucarim.InstitutionName,
                                TopicCode = mappedMucarim.TopicCode,
                                TopicDescription = mappedMucarim.TopicDescription,
                                Status = mappedMucarim.Status,
                                UnitCount = mappedMucarim.UnitCount,
                                Percent = mappedMucarim.Percent,
                                Cost = mappedMucarim.Cost,
                                CalculatedAmount = mappedMucarim.CalculatedAmount,
                                PreviousCalculatedAmount = mappedMucarim.PreviousCalculatedAmount,
                                CalculatedDifference = mappedMucarim.CalculatedDifference,
                                UnitDescription = mappedMucarim.UnitDescription,
                                ProcessId = process.Id,
                                CreatedAt = now,
                                UserId = userId,
                                UpdatedAt = now,
                                UpdateUser = userId
                            });
                            mucarimInserted++;
                        }

                        _logger.LogInformation(
                            "MUCARIM retrieve for process {ProcessId}: mapped {Inserted} row(s), skipped {Skipped} row(s) out of {RawCount} returned",
                            process.Id, mucarimInserted, mucarimSkipped, mucarimResult.RowCount);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    mucarimError = ex.Message;
                    _logger.LogWarning(ex, "MUCARIM retrieve skipped (best-effort) for process {ProcessId}", process.Id);
                }
                catch (Exception ex)
                {
                    mucarimError = "שגיאה בשליפת נתוני מוכרים (MUCARIM).";
                    _logger.LogError(ex, "MUCARIM retrieve failed unexpectedly (best-effort) for process {ProcessId}", process.Id);
                }

                process.MucarimRowCount = mucarimInserted;
                process.MucarimTotalCalculatedSum = mucarimSum;
                process.MucarimError = mucarimError;

                // SHARATIM is best-effort too: special-needs class counts per school, one row per
                // school per month (TopicCode 107, kept only when effective_date == calc_date).
                var sharatimSum = 0;
                var sharatimInserted = 0;
                var sharatimSkipped = 0;
                string? sharatimError = null;

                try
                {
                    if (replaceExisting && exists)
                    {
                        var oldSharatimRows = await _context.MeitarSharatim
                            .Where(m => m.PeriodYear == year && m.PeriodMonth == month)
                            .ToListAsync();
                        if (oldSharatimRows.Count > 0)
                        {
                            _context.MeitarSharatim.RemoveRange(oldSharatimRows);
                            await _context.SaveChangesAsync();
                        }
                    }

                    var sharatimResult = await _meitarDataService.QuerySharatimForSymbolAndPeriodAsync(symbolCode, year, month);

                    _logger.LogInformation(
                        "SHARATIM retrieve for process {ProcessId}: Success={Success}, RowCount={RowCount}, Message={Message}",
                        process.Id, sharatimResult.Success, sharatimResult.RowCount, sharatimResult.Message);

                    if (!sharatimResult.Success)
                    {
                        sharatimError = sharatimResult.Message ?? "שליפת נתוני שרתים (SHARATIM) נכשלה.";
                    }
                    else
                    {
                        var institutionBySymbol = await _context.Institutions
                            .AsNoTracking()
                            .Where(i => i.Symbol != null && i.Symbol != string.Empty)
                            .Select(i => new { i.Id, i.Symbol })
                            .ToListAsync();
                        var institutionIdBySymbol = institutionBySymbol
                            .GroupBy(i => NormalizeSymbol(i.Symbol!), StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

                        var hebrewYears = await _sharedContext.HebrewYears
                            .AsNoTracking()
                            .Where(y => y.StartDate != null && y.EndDate != null)
                            .ToListAsync();

                        foreach (var row in sharatimResult.Rows)
                        {
                            if (!TryMapSharatimRow(row, out var mappedSharatim))
                            {
                                sharatimSkipped++;
                                continue;
                            }

                            int? institutionId = null;
                            if (!string.IsNullOrWhiteSpace(mappedSharatim.InstitutionCode) &&
                                institutionIdBySymbol.TryGetValue(NormalizeSymbol(mappedSharatim.InstitutionCode), out var matchedInstitutionId))
                            {
                                institutionId = matchedInstitutionId;
                            }

                            var hebrewYearId = hebrewYears
                                .FirstOrDefault(y => y.StartDate!.Value <= mappedSharatim.EffectiveDate &&
                                                      mappedSharatim.EffectiveDate <= y.EndDate!.Value)
                                ?.Id;

                            sharatimSum += mappedSharatim.ClassCount;
                            _context.MeitarSharatim.Add(new MeitarSharatim
                            {
                                EntityId = entityId,
                                PeriodYear = year,
                                PeriodMonth = month,
                                CalcDate = mappedSharatim.CalcDate,
                                EffectiveDate = mappedSharatim.EffectiveDate,
                                InstitutionCode = mappedSharatim.InstitutionCode,
                                InstitutionName = mappedSharatim.InstitutionName,
                                TopicCode = mappedSharatim.TopicCode,
                                ClassCount = mappedSharatim.ClassCount,
                                InstitutionId = institutionId,
                                HebrewYearId = hebrewYearId,
                                ProcessId = process.Id,
                                CreatedAt = now,
                                UserId = userId,
                                UpdatedAt = now,
                                UpdateUser = userId
                            });
                            sharatimInserted++;
                        }

                        _logger.LogInformation(
                            "SHARATIM retrieve for process {ProcessId}: mapped {Inserted} row(s), skipped {Skipped} row(s) out of {RawCount} returned",
                            process.Id, sharatimInserted, sharatimSkipped, sharatimResult.RowCount);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    sharatimError = ex.Message;
                    _logger.LogWarning(ex, "SHARATIM retrieve skipped (best-effort) for process {ProcessId}", process.Id);
                }
                catch (Exception ex)
                {
                    sharatimError = "שגיאה בשליפת נתוני שרתים (SHARATIM).";
                    _logger.LogError(ex, "SHARATIM retrieve failed unexpectedly (best-effort) for process {ProcessId}", process.Id);
                }

                process.SharatimRowCount = sharatimInserted;
                process.SharatimTotalClassCount = sharatimSum;
                process.SharatimError = sharatimError;
                process.UpdatedAt = DateTime.UtcNow;
                process.UpdateUser = userId;
                await _context.SaveChangesAsync();

                await _comparisonService.RebuildMeitarProcessAsync(process.Id, userId);

                var message = skipped > 0
                    ? $"מוטבים: נשלפו {inserted} שורות ({skipped} דולגו)."
                    : $"מוטבים: נשלפו {inserted} שורות בהצלחה.";
                message += mucarimError != null
                    ? $"\nמוכרים: לא נשלפו — {mucarimError}"
                    : mucarimSkipped > 0
                        ? $"\nמוכרים: נשלפו {mucarimInserted} שורות ({mucarimSkipped} דולגו)."
                        : $"\nמוכרים: נשלפו {mucarimInserted} שורות בהצלחה.";
                message += sharatimError != null
                    ? $"\nשרתים: לא נשלפו — {sharatimError}"
                    : sharatimSkipped > 0
                        ? $"\nשרתים: נשלפו {sharatimInserted} שורות ({sharatimSkipped} דולגו)."
                        : $"\nשרתים: נשלפו {sharatimInserted} שורות בהצלחה.";

                return PeriodRetrieveResult.SuccessResult(
                    process.Id, inserted, skipped, sum,
                    mucarimInserted, mucarimSkipped, mucarimSum, mucarimError,
                    sharatimInserted, sharatimSkipped, sharatimSum, sharatimError,
                    message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Meitar retrieve validation/business failure for period {Year}/{Month}", year, month);
                return PeriodRetrieveResult.Failure(ex.Message, isBadRequest: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Meitar retrieve failed for period {Year}/{Month}", year, month);
                return PeriodRetrieveResult.Failure("שגיאה בשליפת נתוני מיתר", isBadRequest: false);
            }
        }

        private sealed class PeriodRetrieveResult
        {
            public bool Success { get; private init; }
            public bool Skipped { get; private init; }
            public bool IsBadRequest { get; private init; }
            public int ExistingRowCount { get; private init; }
            public int? ProcessId { get; private init; }
            public int RowCount { get; private init; }
            public int SkippedRows { get; private init; }
            public decimal TotalCalculated { get; private init; }
            public int MucarimRowCount { get; private init; }
            public int MucarimSkipped { get; private init; }
            public decimal MucarimTotalCalculated { get; private init; }
            public string? MucarimError { get; private init; }
            public int SharatimRowCount { get; private init; }
            public int SharatimSkipped { get; private init; }
            public int SharatimTotalClassCount { get; private init; }
            public string? SharatimError { get; private init; }
            public string? ErrorMessage { get; private init; }
            public string Message { get; private init; } = string.Empty;

            public static PeriodRetrieveResult Skip(int existingRowCount) => new()
            {
                Skipped = true,
                ExistingRowCount = existingRowCount
            };

            public static PeriodRetrieveResult Failure(string message, bool isBadRequest) => new()
            {
                Success = false,
                IsBadRequest = isBadRequest,
                ErrorMessage = message
            };

            public static PeriodRetrieveResult SuccessResult(
                int processId, int rowCount, int skippedRows, decimal totalCalculated,
                int mucarimRowCount, int mucarimSkipped, decimal mucarimTotalCalculated, string? mucarimError,
                int sharatimRowCount, int sharatimSkipped, int sharatimTotalClassCount, string? sharatimError,
                string message) => new()
            {
                Success = true,
                ProcessId = processId,
                RowCount = rowCount,
                SkippedRows = skippedRows,
                TotalCalculated = totalCalculated,
                MucarimRowCount = mucarimRowCount,
                MucarimSkipped = mucarimSkipped,
                MucarimTotalCalculated = mucarimTotalCalculated,
                MucarimError = mucarimError,
                SharatimRowCount = sharatimRowCount,
                SharatimSkipped = sharatimSkipped,
                SharatimTotalClassCount = sharatimTotalClassCount,
                SharatimError = sharatimError,
                Message = message
            };
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

            DateOnly? effectiveDate = null;
            if (TryGetDateOnly(row, "effectiveDate", out var effective) ||
                TryGetDateOnly(row, "EffectiveDate", out effective))
                effectiveDate = effective;

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
                effectiveDate,
                topicCode,
                topicDescription,
                TryGetOptionalDecimal(row, "unitCount", "UnitCount"),
                TryGetOptionalDecimal(row, "cost", "Cost"),
                TryGetOptionalDecimal(row, "participationPercent", "ParticipationPercent"),
                amount,
                TryGetOptionalDecimal(row, "previousCalculatedAmount", "PreviousCalculatedAmount"),
                TryGetOptionalDecimal(row, "calculatedDifference", "CalculatedDifference"));
            return true;
        }

        private static bool TryMapMucarimRow(JsonElement row, out MappedMucarimRow mapped)
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

            DateOnly? effectiveDate = null;
            if (TryGetDateOnly(row, "effectiveDate", out var effective) ||
                TryGetDateOnly(row, "EffectiveDate", out effective))
                effectiveDate = effective;

            TryGetString(row, "institutionCode", out var institutionCode);
            if (institutionCode == null)
                TryGetString(row, "InstitutionCode", out institutionCode);

            TryGetString(row, "institutionName", out var institutionName);
            if (institutionName == null)
                TryGetString(row, "InstitutionName", out institutionName);

            TryGetString(row, "topicCode", out var topicCode);
            if (topicCode == null)
                TryGetString(row, "TopicCode", out topicCode);

            TryGetString(row, "topic", out var topic);
            if (topic == null)
                TryGetString(row, "Topic", out topic);

            TryGetString(row, "status", out var status);
            if (status == null)
                TryGetString(row, "Status", out status);

            TryGetString(row, "unitDescription", out var unitDescription);
            if (unitDescription == null)
                TryGetString(row, "UnitDescription", out unitDescription);

            if (!TryGetDecimal(row, "calculatedAmount", out var amount) &&
                !TryGetDecimal(row, "CalculatedAmount", out amount))
                amount = 0;

            mapped = new MappedMucarimRow(
                beneficiary!,
                calcDate,
                effectiveDate,
                institutionCode,
                institutionName,
                topicCode,
                topic,
                status,
                TryGetOptionalDecimal(row, "unitCount", "UnitCount"),
                TryGetOptionalDecimal(row, "percent", "Percent"),
                TryGetOptionalDecimal(row, "cost", "Cost"),
                amount,
                TryGetOptionalDecimal(row, "previousCalculatedAmount", "PreviousCalculatedAmount"),
                TryGetOptionalDecimal(row, "calculatedDifference", "CalculatedDifference"),
                unitDescription);
            return true;
        }

        private static bool TryMapSharatimRow(JsonElement row, out MappedSharatimRow mapped)
        {
            mapped = default!;
            if (row.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryGetDateOnly(row, "calcDate", out var calcDate) &&
                !TryGetDateOnly(row, "CalcDate", out calcDate))
                return false;

            if (!TryGetDateOnly(row, "effectiveDate", out var effectiveDate) &&
                !TryGetDateOnly(row, "EffectiveDate", out effectiveDate))
                return false;

            // Defensive local enforcement of "effective date = calc date" — the provider-side
            // TopicCode=107 filter should already narrow to this, but a row that doesn't match
            // must not be stored as a school/month class-count record.
            if (effectiveDate != calcDate)
                return false;

            if (!TryGetOptionalInt(row, "classCount", "ClassCount", out var classCount))
                return false;

            TryGetString(row, "institutionCode", out var institutionCode);
            if (institutionCode == null)
                TryGetString(row, "InstitutionCode", out institutionCode);

            TryGetString(row, "institutionName", out var institutionName);
            if (institutionName == null)
                TryGetString(row, "InstitutionName", out institutionName);

            TryGetString(row, "topicCode", out var topicCode);
            if (topicCode == null)
                TryGetString(row, "TopicCode", out topicCode);

            mapped = new MappedSharatimRow(
                calcDate,
                effectiveDate,
                institutionCode,
                institutionName,
                topicCode,
                classCount);
            return true;
        }

        private static string NormalizeSymbol(string symbol)
        {
            var trimmed = symbol.Trim();
            if (trimmed.EndsWith(".0", StringComparison.Ordinal) &&
                trimmed.Length > 2 &&
                trimmed[..^2].All(char.IsDigit))
                trimmed = trimmed[..^2];
            return trimmed;
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

        private static decimal? TryGetOptionalDecimal(JsonElement row, string camelName, string pascalName)
        {
            if (TryGetDecimal(row, camelName, out var value) ||
                TryGetDecimal(row, pascalName, out value))
                return value;
            return null;
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

        private static bool TryGetOptionalInt(JsonElement row, string camelName, string pascalName, out int value)
        {
            if (TryGetInt(row, camelName, out value) || TryGetInt(row, pascalName, out value))
                return true;
            value = 0;
            return false;
        }

        private static bool TryGetInt(JsonElement row, string name, out int value)
        {
            value = 0;
            if (!row.TryGetProperty(name, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.Number)
            {
                if (prop.TryGetInt32(out value))
                    return true;
                if (prop.TryGetDecimal(out var dec))
                {
                    value = (int)Math.Round(dec, MidpointRounding.AwayFromZero);
                    return true;
                }
                return false;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    return true;
                if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                {
                    value = (int)Math.Round(dec, MidpointRounding.AwayFromZero);
                    return true;
                }
            }

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
            DateOnly? EffectiveDate,
            string? TopicCode,
            string? TopicDescription,
            decimal? UnitCount,
            decimal? Cost,
            decimal? ParticipationPercent,
            decimal CalculatedAmount,
            decimal? PreviousCalculatedAmount,
            decimal? CalculatedDifference);

        private sealed record MappedMucarimRow(
            string BeneficiaryCode,
            DateOnly CalcDate,
            DateOnly? EffectiveDate,
            string? InstitutionCode,
            string? InstitutionName,
            string? TopicCode,
            string? TopicDescription,
            string? Status,
            decimal? UnitCount,
            decimal? Percent,
            decimal? Cost,
            decimal CalculatedAmount,
            decimal? PreviousCalculatedAmount,
            decimal? CalculatedDifference,
            string? UnitDescription);

        private sealed record MappedSharatimRow(
            DateOnly CalcDate,
            DateOnly EffectiveDate,
            string? InstitutionCode,
            string? InstitutionName,
            string? TopicCode,
            int ClassCount);
    }
}
