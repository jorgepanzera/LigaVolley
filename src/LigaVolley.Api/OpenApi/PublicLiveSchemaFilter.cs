using LigaVolley.Application.PublicQueries;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LigaVolley.Api.OpenApi;

public sealed class PublicLiveSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(PublicLiveMatchDto)) return;
        // OpenAPI 3.0 reference siblings are ignored; wrap the reference to document null explicitly.
        schema.Properties["servingPlayer"] = new OpenApiSchema
        {
            Nullable = true,
            Description = "Canonical current server: jerseyNumber and displayName only; null when no active server is available.",
            AllOf = [schema.Properties["servingPlayer"]]
        };
    }
}
