using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Models;
using PetelApp.Api.Session;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Service for managing system attributes following system attributes pattern
    /// Implements caching and session integration for multi-tenant educational SaaS
    /// </summary>
    public class SystemAttributeService
    {
        private readonly AppDbContext _context;
        private readonly UserSessionService _userSessionService;
        private static readonly Dictionary<string, SystemAttributeDto> _cachedAttributes = new();
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

        public SystemAttributeService(AppDbContext context, UserSessionService userSessionService)
        {
            _context = context;
            _userSessionService = userSessionService;
        }

        public async Task<List<SystemAttributeDto>> GetAllAttributesAsync()
        {
            // Always refresh cache if empty or expired following system attributes pattern
            if (_cachedAttributes.Count == 0 || DateTime.UtcNow - _lastCacheUpdate > _cacheTimeout)
            {
                await RefreshCacheAsync();
            }

            var attributes = _cachedAttributes.Values.ToList();
            
            // Update session with system attributes following authentication & session management
            await UpdateSessionWithSystemAttributesAsync(attributes);
            
            return attributes;
        }

        public async Task<SystemAttributeDto?> GetAttributeAsync(string name)
        {
            if (_cachedAttributes.Count == 0 || DateTime.UtcNow - _lastCacheUpdate > _cacheTimeout)
            {
                await RefreshCacheAsync();
            }

            _cachedAttributes.TryGetValue(name, out var attribute);
            return attribute;
        }

        private async Task RefreshCacheAsync()
        {
            try
            {
                // Load all system attributes from petel_schema following database conventions
                var dbAttributes = await _context.SystemAttributes.ToListAsync();

                _cachedAttributes.Clear();
                
                foreach (var dbAttr in dbAttributes)
                {
                    var dtoAttr = new SystemAttributeDto
                    {
                        Id = dbAttr.Id,
                        Name = dbAttr.Name,
                        Value = dbAttr.Value ?? string.Empty,
                        ForeignId = dbAttr.foreign_id,
                        Description = dbAttr.Description,
                        CreatedAt = (DateTime)dbAttr.CreatedAt,
                        UpdatedAt = (DateTime)dbAttr.UpdatedAt
                    };
                    
                    _cachedAttributes[dbAttr.Name] = dtoAttr;
                }

                _lastCacheUpdate = DateTime.UtcNow;
                Console.WriteLine($"System attributes cache refreshed with {dbAttributes.Count} items from database");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing system attributes cache: {ex.Message}");
                throw;
            }
        }

        private async Task UpdateSessionWithSystemAttributesAsync(List<SystemAttributeDto> attributes)
        {
            try
            {
                var session = _userSessionService.GetUserSession();
                if (session != null)
                {
                    session.SystemAttributes.Clear();
                    session.SystemAttributeForeignIds.Clear();
                    
                    foreach (var attr in attributes)
                    {
                        session.SystemAttributes[attr.Name] = attr.Value ?? string.Empty;
                        session.SystemAttributeForeignIds[attr.Name] = attr.ForeignId;
                    }
                    
                    session.SystemAttributesLastLoaded = DateTime.UtcNow;
                    _userSessionService.SetUserSession(session);
                }
            }
            catch (Exception)
            {
                // Session update is optional, don't break the main flow
            }
            
            await Task.CompletedTask;
        }

        public async Task SetSelectedYearInSessionAsync(string yearType, int? foreignId, string yearValue)
        {
            try
            {
                var session = _userSessionService.GetUserSession();
                if (session != null)
                {
                    // Store selected year following authentication & session management patterns
                    session.SelectedYear = foreignId;
                    session.SelectedYearType = yearType;
                    session.SelectedYearValue = yearValue;
                    
                    _userSessionService.SetUserSession(session);
                }
            }
            catch (Exception)
            {
                // Session update failure should not break functionality
            }
            
            await Task.CompletedTask;
        }

        public async Task<string?> GetAttributeValueAsync(string name)
        {
            var attribute = await GetAttributeAsync(name);
            return attribute?.Value;
        }

        public async Task<int?> GetAttributeForeignIdAsync(string name)
        {
            var attribute = await GetAttributeAsync(name);
            return attribute?.ForeignId;
        }
    }
}