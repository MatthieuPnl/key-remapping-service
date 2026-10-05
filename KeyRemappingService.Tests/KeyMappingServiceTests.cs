using KeyRemappingService.Services;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Business rules of KeyMappingService: validation (unknown keyboard, unknown keys,
/// duplicate sources, empty request), merge semantics and reset (source equals target),
/// and the "all or nothing" guarantee.
/// Uses a fake catalog and the real repository on a temporary SQLite file, so the merge
/// logic is not re-implemented in a fake. xUnit creates one instance of this class per test,
/// so each test starts from an empty database.
/// </summary>
public class KeyMappingServiceTests : IDisposable
{
    private const string Kb = "Test KB";

    private readonly TempDatabase _db = new();
    private readonly KeyMappingService _service;

    public KeyMappingServiceTests()
    {
        _service = new KeyMappingService(new FakeCatalog(), _db.CreateRepository(), NullLogger<KeyMappingService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private KeyMappingView Get(string keyboard, int sourceCode) =>
        _service.GetAllMappings(keyboard)!.Single(m => m.Source.Code == sourceCode);

    // ---- Reading ----

    [Fact]
    public void GetAllMappings_ReturnsEveryKey_UnmappedKeysPointToThemselves()
    {
        var all = _service.GetAllMappings(Kb)!;

        Assert.Equal(4, all.Count);
        Assert.All(all, m => Assert.Equal(m.Source.Code, m.Target.Code));
    }

    [Fact]
    public void GetAllMappings_UnknownKeyboard_ReturnsNull() =>
        Assert.Null(_service.GetAllMappings("Nope"));

    [Fact]
    public void GetAllMappings_ReturnsKeyNamesAsWellAsCodes()
    {
        _service.UpdateMappings(Kb, [new(0x04, 0x1D)]);

        var mapping = Get(Kb, 0x04);

        Assert.Equal("A", mapping.Source.Name);
        Assert.Equal("Z", mapping.Target.Name);
        Assert.Equal(0x1D, mapping.Target.Code);
    }

    // ---- Writing: valid cases ----

    [Fact]
    public void Update_ValidMapping_IsVisibleInGetAllMappings()
    {
        var result = _service.UpdateMappings(Kb, [new(0x04, 0x1D)]);

        Assert.True(result.Success);
        Assert.Equal(0x1D, Get(Kb, 0x04).Target.Code);
        Assert.Equal(0x1D, Get(Kb, 0x1D).Target.Code); //Other keys untouched
    }

    [Fact]
    public void Update_CapsLockToControl_Works()
    {
        Assert.True(_service.UpdateMappings(Kb, [new(0x39, 0xE0)]).Success);
        Assert.Equal(0xE0, Get(Kb, 0x39).Target.Code);
    }

    [Fact]
    public void Update_MergesWithExistingMappings()
    {
        _service.UpdateMappings(Kb, [new(0x04, 0x1D)]);
        _service.UpdateMappings(Kb, [new(0x39, 0xE0)]);

        Assert.Equal(0x1D, Get(Kb, 0x04).Target.Code); //first one is still there
        Assert.Equal(0xE0, Get(Kb, 0x39).Target.Code);
    }

    [Fact]
    public void Update_ExistingSource_ReplacesItsTarget()
    {
        _service.UpdateMappings(Kb, [new(0x04, 0x1D)]);
        _service.UpdateMappings(Kb, [new(0x04, 0x39)]);

        Assert.Equal(0x39, Get(Kb, 0x04).Target.Code);
    }

    [Fact]
    public void Update_SameRequestTwice_GivesTheSameResult() //idempotence
    {
        _service.UpdateMappings(Kb, [new(0x04, 0x1D)]);
        var second = _service.UpdateMappings(Kb, [new(0x04, 0x1D)]);

        Assert.True(second.Success);
        Assert.Equal(0x1D, Get(Kb, 0x04).Target.Code);
    }

    [Fact]
    public void Update_SourceEqualsTarget_ResetsTheKey()
    {
        _service.UpdateMappings(Kb, [new(0x04, 0x1D)]);
        _service.UpdateMappings(Kb, [new(0x04, 0x04)]);

        Assert.Equal(0x04, Get(Kb, 0x04).Target.Code);
    }

    [Fact]
    public void Update_ResettingAKeyThatIsNotRemapped_Succeeds() =>
        Assert.True(_service.UpdateMappings(Kb, [new(0x04, 0x04)]).Success);

    [Fact]
    public void KeyboardName_IsCaseInsensitive_AndTrimmed()
    {
        Assert.True(_service.UpdateMappings("  test kb ", [new(0x04, 0x1D)]).Success);
        Assert.Equal(0x1D, Get("TEST KB", 0x04).Target.Code);
    }

    // ---- Writing: invalid cases ----

    [Fact]
    public void Update_UnknownKeyboard_ReturnsNotFound()
    {
        var result = _service.UpdateMappings("Nope", [new(0x04, 0x1D)]);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public void Update_UnknownSourceKey_IsInvalid()
    {
        var result = _service.UpdateMappings(Kb, [new(0xFF, 0x04)]);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Update_UnknownTargetKey_IsInvalid()
    {
        var result = _service.UpdateMappings(Kb, [new(0x04, 0xFF)]);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public void Update_DuplicateSource_IsInvalid()
    {
        var result = _service.UpdateMappings(Kb, [new(0x04, 0x1D), new(0x04, 0x39)]);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public void Update_EmptyList_IsInvalid() =>
        Assert.Equal(ResultStatus.Invalid, _service.UpdateMappings(Kb, []).Status);

    [Fact]
    public void Update_NullList_IsInvalid() =>
        Assert.Equal(ResultStatus.Invalid, _service.UpdateMappings(Kb, null).Status);

    [Fact]
    public void Update_WithOneInvalidMapping_SavesNothing() //All or nothing
    {
        //The first mapping is valid, the second is not.
        var result = _service.UpdateMappings(Kb, [new(0x04, 0x1D), new(0x39, 0xFF)]);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(0x04, Get(Kb, 0x04).Target.Code); //The valid one was not saved
    }

    [Fact]
    public void Update_Invalid_LeavesExistingMappingsUntouched()
    {
        _service.UpdateMappings(Kb, [new(0x04, 0x1D)]);

        _service.UpdateMappings(Kb, [new(0x39, 0xFF)]); //Rejected

        Assert.Equal(0x1D, Get(Kb, 0x04).Target.Code);
    }

    [Fact]
    public void Update_Invalid_ReportsEveryError()
    {
        var result = _service.UpdateMappings(Kb, [new(0xFF, 0xFE), new(0x04, 0x1D), new(0x04, 0x39)]);

        Assert.True(result.Errors.Count >= 3); // unknown source, unknown target, duplicate
    }
}