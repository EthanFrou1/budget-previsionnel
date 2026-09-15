using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.Auth;

public sealed class EmailAlreadyInUseException(string email)
    : ConflictException($"An account already exists for '{email}'.");

public sealed class InvalidCredentialsException()
    : UnauthorizedException("Invalid email or password.");
