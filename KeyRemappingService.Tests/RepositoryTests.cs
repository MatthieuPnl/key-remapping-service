using KeyRemappingService.Domain;

/// <summary>
/// Storage behaviour of the repository on its own: persistence across repository instances
/// (restart), isolation between keyboards, upsert without duplicates, and deletion on reset.
/// Validation is not tested here, it belongs to the service.
/// </summary>
public class RepositoryTests : IDisposable
{
    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Mappings_SurviveANewRepositoryInstance() //Simulates a restart
    {
        _db.CreateRepository().UpdateMappings("kb", [new KeyMapping(0x04, 0x1D)]);

        var reloaded = _db.CreateRepository().GetAllMappings("kb");

        var mapping = Assert.Single(reloaded);
        Assert.Equal(new KeyMapping(0x04, 0x1D), mapping);
    }

    [Fact]
    public void Mappings_AreIsolatedPerKeyboard()
    {
        var repository = _db.CreateRepository();
        repository.UpdateMappings("kb1", [new KeyMapping(0x04, 0x1D)]);

        Assert.Empty(repository.GetAllMappings("kb2"));
    }

    [Fact]
    public void Update_ExistingSource_ChangesTheTarget_WithoutDuplicates()
    {
        var repository = _db.CreateRepository();
        repository.UpdateMappings("kb", [new KeyMapping(0x04, 0x1D)]);
        repository.UpdateMappings("kb", [new KeyMapping(0x04, 0x39)]);

        var mapping = Assert.Single(repository.GetAllMappings("kb"));
        Assert.Equal(0x39, mapping.Target);
    }

    [Fact]
    public void Update_SourceEqualsTarget_DeletesTheMapping()
    {
        var repository = _db.CreateRepository();
        repository.UpdateMappings("kb", [new KeyMapping(0x04, 0x1D)]);
        repository.UpdateMappings("kb", [new KeyMapping(0x04, 0x04)]);

        Assert.Empty(repository.GetAllMappings("kb"));
    }

    [Fact]
    public void Update_LeavesOtherKeysUntouched()
    {
        var repository = _db.CreateRepository();
        repository.UpdateMappings("kb", [new KeyMapping(0x04, 0x1D), new KeyMapping(0x39, 0xE0)]);
        repository.UpdateMappings("kb", [new KeyMapping(0x04, 0x04)]); // reset A only

        var remaining = Assert.Single(repository.GetAllMappings("kb"));
        Assert.Equal(new KeyMapping(0x39, 0xE0), remaining);
    }
}