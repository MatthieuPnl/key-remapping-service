using KeyRemappingService.Domain;

namespace KeyRemappingService.KeyboardCatalog;

public interface IKeyboardCatalog
{
    /// <summary>Returns the keyboard with this name (case-insensitive), or null.</summary>
    KeyboardDefinition? Find(string name);
}