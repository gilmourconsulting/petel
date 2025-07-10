using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using ModelSystemAttribute = PetelApp.Api.Models.SystemAttribute;

public class SystemAttributeService
{
    private readonly AppDbContext _context;
    private Dictionary<string, ModelSystemAttribute> _cache = new();
    private bool _isLoaded = false;

    public SystemAttributeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task EnsureLoadedAsync()
    {
        if (!_isLoaded)
        {
            await LoadAttributesAsync();
            _isLoaded = true;
        }
    }

    public async Task LoadAttributesAsync()
    {
        var attributes = await _context.SystemAttributes.ToListAsync();
        _cache = attributes.ToDictionary(a => a.Name, a => a);
    }

    public List<ModelSystemAttribute> GetAllAttributes()
    {
        return _cache.Values.ToList();
    }
}