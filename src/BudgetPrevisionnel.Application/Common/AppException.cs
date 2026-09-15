namespace BudgetPrevisionnel.Application.Common;

/// <summary>
/// Base for every exception the Api layer translates directly into an HTTP error
/// response instead of an unhandled 500 - see Api/ErrorHandling/AppExceptionHandler.
/// StatusCode/Title are fixed by the semantic subclass below, not chosen per call site,
/// so every feature's errors map to HTTP consistently without a try/catch in every
/// controller action.
/// </summary>
public abstract class AppException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

/// <summary>The resource identified by the request URL doesn't exist (404). Only for the
/// PRIMARY resource a route addresses - a bad reference to some OTHER entity inside a
/// request body (a category id, a linked account id) is a ValidationException instead,
/// via InvalidReferenceException; see its doc comment for why that distinction matters.</summary>
public abstract class NotFoundException(string message) : AppException(message, 404, "Not found");

/// <summary>The request itself is invalid: missing/malformed fields, a business rule
/// FluentValidation can't express cross-DTO, or a reference to another entity that
/// doesn't exist or isn't yours (400).</summary>
public abstract class ValidationException(string message) : AppException(message, 400, "Invalid request");

/// <summary>The request is well-formed but conflicts with existing state - a duplicate,
/// a uniqueness violation (409).</summary>
public abstract class ConflictException(string message) : AppException(message, 409, "Conflict");

/// <summary>The caller is authenticated but not allowed to perform this action - e.g.
/// editing a system category (403).</summary>
public abstract class ForbiddenException(string message) : AppException(message, 403, "Forbidden");

/// <summary>Authentication itself failed - wrong credentials (401).</summary>
public abstract class UnauthorizedException(string message) : AppException(message, 401, "Unauthorized");
