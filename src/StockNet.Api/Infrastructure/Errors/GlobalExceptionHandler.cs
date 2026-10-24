using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Text.Json;

namespace StockNet.Api.Infrastructure.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled API exception. ExceptionType: {ExceptionType}. Message: {Message}. TraceId: {TraceId}",
            exception.GetType().FullName,
            exception.Message,
            httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "The request could not be completed.",
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problemDetailsContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        };

        if (!await problemDetailsService.TryWriteAsync(problemDetailsContext))
        {
            httpContext.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                httpContext.Response.Body,
                problem,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);
        }

        return true;
    }
}
