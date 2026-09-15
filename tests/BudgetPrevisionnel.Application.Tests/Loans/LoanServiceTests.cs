using BudgetPrevisionnel.Application.Loans;

namespace BudgetPrevisionnel.Application.Tests.Loans;

public class LoanServiceTests
{
    private static LoanService CreateService() => new(new FakeLoanRepository());

    [Fact]
    public async Task CreateAsync_ValidLoan_Persists()
    {
        var service = CreateService();

        var loan = await service.CreateAsync(
            1, "Prêt auto", principalAmount: 15000m, remainingAmount: 12000m,
            interestRate: 3.5m, monthlyPayment: 300m, endDate: new DateOnly(2029, 1, 1));

        Assert.Equal("Prêt auto", loan.Label);
    }

    [Fact]
    public async Task CreateAsync_RemainingExceedsPrincipal_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidLoanException>(() => service.CreateAsync(
            1, "Prêt auto", principalAmount: 10000m, remainingAmount: 15000m,
            interestRate: 3.5m, monthlyPayment: 300m, endDate: new DateOnly(2029, 1, 1)));
    }

    [Fact]
    public async Task CreateAsync_RemainingEqualsPrincipal_IsAllowed()
    {
        // A brand-new, untouched loan - remaining == principal is the normal starting state.
        var service = CreateService();

        var loan = await service.CreateAsync(
            1, "Prêt auto", principalAmount: 10000m, remainingAmount: 10000m,
            interestRate: 3.5m, monthlyPayment: 300m, endDate: new DateOnly(2029, 1, 1));

        Assert.Equal(loan.PrincipalAmount, loan.RemainingAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task CreateAsync_PrincipalNotPositive_Throws(decimal principalAmount)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidLoanException>(() => service.CreateAsync(
            1, "Prêt auto", principalAmount, remainingAmount: 0m,
            interestRate: 3.5m, monthlyPayment: 300m, endDate: new DateOnly(2029, 1, 1)));
    }

    [Fact]
    public async Task CreateAsync_NegativeRemainingAmount_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidLoanException>(() => service.CreateAsync(
            1, "Prêt auto", principalAmount: 10000m, remainingAmount: -1m,
            interestRate: 3.5m, monthlyPayment: 300m, endDate: new DateOnly(2029, 1, 1)));
    }

    [Fact]
    public async Task CreateAsync_NegativeInterestRate_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidLoanException>(() => service.CreateAsync(
            1, "Prêt auto", principalAmount: 10000m, remainingAmount: 5000m,
            interestRate: -0.1m, monthlyPayment: 300m, endDate: new DateOnly(2029, 1, 1)));
    }

    [Fact]
    public async Task CreateAsync_ZeroInterestRate_IsAllowed()
    {
        var service = CreateService();

        var loan = await service.CreateAsync(
            1, "Prêt 0%", principalAmount: 10000m, remainingAmount: 5000m,
            interestRate: 0m, monthlyPayment: 300m, endDate: new DateOnly(2029, 1, 1));

        Assert.Equal(0m, loan.InterestRate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task CreateAsync_MonthlyPaymentNotPositive_Throws(decimal monthlyPayment)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidLoanException>(() => service.CreateAsync(
            1, "Prêt auto", principalAmount: 10000m, remainingAmount: 5000m,
            interestRate: 3.5m, monthlyPayment, endDate: new DateOnly(2029, 1, 1)));
    }

    [Fact]
    public async Task UpdateAsync_ValidChange_Persists()
    {
        var service = CreateService();
        var loan = await service.CreateAsync(
            1, "Prêt auto", 10000m, 10000m, 3.5m, 300m, new DateOnly(2029, 1, 1));

        var updated = await service.UpdateAsync(
            1, loan.Id, "Prêt auto", 10000m, remainingAmount: 9700m, 3.5m, 300m, new DateOnly(2029, 1, 1));

        Assert.Equal(9700m, updated.RemainingAmount);
    }

    [Fact]
    public async Task UpdateAsync_MakesRemainingExceedPrincipal_Throws()
    {
        var service = CreateService();
        var loan = await service.CreateAsync(
            1, "Prêt auto", 10000m, 5000m, 3.5m, 300m, new DateOnly(2029, 1, 1));

        await Assert.ThrowsAsync<InvalidLoanException>(() => service.UpdateAsync(
            1, loan.Id, "Prêt auto", principalAmount: 8000m, remainingAmount: 9000m, 3.5m, 300m, new DateOnly(2029, 1, 1)));
    }

    [Fact]
    public async Task UpdateAsync_AnotherUsersLoan_ThrowsLoanNotFound()
    {
        var service = CreateService();
        var loan = await service.CreateAsync(2, "Pas à moi", 10000m, 5000m, 3.5m, 300m, new DateOnly(2029, 1, 1));

        await Assert.ThrowsAsync<LoanNotFoundException>(() => service.UpdateAsync(
            1, loan.Id, "Modifié", 10000m, 5000m, 3.5m, 300m, new DateOnly(2029, 1, 1)));
    }

    [Fact]
    public async Task DeleteAsync_OwnLoan_Removes()
    {
        var service = CreateService();
        var loan = await service.CreateAsync(1, "Prêt auto", 10000m, 5000m, 3.5m, 300m, new DateOnly(2029, 1, 1));

        await service.DeleteAsync(1, loan.Id);

        Assert.Empty(await service.GetAllForUserAsync(1));
    }
}
