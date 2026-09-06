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
            schema.Description = "Frozen at OpenMatchSheet and preserved by GET /sheet, sync, replay and takeover. Version 0 is historical compatibility: null substitution maximum means unlimited. New sheets use protocol/snapshot version 2: observed libero replacements only; optional plans never mutate court. Existing v0/v1 snapshots and accepted automatic history are preserved.";
            schema.Properties["maxSubstitutionsPerSet"].Nullable = true;
        }
        if (context.Type == typeof(LiberoEnterRequest))
            schema.Description = "Observed regular-to-libero or direct libero-to-second-libero replacement. ReplacedMatchPlayerId is the current effective outgoing occupant; the current regular logical player is preserved. Allowed after StartSet before the first point. Never counts as a regular substitution. At most one effective libero per side; sporting warnings require confirmation.";
        if (context.Type == typeof(LiberoExitRequest))
            schema.Description = "Observed exit of the acting libero. Restores the current regular logical occupant, including any substitution under coverage.";
        if (context.Type == typeof(SetLineupRequest))
            schema.Description = "Six distinct regulars, no declared libero. Optional liberoMatchPlayerId/liberoLogicalPositions (0..5) are suggestion preferences only. Missing selection or empty positions means no plan. Multiple candidate slots are allowed; starting never inserts a libero.";
        if (context.Type == typeof(ScorerLiberoStateDto))
            schema.Description = "Confirmed coverage, indexed by logical position. Automatic=true identifies historical automatic rows without a matching LIBERO_ENTER event; new observed replacements are false. ReplacedMatchPlayerId records the regular at entry; the current regular is derived from lineup and substitutions.";
        if (context.Type == typeof(ScorerSyncEvent))
            schema.Properties["payload"].Description = "Original immutable sporting payload. New clients include observedLiberoReplacements=true. Required for new START_SET/POINT/CORRECT_LAST_POINT events on protocol v2 sheets; absent flag on v0/v1 uses legacy replay. Incompatibility returns scorer_protocol_incompatible without consuming sequence. Suggestions are never sent.";
        if (context.Type == typeof(SubstitutionRequest))
        {
            schema.Description = "One atomic sporting event; each replacement counts as one substitution. Warnings return 409 rule_confirmation_required before persistence. Retry the same UUID with confirmedRuleWarnings. HARD violations cannot be confirmed.";
            schema.Properties["replacements"].MinItems = 1;
        }
        if (schema.Properties.TryGetValue("observedServerMatchPlayerId", out var observed))
            observed.Description = "Exceptional observation for this rally only. Expected server and rotation remain canonically derived; this field never changes the rotational order.";
    }
}
