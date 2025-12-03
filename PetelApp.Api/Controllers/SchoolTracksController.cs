using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Controllers;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Models;
using PetelApp.Api.Session;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolTracksController : BaseController
    {
        private readonly AppDbContext _context;

        public SchoolTracksController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<SchoolTracksController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get school tracks for a specific school year
        /// NO AUTHENTICATION REQUIRED - Similar to school attributes pattern
        /// </summary>
        [HttpGet("by-school-year/{schoolYearId}")]
        public async Task<IActionResult> GetSchoolTracksBySchoolYear(int schoolYearId)
        {
            try
            {
                _logger.LogInformation(
                    "Loading school tracks for school year {SchoolYearId}",
                    schoolYearId
                );

                // ✅ Get data from database WITHOUT calling GlobalFunctions
                var tracksFromDb = await _context.SchoolTracks
                    .AsNoTracking()
                    .Include(st => st.Track)
                    .Include(st => st.TrackLevel)
                    .Include(st => st.SchoolClass)
                    .Where(st => st.SchoolYearId == schoolYearId)
                    .OrderBy(st => st.Track.TrackName) // ✅ Order by database field
                    .ThenBy(st => st.TrackLevel.LevelName)
                    .ThenBy(st => st.SchoolClass.Level)
                    .ThenBy(st => st.SchoolClass.ClassNumber)
                    .ToListAsync(); // ✅ Execute query FIRST

                // ✅ THEN apply RTL text processing in memory
                var tracks = tracksFromDb
                    .Select(st => new
                    {
                        id = st.Id,
                        trackId = st.TrackId,
                        track = st.Track != null ? GlobalFunctions.ToRtlText(st.Track.TrackName) : "",
                        trackLevelId = st.TrackLevelId,
                        trackLevel = st.TrackLevel != null ? GlobalFunctions.ToRtlText(st.TrackLevel.LevelName) ?? "" : "",
                        classId = st.ClassId,
                        className = st.SchoolClass != null ?
                            st.SchoolClass.Level + " " + st.SchoolClass.ClassNumber : "",
                        weeklyHours = st.WeeklyHours ?? 0
                    })
                    .ToList();

                _logger.LogInformation(
                    "Found {Count} school tracks for school year {SchoolYearId}",
                    tracks.Count,
                    schoolYearId
                );

                return Ok(new
                {
                    success = true,
                    data = tracks,
                    message = "School tracks retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting school tracks for school year {SchoolYearId}", schoolYearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת מגמות בית הספר",
                    error = ex.Message
                });
            }
        }
        /// <summary>
        /// Create new school track with hours validation
        /// Validates hours against track level min/max constraints
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSchoolTrack([FromBody] CreateSchoolTrackDto dto)
        {
            try
            {
                var session = GetCurrentSession();

                // ✅ Check for null session
                if (session == null)
                {
                    _logger.LogError("No valid session found");
                    return Unauthorized(new { success = false, message = "לא נמצאה הפעלה פעילה. אנא התחבר מחדש." });
                }

                _logger.LogInformation("Creating school track for school year {SchoolYearId}", dto.SchoolYearId);

                // ✅ Validate hours against track level constraints if level is specified
                if (dto.TrackLevelId.HasValue)
                {
                    var trackLevel = await _context.TrackLevels
                        .AsNoTracking()
                        .FirstOrDefaultAsync(tl => tl.Id == dto.TrackLevelId.Value);

                    if (trackLevel != null)
                    {
                        if (dto.WeeklyHours < trackLevel.MinHours)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = $"מספר השעות ({dto.WeeklyHours}) נמוך ממינימום הנדרש ({trackLevel.MinHours})"
                            });
                        }

                        if (trackLevel.MaxHours.HasValue && dto.WeeklyHours > trackLevel.MaxHours.Value)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = $"מספר השעות ({dto.WeeklyHours}) עובר את המקסימום המותר ({trackLevel.MaxHours.Value})"
                            });
                        }
                    }
                }

                // ✅ Check for duplicate (same track/level/class combination)
                var exists = await _context.SchoolTracks
                    .AnyAsync(st => st.SchoolYearId == dto.SchoolYearId &&
                                   st.TrackId == dto.TrackId &&
                                   st.TrackLevelId == dto.TrackLevelId &&
                                   st.ClassId == dto.ClassId);

                if (exists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "מגמה זו כבר קיימת עבור כיתה זו"
                    });
                }

                var track = new SchoolTrack
                {
                    SchoolYearId = dto.SchoolYearId,
                    TrackId = dto.TrackId,
                    TrackLevelId = dto.TrackLevelId,
                    ClassId = dto.ClassId,
                    WeeklyHours = dto.WeeklyHours,
                    UserId = int.Parse(session.UserId)
                };

                _context.SchoolTracks.Add(track);
                await _context.SaveChangesAsync();

                _logger.LogInformation("School track created successfully with ID {Id}", track.Id);

                return Ok(new
                {
                    success = true,
                    data = new { id = track.Id },
                    message = "מגמת בית ספר נוצרה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating school track");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת מגמת בית ספר",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update existing school track with hours validation
        /// Validates hours against track level min/max constraints
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSchoolTrack(int id, [FromBody] UpdateSchoolTrackDto dto)
        {
            try
            {
                var session = GetCurrentSession();

                // ✅ Check for null session
                if (session == null)
                {
                    _logger.LogError("No valid session found");
                    return Unauthorized(new { success = false, message = "לא נמצאה הפעלה פעילה. אנא התחבר מחדש." });
                }

                _logger.LogInformation("Updating school track {Id}", id);

                var track = await _context.SchoolTracks.FindAsync(id);

                if (track == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "מגמה לא נמצאה"
                    });
                }

                // ✅ Validate hours against track level constraints if level is specified
                if (dto.TrackLevelId.HasValue)
                {
                    var trackLevel = await _context.TrackLevels
                        .AsNoTracking()
                        .FirstOrDefaultAsync(tl => tl.Id == dto.TrackLevelId.Value);

                    if (trackLevel != null)
                    {
                        if (dto.WeeklyHours < trackLevel.MinHours)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = $"מספר השעות ({dto.WeeklyHours}) נמוך ממינימום הנדרש ({trackLevel.MinHours})"
                            });
                        }

                        if (trackLevel.MaxHours.HasValue && dto.WeeklyHours > trackLevel.MaxHours.Value)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = $"מספר השעות ({dto.WeeklyHours}) עובר את המקסימום המותר ({trackLevel.MaxHours.Value})"
                            });
                        }
                    }
                }

                // Update fields
                track.TrackLevelId = dto.TrackLevelId;
                track.ClassId = dto.ClassId;
                track.WeeklyHours = dto.WeeklyHours;
                track.UserId = int.Parse(session.UserId);
                // CreatedAt will be updated automatically by database timestamp

                await _context.SaveChangesAsync();

                _logger.LogInformation("School track {Id} updated successfully", id);

                return Ok(new
                {
                    success = true,
                    data = new { id = track.Id },
                    message = "מגמת בית ספר עודכנה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating school track {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון מגמת בית ספר",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete school track
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSchoolTrack(int id)
        {
            try
            {
                _logger.LogInformation("Deleting school track {Id}", id);

                var track = await _context.SchoolTracks.FindAsync(id);

                if (track == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "מגמה לא נמצאה"
                    });
                }

                _context.SchoolTracks.Remove(track);
                await _context.SaveChangesAsync();

                _logger.LogInformation("School track {Id} deleted successfully", id);

                return Ok(new
                {
                    success = true,
                    message = "מגמת בית ספר נמחקה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting school track {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה במחיקת מגמת בית ספר",
                    error = ex.Message
                });
            }
        }
    }

}