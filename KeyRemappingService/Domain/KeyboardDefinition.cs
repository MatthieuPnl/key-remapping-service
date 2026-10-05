namespace KeyRemappingService.Domain;

public record KeyDefinition(int Code, string Name);

public record KeyboardDefinition(
    string Id, string Name, IReadOnlyList<KeyDefinition> Keys)
{
    private readonly HashSet<int> _codes = Keys.Select(k => k.Code).ToHashSet();

    public bool HasKey(int code) => _codes.Contains(code);
}