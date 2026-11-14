using FluentAssertions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.UnitTests.Helpers;

namespace MechanicsSoftware.BillingService.UnitTests.Application.Payments;

public class GetPaymentByBudgetHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_BudgetWithPayment_ReturnsThePayment()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = BudgetFixtures.CreateApprovedBudget();
        var payment = Payment.CreateFor(budget);
        payment.AttachProviderOrder("ORD1", "qr-data");
        db.Budgets.Add(budget);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var result = await new GetPaymentByBudgetHandler(db).ExecuteAsync(budget.Id);

        result.Id.Should().Be(payment.Id);
        result.BudgetId.Should().Be(budget.Id);
        result.Status.Should().Be("PENDING");
        result.QrData.Should().Be("qr-data");
    }

    [Fact]
    public async Task ExecuteAsync_BudgetWithoutPayment_ThrowsNotFound()
    {
        await using var db = InMemoryDbContextHelper.Create();

        var act = () => new GetPaymentByBudgetHandler(db).ExecuteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
