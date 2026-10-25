using FluentAssertions;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MechanicsSoftware.BillingService.UnitTests.Application.Budgets;

public class GenerateBudgetHandlerTests
{
    private readonly Mock<IBudgetEventPublisher> _publisher = new();

    [Fact]
    public async Task ExecuteAsync_ValidCommand_PersistsBudgetAndReturnsResponse()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var serviceOrderId = Guid.NewGuid();
        var command = new GenerateBudgetCommand(serviceOrderId, Guid.NewGuid(), [
            BudgetFixtures.ServiceItem(unitPriceInCents: 15000, quantity: 1),
            BudgetFixtures.PartItem(unitPriceInCents: 4500, quantity: 2)
        ]);

        var result = (await new GenerateBudgetHandler(db, _publisher.Object).ExecuteAsync(command)).Budget;

        result.Should().NotBeNull();
        result!.ServiceOrderId.Should().Be(serviceOrderId);
        result.Status.Should().Be("PENDING");
        result.TotalInCents.Should().Be(24000);
        result.TotalFormatted.Should().Be("R$ 240,00");
        result.Items.Should().HaveCount(2);
        result.Items.Should().ContainSingle(i => i.Type == "Part" && i.TotalInCents == 9000);

        var stored = await db.Budgets.Include(b => b.Items).SingleAsync();
        stored.Id.Should().Be(result.Id);
        stored.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsCreatedWithCustomerId()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var customerId = Guid.NewGuid();
        var command = new GenerateBudgetCommand(Guid.NewGuid(), customerId, [BudgetFixtures.ServiceItem()]);

        var result = await new GenerateBudgetHandler(db, _publisher.Object).ExecuteAsync(command);

        result.Created.Should().BeTrue();
        result.FailureReason.Should().BeNull();
        result.Budget!.CustomerId.Should().Be(customerId);
        (await db.Budgets.SingleAsync()).CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_PublishesBudgetGenerated()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var command = new GenerateBudgetCommand(Guid.NewGuid(), Guid.NewGuid(), [BudgetFixtures.ServiceItem()]);

        var result = await new GenerateBudgetHandler(db, _publisher.Object).ExecuteAsync(command);

        _publisher.Verify(p => p.PublishBudgetGeneratedAsync(
            It.Is<Budget>(b => b.Id == result.Budget!.Id), It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishBudgetFailedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetAlreadyExistsForServiceOrder_ReturnsExistingWithoutPublishing()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var existing = BudgetFixtures.CreateBudget();
        db.Budgets.Add(existing);
        await db.SaveChangesAsync();
        var command = new GenerateBudgetCommand(existing.ServiceOrderId, Guid.NewGuid(), [BudgetFixtures.ServiceItem(unitPriceInCents: 1)]);

        var result = await new GenerateBudgetHandler(db, _publisher.Object).ExecuteAsync(command);

        result.Created.Should().BeFalse();
        result.Budget!.Id.Should().Be(existing.Id);
        result.Budget.TotalInCents.Should().Be(existing.Total.Cents);
        (await db.Budgets.CountAsync()).Should().Be(1);
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_NoServiceItem_PublishesBudgetFailedAndReturnsRejected()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var serviceOrderId = Guid.NewGuid();
        var command = new GenerateBudgetCommand(serviceOrderId, Guid.NewGuid(), [BudgetFixtures.PartItem()]);

        var result = await new GenerateBudgetHandler(db, _publisher.Object).ExecuteAsync(command);

        result.Failed.Should().BeTrue();
        result.FailureReason.Should().Contain("at least one service item");
        (await db.Budgets.AnyAsync()).Should().BeFalse();
        _publisher.Verify(p => p.PublishBudgetFailedAsync(
            serviceOrderId,
            It.Is<string>(reason => reason.Contains("at least one service item")),
            It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishBudgetGeneratedAsync(
            It.IsAny<Budget>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeUnitPrice_PublishesBudgetFailed()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var serviceOrderId = Guid.NewGuid();
        var command = new GenerateBudgetCommand(serviceOrderId, Guid.NewGuid(), [BudgetFixtures.ServiceItem(unitPriceInCents: -10)]);

        var result = await new GenerateBudgetHandler(db, _publisher.Object).ExecuteAsync(command);

        result.Failed.Should().BeTrue();
        _publisher.Verify(p => p.PublishBudgetFailedAsync(
            serviceOrderId,
            It.Is<string>(reason => reason.Contains("negative")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NullItems_PublishesBudgetFailed()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var serviceOrderId = Guid.NewGuid();
        var command = new GenerateBudgetCommand(serviceOrderId, Guid.NewGuid(), null!);

        var result = await new GenerateBudgetHandler(db, _publisher.Object).ExecuteAsync(command);

        result.Failed.Should().BeTrue();
        _publisher.Verify(p => p.PublishBudgetFailedAsync(
            serviceOrderId,
            It.Is<string>(reason => reason.Contains("at least one item")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
