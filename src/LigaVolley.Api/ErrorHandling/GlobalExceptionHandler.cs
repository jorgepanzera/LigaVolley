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
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", "not_found"),
            ResourceConflictException conflict => (StatusCodes.Status409Conflict, "Resource conflict", conflict.Code),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "internal_error")
        };

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
                Extensions = { ["code"] = code }
            }
        });
    }
}
