using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Infrastructure.Messaging;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using MechanicsSoftware.BillingService.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TorqueOS.Contracts.Billing;

namespace MechanicsSoftware.BillingService.UnitTests.Infrastructure;

/// <summary>
/// The Saga path end to end in memory: <c>CancelBudget</c> in, <c>BudgetCancelled</c> out through the real publisher.
/// Retry, inbox and outbox (<see cref="CancelBudgetConsumerDefinition"/>) need PostgreSQL and are checked against the
/// compose stack.
/// <para>
/// The harness is started only right before publishing: its <c>Any&lt;T&gt;()</c> stops waiting once the bus has been
/// idle for the inactivity timeout (about a second), and seeding the database first can take that long on a cold CI
/// runner — the wait would then end before the consumer ran.
/// </para>
/// </summary>
public sealed class CancelBudgetConsumerTests : IAsyncLifetime
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public Task InitializeAsync()
    {
        _provider = new ServiceCollection()
            .AddDbContext<BillingDbContext>(options => options.UseInMemoryDatabase(_databaseName))
            .AddScoped<IBillingDbContext>(sp => sp.GetRequiredService<BillingDbContext>())
            .AddSingleton<IMercadoPagoClient>(new FakeMercadoPagoClient())
            .AddSingleton<IPaymentHistory>(new InMemoryPaymentHistory())
            .AddScoped<IBudgetEventPublisher, MassTransitBudgetEventPublisher>()
            .AddScoped<CancelBudgetHandler>()
            .AddMassTransitTestHarness(bus => bus.AddConsumer<CancelBudgetConsumer>())
            .BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    private async Task<Budget> SeedBudgetAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var budget = BudgetFixtures.CreateApprovedBudget();
        db.Budgets.Add(budget);
        await db.SaveChangesAsync();
        return budget;
    }

    [Fact]
    public async Task CancelBudget_CancelsTheBudgetAndAnswersBudgetCancelledCorrelatedToTheOrder()
    {
        var budget = await SeedBudgetAsync();
        await _harness.Start();

        await _harness.Bus.Publish(new CancelBudget(budget.ServiceOrderId));

        (await _harness.Consumed.Any<CancelBudget>()).Should().BeTrue();
        (await _harness.Published.Any<BudgetCancelled>()).Should().BeTrue();
        var answer = _harness.Published.Select<BudgetCancelled>().Single().Context;
        answer.Message.ServiceOrderId.Should().Be(budget.ServiceOrderId);
        answer.Message.BudgetId.Should().Be(budget.Id);
        answer.CorrelationId.Should().Be(budget.ServiceOrderId);

        await using var scope = _provider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Budgets.SingleAsync();
        stored.Status.ToString().Should().Be("CANCELLED");
    }

    [Fact]
    public async Task CancelBudget_UnknownServiceOrder_FaultsWithoutAnswering()
    {
        await _harness.Start();

        await _harness.Bus.Publish(new CancelBudget(Guid.NewGuid()));

        (await _harness.Published.Any<Fault<CancelBudget>>()).Should().BeTrue();
        (await _harness.Published.Any<BudgetCancelled>()).Should().BeFalse();
    }
}
