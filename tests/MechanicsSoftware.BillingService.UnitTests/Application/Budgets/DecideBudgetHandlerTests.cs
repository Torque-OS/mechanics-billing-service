using FluentAssertions;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using MechanicsSoftware.BillingService.UnitTests.Helpers;
using Moq;

namespace MechanicsSoftware.BillingService.UnitTests.Application.Budgets;

public class DecideBudgetHandlerTests
{
    private readonly Mock<IBudgetEventPublisher> _publisher = new();

    private static async Task<Budget> SeedBudgetAsync(BillingDbContext db)
    {
        var budget = BudgetFixtures.CreateBudget();
        db.Budgets.Add(budget);
        await db.SaveChangesAsync();
        return budget;
    }

    [Fact]
    public async Task ExecuteAsync_Approve_ApprovesBudgetAndPublishesBudgetApproved()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedBudgetAsync(db);

        var result = await new DecideBudgetHandler(db, _publisher.Object)
            .ExecuteAsync(new DecideBudgetCommand(budget.Id, BudgetDecision.Approve));

        result.Status.Should().Be("APPROVED");
        result.DecidedAt.Should().NotBeNull();
        budget.Status.Value.Should().Be(BudgetStatus.Status.Approved);
        _publisher.Verify(p => p.PublishBudgetApprovedAsync(budget, It.IsAny<CancellationToken>()), Times.Once);
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_Reject_RejectsBudgetAndPublishesBudgetRejected()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedBudgetAsync(db);

        var result = await new DecideBudgetHandler(db, _publisher.Object)
            .ExecuteAsync(new DecideBudgetCommand(budget.Id, BudgetDecision.Reject));

        result.Status.Should().Be("REJECTED");
        _publisher.Verify(p => p.PublishBudgetRejectedAsync(budget, It.IsAny<CancellationToken>()), Times.Once);
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownBudget_ThrowsNotFoundException()
    {
        await using var db = InMemoryDbContextHelper.Create();

        var act = () => new DecideBudgetHandler(db, _publisher.Object)
            .ExecuteAsync(new DecideBudgetCommand(Guid.NewGuid(), BudgetDecision.Approve));

        await act.Should().ThrowAsync<NotFoundException>();
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyDecidedBudget_ThrowsConflictWithoutPublishing()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedBudgetAsync(db);
        budget.Reject();
        await db.SaveChangesAsync();

        var act = () => new DecideBudgetHandler(db, _publisher.Object)
            .ExecuteAsync(new DecideBudgetCommand(budget.Id, BudgetDecision.Approve));

        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDecision_ThrowsDomainException()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedBudgetAsync(db);

        var act = () => new DecideBudgetHandler(db, _publisher.Object)
            .ExecuteAsync(new DecideBudgetCommand(budget.Id, (BudgetDecision)7));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*'7' is invalid*");
        budget.Status.Value.Should().Be(BudgetStatus.Status.Pending);
        _publisher.VerifyNoOtherCalls();
    }
}
