using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using PetelApp.Api.Models;
using PetelApp.Api.Session;
using System.Text.Json;

namespace PetelApp.Api.Services
{
    public class SystemAttributeService
    {
        private readonly AppDbContext _context;
        private readonly UserSessionService _userSessionService;
        private readonly ILogger<SystemAttributeService> _logger;
        private static readonly Dictionary<string, SystemAttributeDto> _systemAttributes = new();
        private static readonly Dictionary<string, SystemAttributeDto> _systemAttributesByForeignId = new();
        private static DateTime _lastLoaded = DateTime.MinValue;

        public SystemAttributeService(AppDbContext context, UserSessionService userSessionService, ILogger<SystemAttributeService> logger)
        {
            _context = context;
            _userSessionService = userSessionService;
            _logger = logger;
        }

        // Method for HostedService - returns list of DTOs
        public async Task<List<SystemAttributeDto>> GetAllAttributesListAsync()
        {
            try
            {
                _logger.LogInformation("Loading all system attributes from database...");

                var attributes = await _context.SystemAttributes
                    .AsNoTracking()
                    .ToListAsync();

                var attributeDtos = attributes.Select(attr => new SystemAttributeDto
                {
                    Id = attr.Id,
                    AttributeName = attr.AttributeName,
                    AttributeValue = attr.AttributeValue,
                    AttributeType = attr.AttributeType,
                    DefaultValue = attr.DefaultValue,
                    AllowedValues = attr.AllowedValues,
                    Description = attr.Description ?? string.Empty,
                    Category = attr.Category,
                    IsRequired = attr.IsRequired,
                    IsActive = attr.IsActive,
                    SortOrder = attr.SortOrder,
                    ForeignId = attr.ForeignId?.ToString() ?? string.Empty,
                   // Tenant = attr.Tenant,
                    CreatedBy = attr.CreatedBy,
                    CreatedAt = attr.CreatedAt ?? DateTime.MinValue,
                    UpdatedBy = attr.UpdatedBy,
                    UpdatedAt = attr.UpdatedAt ?? DateTime.MinValue
                }).ToList();

                _logger.LogInformation("Loaded {Count} system attributes", attributeDtos.Count);
                return attributeDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all system attributes");
                throw;
            }
        }

        // Method for simple access - returns dictionary
        public async Task<Dictionary<string, string>> GetAllAttributesDictionaryAsync()
        {
            try
            {
                var attributes = await _context.SystemAttributes
                    .Where(a => !string.IsNullOrEmpty(a.Name))
                    .ToDictionaryAsync(a => a.Name!, a => a.Value ?? string.Empty);

                return attributes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all system attributes");
                return new Dictionary<string, string>();
            }
        }

        public async Task LoadSystemAttributesAsync()
        {
            try
            {
                var attributes = await GetAllAttributesListAsync();

                _systemAttributes.Clear();
                _systemAttributesByForeignId.Clear();

                foreach (var dto in attributes)
                {
                    if (!string.IsNullOrEmpty(dto.AttributeName))
                    {
                        _systemAttributes[dto.AttributeName] = dto;
                    }

                    if (!string.IsNullOrEmpty(dto.ForeignId))
                    {
                        _systemAttributesByForeignId[dto.ForeignId] = dto;
                    }
                }

                _lastLoaded = DateTime.UtcNow;
                _logger.LogInformation("Loaded {Count} system attributes into memory", attributes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading system attributes");
                throw;
            }
        }

        public async Task<Dictionary<string, SystemAttributeDto>> GetSystemAttributesForSessionAsync(string sessionId)
        {
            var session = _userSessionService.GetUserSession(sessionId);
            if (session == null)
            {
                return new Dictionary<string, SystemAttributeDto>();
            }

            // Update session with current system attributes
            session.SystemAttributes = _systemAttributes.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
            session.SystemAttributeForeignIds = _systemAttributesByForeignId.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
            session.SystemAttributesLastLoaded = _lastLoaded;

            return _systemAttributes;
        }

        public async Task<SystemAttributeDto?> GetSystemAttributeAsync(string sessionId, string attributeName)
        {
            var session = _userSessionService.GetUserSession(sessionId);
            if (session == null)
            {
                return null;
            }

            if (_lastLoaded == DateTime.MinValue || DateTime.UtcNow - _lastLoaded > TimeSpan.FromHours(1))
            {
                await LoadSystemAttributesAsync();
            }

            return _systemAttributes.TryGetValue(attributeName, out var attribute) ? attribute : null;
        }

        public Task UpdateSelectedYearAsync(string sessionId, string yearId, string yearType)
        {
            var session = _userSessionService.GetUserSession(sessionId);
            if (session == null)
            {
                return Task.CompletedTask;
            }

            // Update session selected year
            session.SelectedYear = new { id = yearId, type = yearType };
            session.SelectedYearId = yearId;
            session.SelectedYearType = yearType;
            
            return Task.CompletedTask;
        }

        // Simple attribute retrieval by name
        public async Task<string> GetAttributeValueAsync(string attributeName)
        {
            try
            {
                var attribute = await _context.SystemAttributes
                    .FirstOrDefaultAsync(a => a.Name == attributeName);

                return attribute?.Value ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system attribute: {AttributeName}", attributeName);
                return string.Empty;
            }
        }

        public Dictionary<string, SystemAttributeDto> GetSystemAttributes()
        {
            return _systemAttributes;
        }

        public Dictionary<string, SystemAttributeDto> GetSystemAttributesByForeignId()
        {
            return _systemAttributesByForeignId;
        }

        // Add the missing GetAllAttributesAsync method to SystemAttributeService

        public async Task<List<SystemAttributeDto>> GetAllAttributesAsync()
        {
            // Bridge the method call to the existing implementation
            return await GetAllAttributesListAsync();
        }
        public DateTime GetLastLoaded()
        {
            return _lastLoaded;
        }
    }
}