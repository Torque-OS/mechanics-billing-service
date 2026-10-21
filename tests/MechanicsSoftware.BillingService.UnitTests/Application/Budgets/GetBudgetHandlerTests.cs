using FluentAssertions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.UnitTests.Helpers;

namespace MechanicsSoftware.BillingService.UnitTests.Application.Budgets;

public class GetBudgetHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ExistingBudget_ReturnsBudgetWithItems()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = BudgetFixtures.CreateBudget();
        db.Budgets.Add(budget);
        await db.SaveChangesAsync();

        var result = await new GetBudgetHandler(db).ExecuteAsync(budget.Id);

        result.Id.Should().Be(budget.Id);
        result.ServiceOrderId.Should().Be(budget.ServiceOrderId);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownBudget_ThrowsNotFoundException()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var id = Guid.NewGuid();

        var act = () => new GetBudgetHandler(db).ExecuteAsync(id);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{id}*");
    }
}
