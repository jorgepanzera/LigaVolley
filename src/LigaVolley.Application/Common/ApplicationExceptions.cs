namespace LigaVolley.Application.Common;

public sealed class ResourceNotFoundException(string resource, object id)
    : Exception($"{resource} '{id}' was not found.");

public sealed class ResourceConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class RequestValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
