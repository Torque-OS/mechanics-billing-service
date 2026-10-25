using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Api.Middleware;

/// <summary>Same error contract as mechanics-software: <c>{ "error": "..." }</c> with 403/404/409/422/500.</summary>
[ExcludeFromCodeCoverage]
public sealed partial class ExceptionHandlingMiddleware(
    RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Message),
            ForbiddenException forbidden => (StatusCodes.Status403Forbidden, forbidden.Message),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message),
            DomainException domain => (StatusCodes.Status422UnprocessableEntity, domain.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            LogUnhandledException(exception);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(body);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private partial void LogUnhandledException(Exception exception);
}
