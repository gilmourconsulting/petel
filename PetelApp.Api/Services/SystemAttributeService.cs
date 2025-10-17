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
        private readonly AppDbContext _context;
        private readonly ILogger<SystemAttributeService> _logger;
        private readonly SystemAttributeCache _cache;

        public SystemAttributeService(AppDbContext context, ILogger<SystemAttributeService> logger, SystemAttributeCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        // Called by loader at startup
        public async Task LoadAttributesAsync()
        {
            _cache.Attributes = await _context.SystemAttributes
                .Select(a => new SystemAttributeDto
                {
                    Id = a.Id,
                    Description = a.Description,
                    Value = a.Value,
                    ValueType = a.ValueType,
                    Name = a.Name,
                    ForeignId = a.ForeignId,
                    UpdatedAt = a.UpdatedAt
                })
                .ToListAsync();
            _logger.LogInformation("System attributes loaded into memory: {Count}", _cache.Attributes.Count);
        }

        public Task<List<SystemAttributeDto>> GetAllAttributesListAsync()
        {
            // Return cached attributes from the singleton cache
            return Task.FromResult(_cache.Attributes);
        }
    }
}