using BudgetPrevisionnel.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.ErrorHandling;

/// <summary>
/// Single place that turns an exception into an HTTP response, replacing the
/// try/catch-per-exception-type that used to live in every controller action. Every
/// Application-layer exception that should reach the client as something other than a
/// bare 500 derives from AppException (see its doc comment) and already carries the
/// right StatusCode/Title - this handler just writes them out. Anything else is a real
/// bug: logged at Error with the full exception, and the client gets a generic 500
/// with no internal detail leaked.
/// </summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is AppException appException)
        {
            logger.LogInformation(
                "Request {Method} {Path} rejected: {Title} - {Message}",
                httpContext.Request.Method, httpContext.Request.Path, appException.Title, appException.Message);

            httpContext.Response.StatusCode = appException.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = appException.Title,
                Detail = appException.Message,
                Status = appException.StatusCode
            }, cancellationToken);

            return true;
        }

        logger.LogError(
            exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "An unexpected error occurred",
            Status = StatusCodes.Status500InternalServerError
        }, cancellationToken);

        return true;
    }
}
