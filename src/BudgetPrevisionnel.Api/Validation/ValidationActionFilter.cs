using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BudgetPrevisionnel.Api.Validation;

/// <summary>
/// FluentValidation's own ASP.NET Core auto-validation packages are no longer
/// maintained by the FluentValidation team, who now recommend wiring this kind of
/// filter yourself - deliberately small and self-owned rather than adding a
/// third-party package for something this short. Runs any registered
/// IValidator&lt;T&gt; against every action argument whose type has one, and short-circuits
/// with the same ValidationProblemDetails shape [ApiController] already produces for
/// DataAnnotations failures - one consistent 400 shape regardless of which mechanism
/// caught the problem.
/// </summary>
public sealed class ValidationActionFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            ValidationResult result = await validator.ValidateAsync(validationContext);

            foreach (var error in result.Errors)
            {
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            return;
        }

        await next();
    }
}
