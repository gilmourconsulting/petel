using PetelApp.Api.Data;
using PetelApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace PetelApp.Api.Services;

/// <summary>
/// Singleton cache for school attribute types and their values.
/// Loaded at startup similar to SystemAttributeCache.
/// </summary>
public class SchoolAttributeCache
{
    private Dictionary<int, SchoolAttributeType> _attributeTypes = new();
    private Dictionary<int, List<SchoolAttributeTypeValue>> _attributeValues = new();
    private readonly ILogger<SchoolAttributeCache> _logger;
    private bool _isLoaded = false;

    public SchoolAttributeCache(ILogger<SchoolAttributeCache> logger)
    {
        _logger = logger;
    }

    public async Task LoadAsync(AppDbContext context)
    {
        if (_isLoaded)
        {
            _logger.LogInformation("School attribute cache already loaded, skipping reload");
            return;
        }

        try
        {
            _logger.LogInformation("Loading school attribute types and values into cache...");

            // Load attribute types
            var attributeTypes = await context.SchoolAttributeTypes
                .AsNoTracking()
                .ToListAsync();

            _attributeTypes = attributeTypes.ToDictionary(a => a.Id, a => a);

            // Load attribute values
            var attributeValues = await context.SchoolAttributeTypeValues
                .AsNoTracking()
                .Where(v => v.IsValid)
                .OrderBy(v => v.SortOrder)
                .ToListAsync();

            _attributeValues = attributeValues
                .GroupBy(v => v.SchoolAttributeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            _isLoaded = true;

            _logger.LogInformation(
                "School attribute cache loaded successfully: {TypeCount} types, {ValueCount} values",
                _attributeTypes.Count,
                attributeValues.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading school attribute cache");
            throw;
        }
    }

    public SchoolAttributeType? GetAttributeType(int id)
    {
        return _attributeTypes.TryGetValue(id, out var type) ? type : null;
    }

    public SchoolAttributeType? GetAttributeTypeByName(string name)
    {
        return _attributeTypes.Values.FirstOrDefault(a => 
            a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public List<SchoolAttributeTypeValue> GetAttributeValues(int attributeTypeId)
    {
        return _attributeValues.TryGetValue(attributeTypeId, out var values) 
            ? values 
            : new List<SchoolAttributeTypeValue>();
    }

    public IEnumerable<SchoolAttributeType> GetAllAttributeTypes()
    {
        return _attributeTypes.Values;
    }

    public void Refresh()
    {
        _isLoaded = false;
        _attributeTypes.Clear();
        _attributeValues.Clear();
    }
}