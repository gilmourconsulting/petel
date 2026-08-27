using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/salary-department-mappings")]
    public class SalaryDepartmentMappingsController : BaseController
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _shared;

        public SalaryDepartmentMappingsController(
            AssistDbContext context,
            SharedDbContext shared,
            UserSessionService sessionService,
            ILogger<SalaryDepartmentMappingsController> logger)
            : base(sessionService, logger)
        {
            _context = context;
            _shared = shared;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var mappings = await _context.SalaryDepartmentMappings
                .AsNoTracking()
                .OrderBy(m => m.DepartmentId)
                .ToListAsync();

            var typeNames = await _shared.AssistantTypes.AsNoTracking()
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName);

            var items = mappings.Select(m => new SalaryDepartmentMappingDto
            {
                Id = m.Id,
                DepartmentId = m.DepartmentId,
                DepartmentName = m.DepartmentName,
                AssistantTypeId = m.AssistantTypeId,
                AssistantTypeName = typeNames.GetValueOrDefault(m.AssistantTypeId, m.AssistantTypeId.ToString()),
                IsActive = m.IsActive
            }).ToList();

            return Ok(new { success = true, data = items });
        }

        [HttpGet("unmapped")]
        public async Task<IActionResult> GetUnmapped([FromQuery] int? year, [FromQuery] int? month)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var mappedIds = await _context.SalaryDepartmentMappings
                .AsNoTracking()
                .Select(m => m.DepartmentId)
                .ToListAsync();
            var mappedSet = mappedIds.ToHashSet(StringComparer.Ordinal);

            var query = _context.Salaries.AsNoTracking();
            if (year.HasValue)
                query = query.Where(s => s.PeriodYear == year.Value);
            if (month.HasValue)
                query = query.Where(s => s.PeriodMonth == month.Value);

            var salaries = await query
                .Select(s => new { s.DepartmentId, s.DepartmentName, s.TotalSalary })
                .ToListAsync();

            var items = salaries
                .Where(s => !mappedSet.Contains(s.DepartmentId))
                .GroupBy(s => s.DepartmentId, StringComparer.Ordinal)
                .Select(g => new UnmappedSalaryDepartmentDto
                {
                    DepartmentId = g.Key,
                    DepartmentName = g.Select(x => x.DepartmentName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                    RowCount = g.Count(),
                    TotalSalary = g.Sum(x => x.TotalSalary)
                })
                .OrderBy(x => x.DepartmentId)
                .ToList();

            return Ok(new { success = true, data = items });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveSalaryDepartmentMappingRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            var departmentId = request.DepartmentId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(departmentId))
                return BadRequest(new { success = false, message = "מזהה מחלקה הוא שדה חובה" });

            if (!await _shared.AssistantTypes.AnyAsync(t => t.Id == request.AssistantTypeId && t.IsActive))
                return BadRequest(new { success = false, message = "סוג סייעת לא תקין" });

            if (await _context.SalaryDepartmentMappings.AnyAsync(m => m.DepartmentId == departmentId))
                return BadRequest(new { success = false, message = "מחלקה זו כבר ממופה" });

            var now = DateTime.UtcNow;
            var entity = new SalaryDepartmentMapping
            {
                EntityId = entityId,
                DepartmentId = departmentId,
                DepartmentName = string.IsNullOrWhiteSpace(request.DepartmentName) ? null : request.DepartmentName.Trim(),
                AssistantTypeId = request.AssistantTypeId,
                IsActive = request.IsActive,
                CreatedAt = now,
                UserId = userId,
                UpdatedAt = now,
                UpdateUser = userId
            };
            _context.SalaryDepartmentMappings.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "המיפוי נשמר", data = new { entity.Id } });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveSalaryDepartmentMappingRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            var departmentId = request.DepartmentId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(departmentId))
                return BadRequest(new { success = false, message = "מזהה מחלקה הוא שדה חובה" });

            var entity = await _context.SalaryDepartmentMappings.FirstOrDefaultAsync(m => m.Id == id);
            if (entity == null)
                return NotFound(new { success = false, message = "מיפוי לא נמצא" });

            if (!await _shared.AssistantTypes.AnyAsync(t => t.Id == request.AssistantTypeId))
                return BadRequest(new { success = false, message = "סוג סייעת לא תקין" });

            if (await _context.SalaryDepartmentMappings.AnyAsync(m => m.DepartmentId == departmentId && m.Id != id))
                return BadRequest(new { success = false, message = "מחלקה זו כבר ממופה" });

            entity.DepartmentId = departmentId;
            entity.DepartmentName = string.IsNullOrWhiteSpace(request.DepartmentName) ? null : request.DepartmentName.Trim();
            entity.AssistantTypeId = request.AssistantTypeId;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdateUser = userId;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "המיפוי עודכן" });
        }
    }
}
