using FluentAssertions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using MechanicsSoftware.BillingService.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.UnitTests.Application.Budgets;

/// <summary>
/// Business rules (user, 2026-10-29): no payment → only the budget is cancelled; open QR → cancelled on Mercado Pago,
/// no refund; received payment → the cancellation refunds it. A completed order never gets here (the OS refuses it).
/// </summary>
public class CancelBudgetHandlerTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly FakeMercadoPagoClient _mercadoPago = new();
    private readonly RecordingBudgetEventPublisher _publisher = new();
    private readonly InMemoryPaymentHistory _history = new();
    private readonly SaveChangesCounter _saves = new();

    private BillingDbContext Db() => InMemoryDbContextHelper.Create(_databaseName, _saves);

    private CancelBudgetHandler Handler(BillingDbContext db) => new(db, _mercadoPago, _publisher, _history);

    private async Task<Budget> SeedBudgetAsync(bool approve = true)
    {
        await using var db = Db();
        var budget = approve ? BudgetFixtures.CreateApprovedBudget() : BudgetFixtures.CreateBudget();
        db.Budgets.Add(budget);
        await db.SaveChangesAsync();
        return budget;
    }

    /// <summary>Approved budget with a QR payment; <paramref name="orderStatus"/> is what Mercado Pago reports now.</summary>
    private async Task<(Budget Budget, Payment Payment)> SeedPaymentAsync(string orderStatus = "created", bool confirmed = false)
    {
        var budget = await SeedBudgetAsync();
        await using var db = Db();
        var created = await new CreatePaymentHandler(db, _mercadoPago, _history)
            .ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetFixtures.StaffRequester));
        var payment = await db.Payments.SingleAsync(p => p.Id == created.Payment.Id);
        if (confirmed)
        {
            payment.Confirm();
            await db.SaveChangesAsync();
        }

        _mercadoPago.SetStatus(payment.ProviderOrderId!, orderStatus);
        _saves.Reset();
        return (budget, payment);
    }

    private async Task<(string Budget, string? Payment)> CommittedStatusAsync(Guid budgetId)
    {
        await using var db = InMemoryDbContextHelper.Create(_databaseName);
        var budget = await db.Budgets.SingleAsync(b => b.Id == budgetId);
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.BudgetId == budgetId);
        return (budget.Status.ToString(), payment?.Status.ToString());
    }

    private string[] Published(Budget budget) => _publisher.For(budget.ServiceOrderId).Select(e => e.Name).ToArray();

    // --- One effect per payment state ------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutPayment_CancelsBudgetOnly(bool approved)
    {
        var budget = await SeedBudgetAsync(approved);
        await using var db = Db();

        var result = await Handler(db).ExecuteAsync(budget.Id);

        result.Status.Should().Be("CANCELLED");
        result.CancelledAt.Should().NotBeNull();
        (await CommittedStatusAsync(budget.Id)).Should().Be(("CANCELLED", (string?)null));
        Published(budget).Should().Equal("BudgetCancelled");
        _mercadoPago.GetCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("created")]
    [InlineData("action_required")]
    public async Task OpenQr_IsCancelledOnMercadoPago_WithoutRefund(string orderStatus)
    {
        var (budget, payment) = await SeedPaymentAsync(orderStatus);
        await using var db = Db();

        await Handler(db).ExecuteAsync(budget.Id);

        (await CommittedStatusAsync(budget.Id)).Should().Be(("CANCELLED", "CANCELLED"));
        _mercadoPago.CancelCalls.Should().Equal((payment.ProviderOrderId!, $"cancel-{payment.Id}"));
        _mercadoPago.RefundCalls.Should().BeEmpty();
        _mercadoPago.StatusOf(payment.ProviderOrderId!).Should().Be("canceled");
        Published(budget).Should().Equal("BudgetCancelled");
        var entry = _history.For(payment.Id)[^1];
        entry.Event.Should().Be("cancelled");
        entry.Status.Should().Be("CANCELLED");
        entry.ProviderStatus.Should().Be("canceled");
    }

    [Fact]
    public async Task ReceivedPayment_IsRefundedByTheCancellation()
    {
        var (budget, payment) = await SeedPaymentAsync("processed", confirmed: true);
        await using var db = Db();

        await Handler(db).ExecuteAsync(budget.Id);

        (await CommittedStatusAsync(budget.Id)).Should().Be(("CANCELLED", "REFUNDED"));
        _mercadoPago.RefundCalls.Should().Equal((payment.ProviderOrderId!, $"refund-{payment.Id}"));
        _mercadoPago.CancelCalls.Should().BeEmpty();
        Published(budget).Should().Equal("BudgetCancelled");
        var entry = _history.For(payment.Id)[^1];
        entry.Event.Should().Be("refunded");
        entry.AmountInCents.Should().Be(payment.Amount.Cents);
        entry.ProviderStatus.Should().Be("refunded");
        _saves.Count.Should().Be(1, "both status changes and BudgetCancelled go in one transaction");
    }

    [Fact]
    public async Task PaymentFailed_CancelsBudgetWithoutCallingMercadoPago()
    {
        var (budget, payment) = await SeedPaymentAsync("expired");
        await using (var db = Db())
        {
            (await db.Payments.SingleAsync(p => p.Id == payment.Id)).Fail("Mercado Pago order expired");
            await db.SaveChangesAsync();
        }

        await using (var db = Db())
            await Handler(db).ExecuteAsync(budget.Id);

        (await CommittedStatusAsync(budget.Id)).Should().Be(("CANCELLED", "FAILED"));
        _mercadoPago.GetCalls.Should().Be(0);
        Published(budget).Should().Equal("BudgetCancelled");
    }

    // --- Mercado Pago moved since our last sync -------------------------------------------

    [Fact]
    public async Task PendingButPaidAMomentAgo_IsRefundedNotLeftBehind()
    {
        var (budget, payment) = await SeedPaymentAsync("processed");
        await using var db = Db();

        await Handler(db).ExecuteAsync(budget.Id);

        (await CommittedStatusAsync(budget.Id)).Should().Be(("CANCELLED", "REFUNDED"));
        _mercadoPago.RefundCalls.Should().ContainSingle();
        var stored = await db.Payments.SingleAsync(p => p.Id == payment.Id);
        stored.ConfirmedAt.Should().NotBeNull();
        stored.RefundedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlreadyRefundedOnMercadoPago_IsRecordedWithoutRefundingAgain(bool confirmed)
    {
        var (budget, _) = await SeedPaymentAsync("refunded", confirmed);
        await using var db = Db();

        await Handler(db).ExecuteAsync(budget.Id);

        (await CommittedStatusAsync(budget.Id)).Should().Be(("CANCELLED", "REFUNDED"));
        _mercadoPago.RefundCalls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("canceled")]
    [InlineData("failed")]
    public async Task PendingQrThatCanNoLongerBePaid_IsCancelledLocally(string orderStatus)
    {
        var (budget, _) = await SeedPaymentAsync(orderStatus);
        await using var db = Db();

        await Handler(db).ExecuteAsync(budget.Id);

        (await CommittedStatusAsync(budget.Id)).Should().Be(("CANCELLED", "CANCELLED"));
        _mercadoPago.CancelCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task UnexpectedProviderStatus_ThrowsProviderExceptionAndChangesNothing()
    {
        var (budget, _) = await SeedPaymentAsync("created", confirmed: true);
        await using var db = Db();

        var act = () => Handler(db).ExecuteAsync(budget.Id);

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*created*CONFIRMED*");
        (await CommittedStatusAsync(budget.Id)).Should().Be(("APPROVED", "CONFIRMED"));
        _publisher.For(budget.ServiceOrderId).Should().BeEmpty();
    }

    // --- Failures and repetition ----------------------------------------------------------

    [Fact]
    public async Task RefundFails_NothingChanges_HistoryRecordsIt_AndRepeatingRetriesWithTheSameKey()
    {
        var (budget, payment) = await SeedPaymentAsync("processed", confirmed: true);
        _mercadoPago.FailNextRefund = true;

        await using (var db = Db())
        {
            var act = () => Handler(db).ExecuteAsync(budget.Id);
            await act.Should().ThrowAsync<PaymentProviderException>();
        }

        (await CommittedStatusAsync(budget.Id)).Should().Be(("APPROVED", "CONFIRMED"));
        _publisher.For(budget.ServiceOrderId).Should().BeEmpty();
        _saves.Count.Should().Be(0);
        var failure = _history.For(payment.Id)[^1];
        failure.Event.Should().Be("refund_failed");
        failure.Status.Should().Be("CONFIRMED");
        failure.Reason.Should().Contain("refused the refund");

        await using (var db = Db())
            await Handler(db).ExecuteAsync(budget.Id);

        (await CommittedStatusAsync(budget.Id)).Should().Be(("CANCELLED", "REFUNDED"));
        _mercadoPago.RefundCalls.Should().Equal((payment.ProviderOrderId!, $"refund-{payment.Id}"));
        Published(budget).Should().Equal("BudgetCancelled");
    }

    [Fact]
    public async Task QrCancellationFails_NothingChanges_AndHistoryRecordsIt()
    {
        var (budget, payment) = await SeedPaymentAsync();
        _mercadoPago.FailNextCancel = true;
        await using var db = Db();

        var act = () => Handler(db).ExecuteAsync(budget.Id);

        await act.Should().ThrowAsync<PaymentProviderException>();
        (await CommittedStatusAsync(budget.Id)).Should().Be(("APPROVED", "PENDING"));
        _history.For(payment.Id)[^1].Event.Should().Be("cancel_failed");
        _publisher.For(budget.ServiceOrderId).Should().BeEmpty();
    }

    [Fact]
    public async Task Repeated_RepublishesBudgetCancelledWithoutCallingMercadoPago()
    {
        var (budget, _) = await SeedPaymentAsync("processed", confirmed: true);
        await using (var db = Db())
            await Handler(db).ExecuteAsync(budget.Id);
        var getCalls = _mercadoPago.GetCalls;

        await using (var db = Db())
            await Handler(db).ExecuteForServiceOrderAsync(budget.ServiceOrderId);

        Published(budget).Should().Equal("BudgetCancelled", "BudgetCancelled");
        _mercadoPago.GetCalls.Should().Be(getCalls);
        _mercadoPago.RefundCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task RejectedBudget_ThrowsConflictAndPublishesNothing()
    {
        var budget = await SeedBudgetAsync(approve: false);
        await using (var db = Db())
        {
            (await db.Budgets.SingleAsync(b => b.Id == budget.Id)).Reject();
            await db.SaveChangesAsync();
        }

        await using var context = Db();
        var act = () => Handler(context).ExecuteAsync(budget.Id);

        await act.Should().ThrowAsync<ConflictException>();
        _publisher.For(budget.ServiceOrderId).Should().BeEmpty();
    }

    [Fact]
    public async Task ByServiceOrder_FindsTheBudget()
    {
        var budget = await SeedBudgetAsync();
        await using var db = Db();

        var result = await Handler(db).ExecuteForServiceOrderAsync(budget.ServiceOrderId);

        result.Id.Should().Be(budget.Id);
        result.Status.Should().Be("CANCELLED");
    }

    [Fact]
    public async Task UnknownServiceOrderOrBudget_ThrowsNotFound()
    {
        await using var db = Db();

        await Handler(db).Invoking(h => h.ExecuteForServiceOrderAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
        await Handler(db).Invoking(h => h.ExecuteAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }
}
