using KeyRemappingService.Domain;

namespace KeyRemappingService.Services;

using KeyRemappingService.Data;
using KeyRemappingService.KeyboardCatalog;

public enum ResultStatus { Ok, NotFound, Invalid }

/// <summary>A key of the keyboard and the key it currently behaves as.</summary>
public record KeyMappingView(KeyDefinition Source, KeyDefinition Target);

/// <summary>Outcome of an operation: success, unknown keyboard, or invalid input.</summary>
public record Result(ResultStatus Status, IReadOnlyList<string> Errors)
{
    public static Result Ok() => new(ResultStatus.Ok, []);
    public static Result NotFound(string error) => new(ResultStatus.NotFound, [error]);
    public static Result Invalid(IReadOnlyList<string> errors) => new(ResultStatus.Invalid, errors);
}

public class KeyMappingService
{
    private readonly IKeyboardCatalog _catalog;
    private readonly IKeyMappingRepository _repo;
    private readonly ILogger<KeyMappingService> _logger;
    
    public KeyMappingService(IKeyboardCatalog catalog, IKeyMappingRepository repo, ILogger<KeyMappingService> logger)
    {
        this._catalog = catalog;
        this._repo = repo;
        this._logger = logger;
    }
    
    
    /// <summary>
    /// Validates then merges the mappings into the keyboard configuration.
    /// Nothing is saved if any mapping is invalid (all or nothing).
    /// A mapping whose source equals its target resets that key.
    /// </summary>
    public Result UpdateMappings(string keyboardName, IReadOnlyList<KeyMapping>? mappings)
    {
        if (mappings is null || mappings.Count == 0)
            return Result.Invalid(["At least one mapping is required"]);
        
        var keyboard = _catalog.Find(keyboardName);
        if (keyboard is null)
            return Result.NotFound($"Unknown keyboard '{keyboardName}'");

        var errors = new List<string>();

        foreach (var m in mappings)
        {
            if (!keyboard.HasKey(m.Source))
                errors.Add($"Source key 0x{m.Source:X2} does not exist on {keyboard.Name}");
            if (!keyboard.HasKey(m.Target))
                errors.Add($"Target key 0x{m.Target:X2} does not exist on {keyboard.Name}");
        }

        //Optional check if the same source appears twice. If removed, the latest pair would be the final value
        foreach (var group in mappings.GroupBy(m => m.Source).Where(g => g.Count() > 1))
            errors.Add($"Source key 0x{group.Key:X2} appears more than once");

        if (errors.Count > 0)
        {
            _logger.LogWarning("Rejected {Count} mapping(s) for {Keyboard}: {ErrorCount} error(s)",
                mappings.Count, keyboard.Name, errors.Count);
            return Result.Invalid(errors);
        }

        _repo.UpdateMappings(keyboard.Id, mappings);
        
        _logger.LogInformation("Applied {Count} mapping(s) to {Keyboard}", mappings.Count, keyboard.Name);
        
        return Result.Ok();
    }
    
    /// <summary>
    /// Returns every key of the keyboard with its current target.
    /// A key that is not remapped points to itself. Null if the keyboard is unknown.
    /// </summary>
    public IReadOnlyList<KeyMappingView>? GetAllMappings(string keyboardName)
    {
        var keyboard = _catalog.Find(keyboardName);
        if (keyboard is null) return null;

        var targetByKey = _repo.GetAllMappings(keyboard.Id).ToDictionary(m => m.Source, m => m.Target);
        var keyByCode = keyboard.Keys.ToDictionary(k => k.Code);

        return keyboard.Keys
            .Select(k => new KeyMappingView(
                k,
                keyByCode[targetByKey.GetValueOrDefault(k.Code, k.Code)]))
            .ToList();
    }
}