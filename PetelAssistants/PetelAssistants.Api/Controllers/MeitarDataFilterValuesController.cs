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
    [Route("api/meitar-data-filter-values")]
    public class MeitarDataFilterValuesController : BaseController
    {
        private readonly SharedDbContext _sharedContext;

        public MeitarDataFilterValuesController(
            SharedDbContext sharedContext,
            UserSessionService userSessionService,
            ILogger<MeitarDataFilterValuesController> logger)
            : base(userSessionService, logger)
        {
            _sharedContext = sharedContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var query = _sharedContext.MeitarDataFilterValues.AsNoTracking();
            if (!includeInactive)
                query = query.Where(v => v.IsActive);

            var items = await query
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.FileName)
                .ThenBy(v => v.FilterField)
                .Select(v => new MeitarDataFilterValueDto
                {
                    Id = v.Id,
                    FileName = v.FileName,
                    FilterField = v.FilterField,
                    FilterValue = v.FilterValue,
                    IsActive = v.IsActive,
                    DisplayOrder = v.DisplayOrder
                })
                .ToListAsync();

            return Ok(new { success = true, data = items });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMeitarDataFilterValueRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.FileName) ||
                string.IsNullOrWhiteSpace(request.FilterField) ||
                string.IsNullOrWhiteSpace(request.FilterValue))
                return BadRequest(new { success = false, message = "שם קובץ, שדה סינון וערך הם שדות חובה" });

            var fileName = request.FileName.Trim();
            var filterField = request.FilterField.Trim();
            var filterValue = request.FilterValue.Trim();

            var exists = await _sharedContext.MeitarDataFilterValues.AnyAsync(v =>
                v.FileName == fileName && v.FilterField == filterField && v.FilterValue == filterValue);
            if (exists)
                return BadRequest(new { success = false, message = "ערך סינון זהה כבר קיים" });

            var entity = new MeitarDataFilterValue
            {
                FileName = fileName,
                FilterField = filterField,
                FilterValue = filterValue,
                DisplayOrder = request.DisplayOrder,
                IsActive = true
            };

            _sharedContext.MeitarDataFilterValues.Add(entity);
            await _sharedContext.SaveChangesAsync();

            return Ok(new { success = true, message = "ערך סינון נוצר בהצלחה", data = new { entity.Id } });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateMeitarDataFilterValueRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.FileName) ||
                string.IsNullOrWhiteSpace(request.FilterField) ||
                string.IsNullOrWhiteSpace(request.FilterValue))
                return BadRequest(new { success = false, message = "שם קובץ, שדה סינון וערך הם שדות חובה" });

            var entity = await _sharedContext.MeitarDataFilterValues.FindAsync(id);
            if (entity == null)
                return NotFound(new { success = false, message = "ערך סינון לא נמצא" });

            var fileName = request.FileName.Trim();
            var filterField = request.FilterField.Trim();
            var filterValue = request.FilterValue.Trim();

            var exists = await _sharedContext.MeitarDataFilterValues.AnyAsync(v =>
                v.Id != id && v.FileName == fileName && v.FilterField == filterField && v.FilterValue == filterValue);
            if (exists)
                return BadRequest(new { success = false, message = "ערך סינון זהה כבר קיים" });

            entity.FileName = fileName;
            entity.FilterField = filterField;
            entity.FilterValue = filterValue;
            entity.DisplayOrder = request.DisplayOrder;
            entity.IsActive = request.IsActive;

            await _sharedContext.SaveChangesAsync();
            return Ok(new { success = true, message = "ערך סינון עודכן בהצלחה" });
        }
    }
}
