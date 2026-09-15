namespace BudgetPrevisionnel.Application.Common;

/// <summary>
/// A request body referenced another entity (a category id, a linked bank account id)
/// that doesn't exist or isn't visible to the caller. Deliberately a ValidationException
/// (400), not a NotFoundException (404): 404 means the URL's own resource is missing,
/// but here the URL/endpoint is fine - it's one field inside the payload that's bad,
/// same class of problem as any other invalid input. Keeping this distinct from e.g.
/// CategoryNotFoundException (used only when a category IS the primary resource, as in
/// PUT /api/categories/{id}) is what lets a global exception handler map every
/// NotFoundException to 404 without exceptions to the rule.
/// </summary>
public sealed class InvalidReferenceException(string entityName, int id)
    : ValidationException($"{entityName} {id} does not exist or is not accessible.");
