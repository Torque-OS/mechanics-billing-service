using FluentAssertions;
using MechanicsSoftware.BillingService.Domain.Enums;
using MechanicsSoftware.BillingService.Domain.ValueObjects;
using MechanicsSoftware.BillingService.Infrastructure.Persistence.Configurations;
using MechanicsSoftware.BillingService.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.UnitTests.Infrastructure;

public class BudgetPersistenceTests
{
    [Fact]
    public async Task SaveAndReload_DecidedBudget_RoundTripsAllFields()
    {
        var dbName = Guid.NewGuid().ToString();
        var budget = BudgetFixtures.CreateBudget();
        budget.Approve();

        await using (var writeDb = InMemoryDbContextHelper.Create(dbName))
        {
            writeDb.Budgets.Add(budget);
            await writeDb.SaveChangesAsync();
        }

        await using var readDb = InMemoryDbContextHelper.Create(dbName);
        var stored = await readDb.Budgets.Include(b => b.Items).SingleAsync();

        stored.Id.Should().Be(budget.Id);
        stored.ServiceOrderId.Should().Be(budget.ServiceOrderId);
        stored.Status.Should().Be(new BudgetStatus(BudgetStatus.Status.Approved));
        stored.Total.Should().Be(budget.Total);
        stored.DecidedAt.Should().Be(budget.DecidedAt);
        stored.Items.Should().HaveCount(2);
        stored.Items.Should().ContainSingle(i => i.Type == BudgetItemType.Part && i.Quantity == 2);
    }

    [Theory]
    [InlineData("PENDING", BudgetStatus.Status.Pending)]
    [InlineData("APPROVED", BudgetStatus.Status.Approved)]
    [InlineData("REJECTED", BudgetStatus.Status.Rejected)]
    [InlineData("CANCELLED", BudgetStatus.Status.Cancelled)]
    public void ParseStatus_KnownValue_ReturnsStatus(string value, BudgetStatus.Status expected)
    {
        BudgetConfiguration.ParseStatus(value).Value.Should().Be(expected);
    }

    [Fact]
    public void ParseStatus_UnknownValue_ThrowsInvalidOperationException()
    {
        var act = () => BudgetConfiguration.ParseStatus("ARCHIVED");

        act.Should().Throw<InvalidOperationException>().WithMessage("*ARCHIVED*");
    }

    [Theory]
    [InlineData("CANCELLED", PaymentStatus.Status.Cancelled)]
    [InlineData("REFUNDED", PaymentStatus.Status.Refunded)]
    public void PaymentParseStatus_CompensationValues_ReturnStatus(string value, PaymentStatus.Status expected)
    {
        PaymentConfiguration.ParseStatus(value).Value.Should().Be(expected);
    }
}
