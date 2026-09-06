using LigaVolley.Application.MatchSheets;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LigaVolley.Api.OpenApi;

public sealed class RulesAssistantSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))
            schema.Properties["warnings"] = new OpenApiSchema {
                Type = "array", Description = "Present on rule_confirmation_required (409). Confirm the concrete codes to retry a direct sporting command; HARD errors have no confirmable bypass.",
                Items = context.SchemaGenerator.GenerateSchema(typeof(RuleWarningDto), context.SchemaRepository)
            };
        if (schema.Properties.TryGetValue("confirmedRuleWarnings", out var confirmations))
        {
            confirmations.Description = "Concrete rule codes explicitly confirmed before this event was persisted. Missing/null means []. Direct commands require coverage of all current warnings. Sync never rejects an otherwise representable persisted event because sporting warnings differ.";
            confirmations.UniqueItems = true;
        }
        if (context.Type == typeof(RulesSnapshotDto))
        {
            schema.Description = "Frozen at OpenMatchSheet and preserved by GET /sheet, sync, replay and takeover. Version 0 is historical compatibility: null substitution maximum means unlimited. New sheets use version 1 and positive effective limits.";
            schema.Properties["maxSubstitutionsPerSet"].Nullable = true;
        }
        if (context.Type == typeof(SubstitutionRequest))
        {
            schema.Description = "One atomic sporting event; each replacement counts as one substitution. Warnings return 409 rule_confirmation_required before persistence. Retry the same UUID with confirmedRuleWarnings. HARD violations cannot be confirmed.";
            schema.Properties["replacements"].MinItems = 1;
        }
        if (schema.Properties.TryGetValue("observedServerMatchPlayerId", out var observed))
            observed.Description = "Exceptional observation for this rally only. Expected server and rotation remain canonically derived; this field never changes the rotational order.";
    }
}
