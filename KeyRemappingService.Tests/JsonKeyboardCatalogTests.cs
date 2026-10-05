using KeyRemappingService.KeyboardCatalog;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Loading of keyboard definition files: valid file, case-insensitive lookup, and fail-fast
/// errors (duplicate key code, invalid or out-of-range hex code, duplicate keyboard name).
/// Each test writes its own JSON files in a temporary directory.
/// </summary>
public class JsonKeyboardCatalogTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private JsonKeyboardCatalog LoadWith(string keysJson)
    {
        File.WriteAllText(Path.Combine(_directory, "kb.json"),
            $$"""{ "id": "kb", "name": "My KB", "keys": {{keysJson}} }""");
        return new JsonKeyboardCatalog(_directory, NullLogger<JsonKeyboardCatalog>.Instance);
    }

    [Fact]
    public void ValidFile_IsLoaded_AndFoundCaseInsensitively()
    {
        var catalog = LoadWith("""[ { "code": "0x04", "name": "A" } ]""");

        var keyboard = catalog.Find("  my kb ");

        Assert.NotNull(keyboard);
        Assert.True(keyboard!.HasKey(0x04));
        Assert.False(keyboard.HasKey(0x05));
    }

    [Fact]
    public void UnknownKeyboard_ReturnsNull() =>
        Assert.Null(LoadWith("""[ { "code": "0x04", "name": "A" } ]""").Find("Other"));

    [Fact]
    public void DuplicateCode_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            LoadWith("""[ { "code": "0x04", "name": "A" }, { "code": "0x04", "name": "B" } ]"""));

    [Fact]
    public void InvalidHex_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            LoadWith("""[ { "code": "0xZZ", "name": "A" } ]"""));

    [Fact]
    public void CodeOutOfRange_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            LoadWith("""[ { "code": "0x1FF", "name": "A" } ]"""));

    [Fact]
    public void DuplicateKeyboardName_Throws()
    {
        File.WriteAllText(Path.Combine(_directory, "a.json"),
            """{ "id": "a", "name": "Same", "keys": [] }""");
        File.WriteAllText(Path.Combine(_directory, "b.json"),
            """{ "id": "b", "name": "Same", "keys": [] }""");

        Assert.Throws<InvalidOperationException>(() => new JsonKeyboardCatalog(_directory, NullLogger<JsonKeyboardCatalog>.Instance));
    }
}