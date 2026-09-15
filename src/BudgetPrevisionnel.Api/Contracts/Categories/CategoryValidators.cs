using FluentValidation;

namespace BudgetPrevisionnel.Api.Contracts.Categories;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class CreateCategoryRuleRequestValidator : AbstractValidator<CreateCategoryRuleRequest>
{
    public CreateCategoryRuleRequestValidator()
    {
        RuleFor(x => x.MatchPattern).NotEmpty();
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}
