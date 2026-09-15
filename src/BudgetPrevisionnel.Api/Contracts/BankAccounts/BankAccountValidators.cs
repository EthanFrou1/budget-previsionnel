using FluentValidation;

namespace BudgetPrevisionnel.Api.Contracts.BankAccounts;

public sealed class CreateBankAccountRequestValidator : AbstractValidator<CreateBankAccountRequest>
{
    public CreateBankAccountRequestValidator()
    {
        RuleFor(x => x.BankName).NotEmpty();
        RuleFor(x => x.Label).NotEmpty();
    }
}

public sealed class UpdateBankAccountRequestValidator : AbstractValidator<UpdateBankAccountRequest>
{
    public UpdateBankAccountRequestValidator()
    {
        RuleFor(x => x.BankName).NotEmpty();
        RuleFor(x => x.Label).NotEmpty();
    }
}
