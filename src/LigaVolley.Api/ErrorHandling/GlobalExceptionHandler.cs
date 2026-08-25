using LigaVolley.Application.Common;
using LigaVolley.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LigaVolley.Api.ErrorHandling;

internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Validation failed", "validation_error"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request", "invalid_request"),
            RequestValidationException validation => (StatusCodes.Status400BadRequest, "Invalid request", validation.Code),
            ResourceNotFoundException notFound => (StatusCodes.Status404NotFound, "Resource not found", notFound.Code),
            ResourceConflictException conflict => (StatusCodes.Status409Conflict, "Resource conflict", conflict.Code),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "internal_error")
        };

        httpContext.Response.StatusCode = status;
        var extensions = exception is ResourceConflictException conflictException
            ? conflictException.Extensions
            : new Dictionary<string, object?>();
        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            Extensions = { ["code"] = code }
        };
        foreach (var extension in extensions)
            details.Extensions[extension.Key] = extension.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = details
        });
    }
}
