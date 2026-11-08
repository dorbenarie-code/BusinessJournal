using System.Diagnostics;
using BusinessJournal.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BusinessJournal.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public const string UnexpectedErrorTitle = "Server Error";
    public const string UnexpectedErrorDetail =
        "An unexpected error occurred. If the problem persists, report the traceId.";

    private const string ProblemJsonContentType = "application/problem+json";

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var knownError = MapKnownException(exception);

        ProblemDetails problemDetails;

        if (knownError is { } error)
        {
            // Intentional application errors. Their messages are written for API clients.
            _logger.LogWarning(
                "Request {Method} {Path} failed with {StatusCode} ({ExceptionType}). TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                error.StatusCode,
                exception.GetType().Name,
                traceId);

            problemDetails = new ProblemDetails
            {
                Status = error.StatusCode,
                Title = error.Title,
                Detail = exception.Message
            };
        }
        else
        {
            // Anything else may carry SQL, file system or configuration details.
            // The full exception is kept in the server log only.
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = UnexpectedErrorTitle,
                Detail = UnexpectedErrorDetail
            };
        }

        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode =
            problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: ProblemJsonContentType,
            cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title)? MapKnownException(Exception exception)
    {
        return exception switch
        {
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource Not Found"),
            ConflictException => (StatusCodes.Status409Conflict, "Business Conflict"),

            // A required resource (the calendar lock) was busy. Nothing was changed; retry is safe.
            ServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable"),

            // Guard clauses (ThrowIfNull, range checks) signal a server-side bug,
            // not invalid client input, so they get the generic 500 response.
            ArgumentNullException or ArgumentOutOfRangeException => null,

            // Domain rules (TimeRange, required text) reject client input this way.
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Request"),

            _ => null
        };
    }
}