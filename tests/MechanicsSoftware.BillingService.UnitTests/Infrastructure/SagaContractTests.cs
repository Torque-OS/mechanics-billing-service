using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Infrastructure.Messaging;
using MechanicsSoftware.BillingService.UnitTests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using TorqueOS.Contracts.Billing;

namespace MechanicsSoftware.BillingService.UnitTests.Infrastructure;

/// <summary>
/// Publishes through MassTransit's in-memory test harness. These tests pin the cross-service contract:
/// the OS Service (F4-13) consumes these exact message identities and fields.
/// </summary>
public sealed class SagaContractTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection().AddMassTransitTestHarness().BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Theory]
    [InlineData(typeof(BudgetGenerated), "urn:message:TorqueOS.Contracts.Billing:BudgetGenerated")]
    [InlineData(typeof(BudgetFailed), "urn:message:TorqueOS.Contracts.Billing:BudgetFailed")]
    [InlineData(typeof(BudgetApproved), "urn:message:TorqueOS.Contracts.Billing:BudgetApproved")]
    [InlineData(typeof(BudgetRejected), "urn:message:TorqueOS.Contracts.Billing:BudgetRejected")]
    [InlineData(typeof(PaymentConfirmed), "urn:message:TorqueOS.Contracts.Billing:PaymentConfirmed")]
    [InlineData(typeof(PaymentFailed), "urn:message:TorqueOS.Contracts.Billing:PaymentFailed")]
    [InlineData(typeof(BudgetCancelled), "urn:message:TorqueOS.Contracts.Billing:BudgetCancelled")]
    [InlineData(typeof(CancelBudget), "urn:message:TorqueOS.Contracts.Billing:CancelBudget")]
    public void MessageIdentity_IsStable(Type contract, string expectedUrn)
    {
        MessageUrn.ForType(contract).ToString().Should().Be(expectedUrn);
    }

    [Fact]
    public async Task PaymentConfirmed_CarriesContractFieldsAndServiceOrderCorrelation()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());
        payment.AttachProviderOrder("ORD1", "qr");
        payment.Confirm();

        await new MassTransitPaymentEventPublisher(_harness.Bus).PublishPaymentConfirmedAsync(payment);

        var published = _harness.Published.Select<PaymentConfirmed>().Single().Context;
        published.Message.Should().Be(new PaymentConfirmed(
            payment.ServiceOrderId, payment.Id, payment.BudgetId, payment.Amount.Cents, payment.ConfirmedAt!.Value));
        published.CorrelationId.Should().Be(payment.ServiceOrderId);
        published.MessageId.Should().NotBeNull();
    }

    [Fact]
    public async Task PaymentFailed_CarriesReason()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());
        payment.Fail("Mercado Pago order expired");

        await new MassTransitPaymentEventPublisher(_harness.Bus).PublishPaymentFailedAsync(payment);

        var message = _harness.Published.Select<PaymentFailed>().Single().Context.Message;
        message.Reason.Should().Be("Mercado Pago order expired");
        message.ServiceOrderId.Should().Be(payment.ServiceOrderId);
        message.OccurredAt.Should().Be(payment.FailedAt!.Value);
    }

    [Fact]
    public async Task BudgetEvents_CarryContractFields()
    {
        var budget = BudgetFixtures.CreateBudget();
        var publisher = new MassTransitBudgetEventPublisher(_harness.Bus);

        await publisher.PublishBudgetGeneratedAsync(budget);
        budget.Approve();
        await publisher.PublishBudgetApprovedAsync(budget);
        await publisher.PublishBudgetFailedAsync(budget.ServiceOrderId, "A budget requires at least one service item.");

        var generated = _harness.Published.Select<BudgetGenerated>().Single().Context;
        generated.Message.ServiceOrderId.Should().Be(budget.ServiceOrderId);
        generated.Message.BudgetId.Should().Be(budget.Id);
        generated.Message.TotalInCents.Should().Be(budget.Total.Cents);
        generated.CorrelationId.Should().Be(budget.ServiceOrderId);

        var approved = _harness.Published.Select<BudgetApproved>().Single().Context.Message;
        approved.OccurredAt.Should().Be(budget.DecidedAt!.Value);

        _harness.Published.Select<BudgetFailed>().Single().Context.Message.Reason.Should().Contain("service item");
    }

    [Fact]
    public async Task BudgetRejected_IsPublished()
    {
        var budget = BudgetFixtures.CreateBudget();
        budget.Reject();

        await new MassTransitBudgetEventPublisher(_harness.Bus).PublishBudgetRejectedAsync(budget);

        _harness.Published.Select<BudgetRejected>().Single().Context.Message.BudgetId.Should().Be(budget.Id);
    }

    [Fact]
    public async Task BudgetCancelled_CarriesContractFieldsAndServiceOrderCorrelation()
    {
        var budget = BudgetFixtures.CreateBudget();
        budget.Cancel(payment: null);

        await new MassTransitBudgetEventPublisher(_harness.Bus).PublishBudgetCancelledAsync(budget);

        var published = _harness.Published.Select<BudgetCancelled>().Single().Context;
        published.Message.Should().Be(new BudgetCancelled(budget.ServiceOrderId, budget.Id, budget.CancelledAt!.Value));
        published.CorrelationId.Should().Be(budget.ServiceOrderId);
    }
}
