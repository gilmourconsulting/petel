using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/salary-anomalies")]
    public class SalaryAnomaliesController : BaseController
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _shared;
        private readonly MonthlyImportComparisonService _comparisonService;

        public SalaryAnomaliesController(
            AssistDbContext context,
            SharedDbContext shared,
            MonthlyImportComparisonService comparisonService,
            UserSessionService sessionService,
            ILogger<SalaryAnomaliesController> logger)
            : base(sessionService, logger)
        {
            _context = context;
            _shared = shared;
            _comparisonService = comparisonService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] int? processId,
            [FromQuery] int? statusId,
            [FromQuery] string? reasonCode)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            var resolvedProcessId = processId;
            if (resolvedProcessId == null)
            {
                resolvedProcessId = await _context.SalaryUploadProcesses
                    .Where(p => p.PeriodYear == year && p.PeriodMonth == month)
                    .OrderByDescending(p => p.Id)
                    .Select(p => (int?)p.Id)
                    .FirstOrDefaultAsync();
            }

            if (resolvedProcessId == null)
                return Ok(new { success = true, data = new List<SalaryAnomalyDto>() });

            var hasSummaries = await _context.SalaryMonthSummaries.AnyAsync(s => s.ProcessId == resolvedProcessId.Value);
            var hasSalaries = await _context.Salaries.AnyAsync(s => s.ProcessId == resolvedProcessId.Value);
            if (!hasSummaries && hasSalaries)
                await _comparisonService.RebuildSalaryProcessAsync(resolvedProcessId.Value, userId);

            var query = _context.SalaryAnomalies.AsNoTracking()
                .Where(a => a.ProcessId == resolvedProcessId.Value);

            if (statusId.HasValue)
                query = query.Where(a => a.StatusId == statusId.Value);
            if (!string.IsNullOrWhiteSpace(reasonCode))
                query = query.Where(a => a.ReasonCode == reasonCode);

            var anomalies = await query.ToListAsync();

            var statuses = await _shared.Statuses.AsNoTracking()
                .Where(s => s.Object == StatusObjects.SalaryAnomaly)
                .ToDictionaryAsync(s => s.Id);

            var typeNames = await _shared.AssistantTypes.AsNoTracking()
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName);

            var personIds = anomalies.Where(a => a.MatchedPersonId.HasValue)
                .Select(a => a.MatchedPersonId!.Value)
                .Distinct()
                .ToList();
            var personNames = new Dictionary<int, string>();
            if (personIds.Count > 0)
            {
                personNames = await _context.PersonDetails.AsNoTracking()
                    .Where(d => d.IsLastVersion && personIds.Contains(d.PersonId))
                    .Select(d => new { d.PersonId, Name = (d.FirstName + " " + d.LastName).Trim() })
                    .ToDictionaryAsync(x => x.PersonId, x => x.Name);
            }

            var items = anomalies
                .OrderBy(a => a.DepartmentId)
                .ThenBy(a => a.NationalId)
                .Select(a =>
                {
                    var status = statuses.GetValueOrDefault(a.StatusId);
                    return new SalaryAnomalyDto
                    {
                        Id = a.Id,
                        ProcessId = a.ProcessId,
                        SalaryId = a.SalaryId,
                        NationalId = a.NationalId,
                        DepartmentId = a.DepartmentId,
                        DepartmentName = a.DepartmentName,
                        PositionPercentage = a.PositionPercentage,
                        TotalSalary = a.TotalSalary,
                        MatchedPersonId = a.MatchedPersonId,
                        MatchedPersonName = a.MatchedPersonId.HasValue
                            ? personNames.GetValueOrDefault(a.MatchedPersonId.Value)
                            : null,
                        MatchedAllocationId = a.MatchedAllocationId,
                        MappedAssistantTypeId = a.MappedAssistantTypeId,
                        MappedAssistantTypeName = a.MappedAssistantTypeId.HasValue
                            ? typeNames.GetValueOrDefault(a.MappedAssistantTypeId.Value)
                            : null,
                        AllocationAssistantTypeId = a.AllocationAssistantTypeId,
                        AllocationAssistantTypeName = a.AllocationAssistantTypeId.HasValue
                            ? typeNames.GetValueOrDefault(a.AllocationAssistantTypeId.Value)
                            : null,
                        ReasonCode = a.ReasonCode,
                        Message = a.Message,
                        StatusId = a.StatusId,
                        StatusCode = status?.Code ?? "",
                        StatusName = status?.Name ?? "",
                        Notes = a.Notes
                    };
                })
                .ToList();

            return Ok(new { success = true, data = items });
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateSalaryAnomalyStatusRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            var anomaly = await _context.SalaryAnomalies.FirstOrDefaultAsync(a => a.Id == id);
            if (anomaly == null)
                return NotFound(new { success = false, message = "חריגה לא נמצאה" });

            var status = await _shared.Statuses.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.StatusId &&
                                          s.Object == StatusObjects.SalaryAnomaly &&
                                          s.IsActive);
            if (status == null)
                return BadRequest(new { success = false, message = "סטטוס לא תקין" });

            anomaly.StatusId = status.Id;
            anomaly.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            anomaly.UpdatedAt = DateTime.UtcNow;
            anomaly.UpdateUser = userId;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "הסטטוס עודכן" });
        }
    }
}
