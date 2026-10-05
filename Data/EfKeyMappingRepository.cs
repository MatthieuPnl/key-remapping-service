using KeyRemappingService.Domain;
using Microsoft.EntityFrameworkCore;

namespace KeyRemappingService.Data;

public class EfKeyMappingRepository : IKeyMappingRepository
{
    private readonly IDbContextFactory<KeyMappingDbContext> _contextFactory;

    public EfKeyMappingRepository(IDbContextFactory<KeyMappingDbContext> contextFactory)
    {
        this._contextFactory = contextFactory;
    }
    
    public void Initialize()
    {
        using var context = _contextFactory.CreateDbContext();
        //Creates the database and the table if they do not exist
        context.Database.EnsureCreated();
    }

    public IReadOnlyList<KeyMapping> GetAllMappings(string keyboardId)
    {
        using var context = _contextFactory.CreateDbContext();

        //AsNoTracking: read-only query, no change tracking needed
        return context.KeyMappings
            .AsNoTracking()
            .Where(m => m.Keyboard == keyboardId)
            .Select(m => new KeyMapping(m.SourceCode, m.TargetCode))
            .ToList();
    }
    
    public void UpdateMappings(string keyboardId, IEnumerable<KeyMapping> mappings)
    {
        var newMappingsList = mappings.ToList();
        var sources = newMappingsList.Select(m => m.Source).ToList();

        using var context = _contextFactory.CreateDbContext();

        //One query to load the rows that may be touched
        var existing = context.KeyMappings
            .Where(m => m.Keyboard == keyboardId && sources.Contains(m.SourceCode))
            .ToDictionary(m => m.SourceCode);

        foreach (var mapping in newMappingsList)
        {
            existing.TryGetValue(mapping.Source, out var row);

            if (mapping.Source == mapping.Target)
            {
                //If Source equals Target, we take it as a "back to default"
                //And remove the related row (if it exists)
                if (row is not null)
                    context.KeyMappings.Remove(row);
            }
            else if (row is not null)
            {
                row.TargetCode = mapping.Target;
            }
            else
            {
                context.KeyMappings.Add(new KeyMappingEntity
                {
                    Keyboard = keyboardId,
                    SourceCode = mapping.Source,
                    TargetCode = mapping.Target,
                });
            }
        }

        //SaveChanges runs all pending changes in a single transaction
        //Everything is applied, or nothing changes
        context.SaveChanges();
    }
}