using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using GiddyEdu.Infrastructure.Hr;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            StaffImportValidationException => (StatusCodes.Status400BadRequest, "Staff import could not be reviewed"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            JsonException => (StatusCodes.Status400BadRequest, "Invalid JSON"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            TeachingAssignmentConflictException => (StatusCodes.Status409Conflict, "Teaching assignment already exists"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Operation could not be completed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };
        if (status == 500) logger.LogError(exception, "Unhandled API exception"); else logger.LogWarning(exception, "API request failed with status {StatusCode}", status);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title, Detail = exception is TeachingAssignmentConflictException or StaffImportValidationException ? exception.Message : null, Instance = context.Request.Path, Extensions = { ["correlationId"] = context.Response.Headers["X-Correlation-ID"].ToString() } }, cancellationToken);
        return true;
    }
}
