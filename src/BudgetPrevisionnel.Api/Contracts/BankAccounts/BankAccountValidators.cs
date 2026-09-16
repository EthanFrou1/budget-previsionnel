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

// Rows echo back what Preview returned (or an edited CategoryId) - the frontend never
// lets a row through with a blank label, but this is client input crossing the API
// boundary regardless, so it's still checked here.
public sealed class ImportCommitRowRequestValidator : AbstractValidator<ImportCommitRowRequest>
{
    public ImportCommitRowRequestValidator()
    {
        RuleFor(x => x.RawLabel).NotEmpty();
    }
}

public sealed class ImportCommitRequestValidator : AbstractValidator<ImportCommitRequest>
{
    public ImportCommitRequestValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.Rows).NotNull();
        RuleForEach(x => x.Rows).SetValidator(new ImportCommitRowRequestValidator());

        // Checked here, not left to throw FormatException at Convert.FromBase64String in
        // the controller - client input crossing the API boundary, same reasoning as the
        // row validator above.
        RuleFor(x => x.FileContentBase64)
            .Must(BeValidBase64)
            .When(x => x.FileContentBase64 is not null)
            .WithMessage("FileContentBase64 must be valid base64.");
    }

    private static bool BeValidBase64(string? value)
    {
        Span<byte> buffer = new byte[(value!.Length / 4 + 1) * 3];
        return Convert.TryFromBase64String(value, buffer, out _);
    }
}
