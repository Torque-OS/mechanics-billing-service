using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Infrastructure.Messaging;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TorqueOS.Contracts.Billing;

namespace MechanicsSoftware.BillingService.UnitTests.Infrastructure;

public sealed class RequestBudgetConsumerTests : IAsyncLifetime
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public Task InitializeAsync()
    {
        _provider = new ServiceCollection()
            .AddDbContext<BillingDbContext>(options => options.UseInMemoryDatabase(_databaseName))
            .AddScoped<IBillingDbContext>(sp => sp.GetRequiredService<BillingDbContext>())
            .AddScoped<IBudgetEventPublisher, MassTransitBudgetEventPublisher>()
            .AddScoped<GenerateBudgetHandler>()
            .AddMassTransitTestHarness(bus => bus.AddConsumer<RequestBudgetConsumer>())
            .BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    private static RequestBudget Request(Guid serviceOrderId, params RequestBudgetItem[] items) =>
        new(serviceOrderId, Guid.NewGuid(), items);

    private static RequestBudgetItem Service(int unitPriceInCents = 15000, int quantity = 1) =>
        new("Service", Guid.NewGuid(), "Oil change", unitPriceInCents, quantity);

    private static RequestBudgetItem Part(int unitPriceInCents = 4500, int quantity = 2) =>
        new("part", Guid.NewGuid(), "Oil filter", unitPriceInCents, quantity);

    private async Task<int> BudgetCountAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Budgets.CountAsync();
    }

    [Fact]
    public async Task RequestBudget_ValidSnapshot_CreatesTheBudgetAndPublishesBudgetGenerated()
    {
        var serviceOrderId = Guid.NewGuid();
        await _harness.Start();

        await _harness.Bus.Publish(Request(serviceOrderId, Service(), Part()));

        (await _harness.Published.Any<BudgetGenerated>()).Should().BeTrue();
        var generated = _harness.Published.Select<BudgetGenerated>().Single().Context;
        generated.Message.ServiceOrderId.Should().Be(serviceOrderId);
        generated.Message.TotalInCents.Should().Be(15000 + 4500 * 2);
        generated.CorrelationId.Should().Be(serviceOrderId);

        await using var scope = _provider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Budgets
            .Include(b => b.Items).SingleAsync();
        stored.ServiceOrderId.Should().Be(serviceOrderId);
        stored.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task RequestBudget_WithoutServiceItem_PublishesBudgetFailed()
    {
        var serviceOrderId = Guid.NewGuid();
        await _harness.Start();

        await _harness.Bus.Publish(Request(serviceOrderId, Part()));

        (await _harness.Published.Any<BudgetFailed>()).Should().BeTrue();
        _harness.Published.Select<BudgetFailed>().Single().Context.Message.ServiceOrderId.Should().Be(serviceOrderId);
        (await _harness.Published.Any<BudgetGenerated>()).Should().BeFalse();
        (await BudgetCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RequestBudget_UnknownItemType_PublishesBudgetFailed()
    {
        await _harness.Start();

        await _harness.Bus.Publish(Request(Guid.NewGuid(), Service(), new RequestBudgetItem("Labor", Guid.NewGuid(), "Extra", 1000, 1)));

        (await _harness.Published.Any<BudgetFailed>()).Should().BeTrue();
        (await BudgetCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RequestBudget_RepeatedForTheSameServiceOrder_KeepsOneBudgetAndPublishesOnce()
    {
        var serviceOrderId = Guid.NewGuid();
        await _harness.Start();

        await _harness.Bus.Publish(Request(serviceOrderId, Service()));
        (await _harness.Published.Any<BudgetGenerated>()).Should().BeTrue();
        await _harness.Bus.Publish(Request(serviceOrderId, Service()));

        (await _harness.Consumed.SelectAsync<RequestBudget>().Take(2).Count()).Should().Be(2);
        _harness.Published.Select<BudgetGenerated>().Should().ContainSingle();
        (await BudgetCountAsync()).Should().Be(1);
    }
}
