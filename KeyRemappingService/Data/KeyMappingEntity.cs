namespace KeyRemappingService.Data;

/// <summary>A remapped key as stored in the database (one row per remap).</summary>
public class KeyMappingEntity
{
    public required string Keyboard { get; set; }
    public int SourceCode { get; set; }
    public int TargetCode { get; set; }
}