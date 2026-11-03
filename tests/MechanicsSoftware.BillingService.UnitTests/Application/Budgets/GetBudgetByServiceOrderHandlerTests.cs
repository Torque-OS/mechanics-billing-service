using FluentAssertions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Queries;
using MechanicsSoftware.BillingService.UnitTests.Helpers;

namespace MechanicsSoftware.BillingService.UnitTests.Application.Budgets;

public class GetBudgetByServiceOrderHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ServiceOrderWithBudget_ReturnsBudget()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = BudgetFixtures.CreateBudget();
        db.Budgets.AddRange(budget, BudgetFixtures.CreateBudget());
        await db.SaveChangesAsync();

        var result = await new GetBudgetByServiceOrderHandler(db)
            .ExecuteAsync(new GetBudgetByServiceOrderQuery(budget.ServiceOrderId));

        result.Id.Should().Be(budget.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceOrderWithoutBudget_ThrowsNotFoundException()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var serviceOrderId = Guid.NewGuid();

        var act = () => new GetBudgetByServiceOrderHandler(db)
            .ExecuteAsync(new GetBudgetByServiceOrderQuery(serviceOrderId));

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*service order*{serviceOrderId}*");
    }
}
