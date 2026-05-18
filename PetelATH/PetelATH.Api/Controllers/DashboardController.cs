using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Models.DTOs;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : BaseController
    {
        private readonly AppDbContext _context;

        // School entity type IDs (types 1 and 4 are school-level)
        private static readonly int[] SchoolEntityTypes = { 1, 4 };
        // Council entity type ID
        private const int CouncilEntityType = 5;

        public DashboardController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<DashboardController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Returns KPI summary stats scoped to the current user's entity and selected year.
        /// The returned stats vary by entity type: school / council / ministry-network.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return Unauthorized(new { success = false, message = "נדרש אימות" });

                if (!int.TryParse(session.EntityId, out int entityId))
                    return BadRequest(new { success = false, message = "מזהה ישות לא תקין" });

                if (!int.TryParse(session.EntityTypeId, out int entityTypeId))
                    return BadRequest(new { success = false, message = "סוג ישות לא תקין" });

                // Resolve selected year from session
                int? yearId = null;
                var yearIdStr = session.GetProperty("SelectedYearId");
                if (!string.IsNullOrEmpty(yearIdStr) && int.TryParse(yearIdStr, out int parsedYearId))
                    yearId = parsedYearId;

                DashboardSummaryDto result;

                if (SchoolEntityTypes.Contains(entityTypeId))
                    result = await BuildSchoolSummary(entityId, yearId);
                else if (entityTypeId == CouncilEntityType)
                    result = await BuildCouncilSummary(entityId, yearId);
                else
                    result = await BuildNetworkSummary(entityId, yearId);

                result.EntityTypeName = session.EntityTypeName ?? string.Empty;
                result.YearId = yearId;

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building dashboard summary");
                return StatusCode(500, new { success = false, message = "שגיאה בטעינת נתוני לוח בקרה", error = ex.Message });
            }
        }

        // ── School-level summary ────────────────────────────────────────────
        private async Task<DashboardSummaryDto> BuildSchoolSummary(int entityId, int? yearId)
        {
            var stats = new List<StatItemDto>();

            // Resolve the school year IDs for this school + selected year
            var schoolYearQuery = _context.SchoolYears
                .AsNoTracking()
                .Where(sy => sy.SchoolId == entityId);

            if (yearId.HasValue)
                schoolYearQuery = schoolYearQuery.Where(sy => sy.YearId == yearId.Value);

            var schoolYearIds = await schoolYearQuery.Select(sy => sy.Id).ToListAsync();

            if (schoolYearIds.Count > 0)
            {
                // Total registered students
                var studentCount = await _context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => schoolYearIds.Contains(s.SchoolYearId))
                    .CountAsync();

                stats.Add(new StatItemDto { Label = "תלמידים", Value = studentCount.ToString(), AccentColor = "#667eea" });

                // Total classes
                var classCount = await _context.SchoolClasses
                    .AsNoTracking()
                    .Where(c => schoolYearIds.Contains(c.SchoolYearId))
                    .CountAsync();

                stats.Add(new StatItemDto { Label = "כיתות", Value = classCount.ToString(), AccentColor = "#28a745" });

                // Total active tracks
                var trackCount = await _context.SchoolTracks
                    .AsNoTracking()
                    .Where(t => schoolYearIds.Contains(t.SchoolYearId))
                    .CountAsync();

                if (trackCount > 0)
                    stats.Add(new StatItemDto { Label = "מסלולים", Value = trackCount.ToString(), AccentColor = "#fd7e14" });
            }

            // Active alerts for this entity
            var alertCount = await _context.AlertLinks
                .AsNoTracking()
                .Where(al => al.EntityId == entityId)
                .Select(al => al.AlertId)
                .Distinct()
                .CountAsync();

            if (alertCount > 0)
                stats.Add(new StatItemDto { Label = "התראות", Value = alertCount.ToString(), AccentColor = "#dc3545" });

            return new DashboardSummaryDto { Stats = stats };
        }

        // ── Council-level summary ───────────────────────────────────────────
        private async Task<DashboardSummaryDto> BuildCouncilSummary(int entityId, int? yearId)
        {
            var stats = new List<StatItemDto>();

            // Entities owned by this council
            var ownedEntityIds = await _context.Entities
                .AsNoTracking()
                .Where(e => e.OwnerId == entityId && e.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

            stats.Add(new StatItemDto { Label = "ישויות", Value = ownedEntityIds.Count.ToString(), AccentColor = "#667eea" });

            if (ownedEntityIds.Count > 0)
            {
                // Schools across owned entities for selected year
                var schoolYearQuery = _context.SchoolYears
                    .AsNoTracking()
                    .Where(sy => ownedEntityIds.Contains(sy.SchoolId));

                if (yearId.HasValue)
                    schoolYearQuery = schoolYearQuery.Where(sy => sy.YearId == yearId.Value);

                var schoolYearIds = await schoolYearQuery.Select(sy => sy.Id).ToListAsync();

                // Active schools (last version, active)
                var activeSchoolCount = await _context.Schools
                    .AsNoTracking()
                    .Where(s => schoolYearIds.Contains(s.SchoolYearId) && s.IsLastVersion && s.IsActive)
                    .CountAsync();

                stats.Add(new StatItemDto { Label = "בתי ספר", Value = activeSchoolCount.ToString(), AccentColor = "#28a745" });

                if (schoolYearIds.Count > 0)
                {
                    var totalStudents = await _context.SchoolStudents
                        .AsNoTracking()
                        .Where(s => schoolYearIds.Contains(s.SchoolYearId))
                        .CountAsync();

                    stats.Add(new StatItemDto { Label = "תלמידים", Value = totalStudents.ToString(), AccentColor = "#fd7e14" });
                }
            }

            // Alerts linked to this entity
            var alertCount = await _context.AlertLinks
                .AsNoTracking()
                .Where(al => al.EntityId == entityId)
                .Select(al => al.AlertId)
                .Distinct()
                .CountAsync();

            if (alertCount > 0)
                stats.Add(new StatItemDto { Label = "התראות", Value = alertCount.ToString(), AccentColor = "#dc3545" });

            return new DashboardSummaryDto { Stats = stats };
        }

        // ── Network/Ministry-level summary ─────────────────────────────────
        private async Task<DashboardSummaryDto> BuildNetworkSummary(int entityId, int? yearId)
        {
            var stats = new List<StatItemDto>();

            bool isAdmin = false; // extended admin check can be added here if needed

            var ownedEntityIds = await _context.Entities
                .AsNoTracking()
                .Where(e => e.OwnerId == entityId && e.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

            stats.Add(new StatItemDto { Label = "ישויות", Value = ownedEntityIds.Count.ToString(), AccentColor = "#667eea" });

            if (ownedEntityIds.Count > 0)
            {
                var schoolYearQuery = _context.SchoolYears
                    .AsNoTracking()
                    .Where(sy => ownedEntityIds.Contains(sy.SchoolId));

                if (yearId.HasValue)
                    schoolYearQuery = schoolYearQuery.Where(sy => sy.YearId == yearId.Value);

                var schoolYearIds = await schoolYearQuery.Select(sy => sy.Id).ToListAsync();

                var schoolCount = await _context.Schools
                    .AsNoTracking()
                    .Where(s => schoolYearIds.Contains(s.SchoolYearId) && s.IsLastVersion && s.IsActive)
                    .CountAsync();

                stats.Add(new StatItemDto { Label = "בתי ספר", Value = schoolCount.ToString(), AccentColor = "#28a745" });

                if (schoolYearIds.Count > 0)
                {
                    var totalStudents = await _context.SchoolStudents
                        .AsNoTracking()
                        .Where(s => schoolYearIds.Contains(s.SchoolYearId))
                        .CountAsync();

                    stats.Add(new StatItemDto { Label = "תלמידים", Value = totalStudents.ToString(), AccentColor = "#fd7e14" });
                }
            }

            var alertCount = await _context.AlertLinks
                .AsNoTracking()
                .Where(al => al.EntityId == entityId)
                .Select(al => al.AlertId)
                .Distinct()
                .CountAsync();

            if (alertCount > 0)
                stats.Add(new StatItemDto { Label = "התראות", Value = alertCount.ToString(), AccentColor = "#dc3545" });

            return new DashboardSummaryDto { Stats = stats };
        }
    }
}
