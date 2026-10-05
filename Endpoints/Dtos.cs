using System.Text.Json.Serialization;
using KeyRemappingService.Domain;

namespace KeyRemappingService.Endpoints;

/// <summary>A mapping as it appears in the JSON of the API.</summary>
public record KeyMappingDto(
    [property: JsonRequired] int Source,
    [property: JsonRequired] int Target)
{
    public KeyMapping ToDomain() => new(Source, Target);
    public static KeyMappingDto From(KeyMapping mapping) => new(mapping.Source, mapping.Target);
}

public record UpdateMappingsRequest(
    [property: JsonRequired] List<KeyMappingDto> Mappings);
    
public record KeyDto(int Code, string Name);
public record KeyMappingResponse(KeyDto Source, KeyDto Target);