using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using PetelApp.Api.Models;
using PetelApp.Api.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PetelApp.Api.Services
{
    public class SystemAttributeService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly UserSessionService _userSessionService;
        private readonly ILogger<SystemAttributeService> _logger;
        private readonly Dictionary<string, SystemAttributeDto> _systemAttributes = new Dictionary<string, SystemAttributeDto>();
        private static DateTime _lastLoaded = DateTime.MinValue;

        public SystemAttributeService(
            IServiceProvider serviceProvider, 
            UserSessionService userSessionService, 
            ILogger<SystemAttributeService> logger)
        {
            _serviceProvider = serviceProvider;
            _userSessionService = userSessionService;
            _logger = logger;
        }

        public async Task<List<SystemAttributeDto>> GetAllAttributesListAsync()
        {
            try
            {
                _logger.LogInformation("Loading all system attributes from database...");

                // Create a scope to resolve the DbContext
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var attributes = await context.SystemAttributes
                    .AsNoTracking()
                    .ToListAsync();

                if (attributes == null || attributes.Count == 0)
                {
                    _logger.LogError("No system attributes found in the database. There are supposed to be 4 records.");
                    return new List<SystemAttributeDto>();
                }

                _logger.LogInformation("Found {Count} attributes in database", attributes.Count);

                var attributeDtos = attributes.Select(attr => new SystemAttributeDto
                {
                    Id = attr.Id,
                    Name = attr.Name,
                    Value = attr.Value,
                    ValueType = attr.ValueType,
                    Description = attr.Description ?? string.Empty,
                    UpdateUser = attr.UpdateUser ?? string.Empty,
                    ForeignId = attr.ForeignId,
                    CreatedAt = attr.CreatedAt,
                    UpdatedAt = attr.UpdatedAt
                }).ToList();

                _logger.LogInformation("Successfully converted {Count} database attributes to DTOs", attributeDtos.Count);
                return attributeDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading system attributes from database");
                throw;
            }
        }

        public async Task LoadSystemAttributesAsync()
        {
            try
            {
                _logger.LogInformation("Loading system attributes into memory cache");
                
                var attributes = await GetAllAttributesListAsync();
                _systemAttributes.Clear();

                if (attributes.Count == 0)
                {
                    _logger.LogError("Failed to load any system attributes into memory. The system attributes table should contain 4 records.");
                    return;
                }

                foreach (var dto in attributes)
                {
                    if (!string.IsNullOrEmpty(dto.Name))
                    {
                        _systemAttributes[dto.Name] = dto;
                        _logger.LogDebug("Added attribute: {Name}={Value}", dto.Name, dto.Value);
                    }
                }

                _lastLoaded = DateTime.UtcNow;
                _logger.LogInformation("Successfully loaded {Count} system attributes into memory cache", _systemAttributes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading system attributes into memory");
                throw;
            }
        }

        public Dictionary<string, SystemAttributeDto> GetSystemAttributes()
        {
            if (_systemAttributes.Count == 0)
            {
                _logger.LogWarning("No system attributes loaded in memory. The LoadSystemAttributesAsync method must be called first.");
                
                // Force a sync load if empty
                try
                {
                    LoadSystemAttributesAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load system attributes on-demand");
                }
                
                if (_systemAttributes.Count == 0)
                {
                    _logger.LogError("Still could not load system attributes. The system will operate with empty attributes.");
                }
            }

            return new Dictionary<string, SystemAttributeDto>(_systemAttributes);
        }

        public async Task<bool> UpdateSelectedYearAsync(string sessionId, string yearId, string yearType)
        {
            try
            {
                if (_userSessionService == null || string.IsNullOrEmpty(sessionId))
                {
                    _logger.LogWarning("Cannot update selected year: invalid session or service");
                    return false;
                }

                // These are actual async operations, so use await
                await _userSessionService.UpdateSessionDataAsync(sessionId, "selectedYearId", yearId);
                await _userSessionService.UpdateSessionDataAsync(sessionId, "selectedYearType", yearType);

                // Get additional year data if needed
                if (!string.IsNullOrEmpty(yearId))
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    
                    try
                    {
                        // Fetch year data without assuming property names
                        var yearData = await context.SchoolYears
                            .AsNoTracking()
                            .FirstOrDefaultAsync(y => y.Id.ToString() == yearId);
                            
                        if (yearData != null)
                        {
                            // Use reflection to get the most appropriate property for display
                            // This avoids hard-coding property names that might not exist
                            var yearDisplayValue = GetYearDisplayValue(yearData);
                            await _userSessionService.UpdateSessionDataAsync(sessionId, "selectedYearName", yearDisplayValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load school year data for year ID {YearId}", yearId);
                        // Continue without year data - don't fail the whole operation
                    }
                }

                _logger.LogInformation("Updated selected year for session {SessionId}: YearId={YearId}, YearType={YearType}",
                    sessionId, yearId, yearType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating selected year for session {SessionId}", sessionId);
                return false;
            }
        }

        // Helper method to get the best display value from a SchoolYear object
        private string GetYearDisplayValue(object yearData)
        {
            // Try common property names that might exist on the SchoolYear entity
            // in order of preference
            var possibleProperties = new[]
            {
                "DisplayName",
                "Name", 
                "SchoolYearName",
                "YearName",
                "Title",
                "YearValue",
                "Value",
                "Description"
            };
            
            var type = yearData.GetType();
            
            foreach (var propName in possibleProperties)
            {
                var prop = type.GetProperty(propName);
                if (prop != null)
                {
                    var value = prop.GetValue(yearData)?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }
            
            // Fall back to using the ID as string if no suitable property is found
            var idProp = type.GetProperty("Id");
            if (idProp != null)
            {
                return idProp.GetValue(yearData)?.ToString() ?? "Unknown";
            }
            
            return "Unknown";
        }

        public async Task<Dictionary<string, object>> GetSystemAttributesForSessionAsync(string sessionId)
        {
            try
            {
                var attributes = GetSystemAttributes();
                var result = new Dictionary<string, object>();

                foreach (var attr in attributes.Values)
                {
                    var key = attr.Name;
                    var value = attr.Value;
                    result[key] = value;
                }

                if (_userSessionService != null && !string.IsNullOrEmpty(sessionId))
                {
                    var session = _userSessionService.GetUserSession(sessionId);
                    if (session != null)
                    {
                        // Store the entire attributes collection in session
                        session.SystemAttributes = result;
                        session.SystemAttributesLastLoaded = DateTime.UtcNow;
                        
                        // Individual key/value pairs can be stored as strings if needed
                        foreach (var kvp in result)
                        {
                            await _userSessionService.UpdateSessionDataAsync(sessionId, 
                                $"attr_{kvp.Key}", kvp.Value.ToString());
                        }
                        
                        // Store the last loaded timestamp as a formatted string
                        await _userSessionService.UpdateSessionDataAsync(sessionId,
                            "systemAttributesLastLoadedTime", DateTime.UtcNow.ToString("o"));
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system attributes for session");
                return new Dictionary<string, object>();
            }
        }
    }
}