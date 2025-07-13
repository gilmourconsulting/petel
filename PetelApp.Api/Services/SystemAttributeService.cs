using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using ModelSystemAttribute = PetelApp.Api.Models.SystemAttribute;

public class SystemAttributeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Dictionary<string, ModelSystemAttribute> _cache = new();

    public SystemAttributeService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task LoadAttributesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var attributes = await context.SystemAttributes.ToListAsync();
        _cache = attributes.ToDictionary(a => a.Name, a => a);

        Console.WriteLine($"Loaded {attributes.Count} system attributes");
        foreach (var attr in attributes)
        {
            Console.WriteLine($"  - {attr.Name}: {attr.Value}");
        }
    }

    public List<ModelSystemAttribute> GetAllAttributes()
    {
        return _cache.Values.ToList();
    }
}