using KeyRemappingService.Domain;
using KeyRemappingService.Services;

namespace KeyRemappingService.Endpoints;

public static class KeyMappingEndpoints
{
    public static void MapKeyMappingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/keyboards/{name}/mappings");

        group.MapPatch("/", UpdateMappings);
        
        group.MapGet("/", GetAllMappings);
    }

    private static IResult UpdateMappings(
        string name, UpdateMappingsRequest? request, KeyMappingService service)
    {
        // DTO -> domain: the only place where the API format meets the business types.
        var mappings = request?.Mappings?.Select(m => m.ToDomain()).ToList();
        
        var result = service.UpdateMappings(name, mappings);

        return result.Status switch
        {
            ResultStatus.Ok       => Results.NoContent(),
            ResultStatus.NotFound => Results.NotFound(new { errors = result.Errors }),
            _                     => Results.BadRequest(new { errors = result.Errors }),
        };
    }

    private static IResult GetAllMappings(string name, KeyMappingService service)
    {
        var all = service.GetAllMappings(name);
        return all is null
            ? Results.NotFound(new { errors = new[] { $"Unknown keyboard '{name}'" } })
            : Results.Ok(all.Select(m => new KeyMappingResponse(
                new KeyDto(m.Source.Code, m.Source.Name),
                new KeyDto(m.Target.Code, m.Target.Name))));
    }
}