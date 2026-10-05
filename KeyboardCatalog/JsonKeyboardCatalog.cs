using KeyRemappingService.Domain;

namespace KeyRemappingService.KeyboardCatalog;

using System.Text.Json;
using KeyRemappingService.Utils;


public class JsonKeyboardCatalog : IKeyboardCatalog
{
    private readonly Dictionary<string, KeyboardDefinition> _keyboardByName =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads every .json file of the directory. Fails fast: an invalid file
    /// stops the startup rather than letting bad data in.
    /// </summary>
    public JsonKeyboardCatalog(string directory, ILogger<JsonKeyboardCatalog> logger)
    {
        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            var keyboard = Load(file);
            if (!_keyboardByName.TryAdd(keyboard.Name, keyboard))
                throw new InvalidOperationException(
                    $"Duplicate keyboard name '{keyboard.Name}' in {file}");
            
            logger.LogInformation("Loaded keyboard {Keyboard} with {KeyCount} keys from {File}",
                keyboard.Name, keyboard.Keys.Count, Path.GetFileName(file));
        }
        
        if (_keyboardByName.Count == 0)
            logger.LogWarning("No keyboard definition found in {Directory}", directory);

    }

    public KeyboardDefinition? Find(string name) =>
        _keyboardByName.GetValueOrDefault(name.Trim());

    private static KeyboardDefinition Load(string path)
    {
        var raw = JsonSerializer.Deserialize<RawKeyboard>(
                      File.ReadAllText(path),
                      new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? throw new InvalidOperationException($"Empty file: {path}");

        if (string.IsNullOrWhiteSpace(raw.Id) || string.IsNullOrWhiteSpace(raw.Name))
            throw new InvalidOperationException($"Missing id or name in {path}");

        var keys = raw.Keys.Select(k => new KeyDefinition(Utils.ParseHex(k.Code, path), k.Name)).ToList();

        //HID codes are 8-bit unsigned integers
        var outOfRange = keys.FirstOrDefault(k => k.Code is < 0 or > 0xFF);
        if (outOfRange is not null)
            throw new InvalidOperationException(
                $"Code {outOfRange.Code} out of range (0-255) in {path}");

        var duplicate = keys.GroupBy(k => k.Code).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException(
                $"Duplicate code 0x{duplicate.Key:X2} in {path}");

        return new KeyboardDefinition(raw.Id, raw.Name.Trim(), keys);
    }


    
    private record RawKey(string Code, string Name);
    private record RawKeyboard(string Id, string Name, List<RawKey> Keys);
}