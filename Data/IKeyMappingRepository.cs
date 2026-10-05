using KeyRemappingService.Domain;

namespace KeyRemappingService.Data;

public interface IKeyMappingRepository
{
    /// <summary>Creates the table if it does not exist yet.</summary>
    void Initialize();
    
    /// <summary>
    /// Returns only the remapped keys of a keyboard. Keys that are not
    /// remapped are not stored, the service fills them in.
    /// </summary> 
    IReadOnlyList<KeyMapping> GetAllMappings(string keyboardId);
    
    /// <summary>
    /// Merges the given mappings into the existing configuration of a keyboard.
    /// A mapping whose source differs from its target is inserted or updated.
    /// A mapping whose source equals its target (e.g. A -> A) resets that key:
    /// the stored remapping is deleted. Keys not mentioned are left untouched.
    /// Atomic: either everything is applied, or nothing changes.
    /// </summary>
    void UpdateMappings(string keyboardId, IEnumerable<KeyMapping> mappings);
}