namespace LigaVolley.Application.Common;

public sealed class ResourceNotFoundException(string resource, object id)
    : Exception($"{resource} '{id}' was not found.")
{
    public string Code { get; } = resource switch
    {
        "Person" => "person_not_found",
        "PersonAdditionalDocument" => "person_additional_document_not_found",
        "Player" => "player_not_found",
        "Coach" => "coach_not_found",
        "Referee" => "referee_not_found",
        _ => "not_found"
    };
}

public sealed class ResourceConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, object?> Extensions { get; init; } = new Dictionary<string, object?>();
}

public sealed class RequestValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
