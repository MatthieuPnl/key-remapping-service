using KeyRemappingService.Data;
using KeyRemappingService.Domain;
using KeyRemappingService.KeyboardCatalog;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>Small in-memory keyboard so tests do not depend on the real JSON file.</summary>
public class FakeCatalog : IKeyboardCatalog
{
    private readonly KeyboardDefinition _keyboard = new("test-kb", "Test KB",
    [
        new KeyDefinition(0x04, "A"),
        new KeyDefinition(0x1D, "Z"),
        new KeyDefinition(0x39, "Caps Lock"),
        new KeyDefinition(0xE0, "Left Ctrl"),
    ]);

    public KeyboardDefinition? Find(string name) =>
        name.Trim().Equals("Test KB", StringComparison.OrdinalIgnoreCase) ? _keyboard : null;
}

public sealed class TestDbContextFactory : IDbContextFactory<KeyMappingDbContext>
{
    private readonly DbContextOptions<KeyMappingDbContext> _options;

    public TestDbContextFactory(DbContextOptions<KeyMappingDbContext> options)
    {
        _options = options;
    }

    public KeyMappingDbContext CreateDbContext() => new(_options);
}

/// <summary>A temporary SQLite file, deleted when the test ends.</summary>
public sealed class TempDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    // Pooling=False releases the file as soon as a connection closes,
    // otherwise Windows cannot delete it.
    private string ConnectionString => $"Data Source={_path};Pooling=False";

    /// <summary>Each call returns a new repository on the same file (simulates a restart).</summary>
    public EfKeyMappingRepository CreateRepository()
    {
        var options = new DbContextOptionsBuilder<KeyMappingDbContext>()
            .UseSqlite(ConnectionString)
            .Options;

        var repository = new EfKeyMappingRepository(new TestDbContextFactory(options));
        repository.Initialize();
        return repository;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }
}