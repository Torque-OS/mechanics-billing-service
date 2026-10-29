using FluentAssertions;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Application.UseCases.Payments;
using MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using MechanicsSoftware.BillingService.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MechanicsSoftware.BillingService.UnitTests.Application.Payments;

public class PaymentHandlersTests
{
    private readonly FakeMercadoPagoClient _mercadoPago = new();
    private readonly Mock<IPaymentEventPublisher> _publisher = new();
    private readonly Mock<IWebhookSignatureValidator> _signature = new();
    private readonly Mock<INotificationStore> _store = new();
    private readonly InMemoryPaymentHistory _history = new();

    public PaymentHandlersTests() =>
        _signature.Setup(v => v.IsValid(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>())).Returns(true);

    private CreatePaymentHandler CreateHandler(BillingDbContext db) => new(db, _mercadoPago, _history);
    private PaymentSynchronizer Synchronizer(BillingDbContext db) => new(db, _mercadoPago, _publisher.Object, _history);
    private HandleMercadoPagoNotificationHandler NotificationHandler(BillingDbContext db) =>
        new(db, _signature.Object, _store.Object, Synchronizer(db));

    private static async Task<Budget> SeedApprovedBudgetAsync(BillingDbContext db, Guid? customerId = null)
    {
        var budget = BudgetFixtures.CreateApprovedBudget(customerId);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync();
        return budget;
    }

    private async Task<PaymentResponse> SeedPaymentAsync(BillingDbContext db)
    {
        var budget = await SeedApprovedBudgetAsync(db);
        return (await CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetFixtures.StaffRequester))).Payment;
    }

    private static MercadoPagoNotification Notification(string? dataId, string action = "order.processed") =>
        new("ts=1,v1=abc", "req-1", dataId, action, "{\"action\":\"" + action + "\"}", DateTime.UtcNow);

    // --- Create -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ApprovedBudget_CreatesQrOrderAndPersistsPendingPayment()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedApprovedBudgetAsync(db);

        var result = await CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetFixtures.StaffRequester));

        result.Created.Should().BeTrue();
        result.Payment.Status.Should().Be("PENDING");
        result.Payment.AmountInCents.Should().Be(budget.Total.Cents);
        result.Payment.QrData.Should().NotBeNullOrWhiteSpace();
        result.Payment.ProviderOrderId.Should().StartWith("ORD");
        var call = _mercadoPago.CreateCalls.Single();
        call.IdempotencyKey.Should().Be(result.Payment.Id.ToString());
        call.Request.ExternalReference.Should().Be(result.Payment.Id.ToString());
        call.Request.Amount.Should().Be(budget.Total);
        (await db.Payments.SingleAsync()).Id.Should().Be(result.Payment.Id);
    }

    [Fact]
    public async Task Create_SecondRequestForSameBudget_ReturnsExistingWithoutCallingProvider()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedApprovedBudgetAsync(db);
        var first = await CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetFixtures.StaffRequester));

        var second = await CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetFixtures.StaffRequester));

        second.Created.Should().BeFalse();
        second.Payment.Id.Should().Be(first.Payment.Id);
        _mercadoPago.CreateCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_CustomerPayingOwnBudget_Succeeds()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var customerId = Guid.NewGuid();
        var budget = await SeedApprovedBudgetAsync(db, customerId);

        var result = await CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetRequester.Customer(customerId)));

        result.Created.Should().BeTrue();
    }

    [Fact]
    public async Task Create_CustomerPayingAnotherCustomersBudget_ThrowsForbidden()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedApprovedBudgetAsync(db);

        var act = () => CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetRequester.Customer(Guid.NewGuid())));

        await act.Should().ThrowAsync<ForbiddenException>();
        _mercadoPago.CreateCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_BudgetNotApproved_ThrowsConflictWithoutCallingProvider()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = BudgetFixtures.CreateBudget();
        db.Budgets.Add(budget);
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetFixtures.StaffRequester));

        await act.Should().ThrowAsync<ConflictException>();
        _mercadoPago.CreateCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_UnknownBudget_ThrowsNotFound()
    {
        await using var db = InMemoryDbContextHelper.Create();

        var act = () => CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(Guid.NewGuid(), BudgetFixtures.StaffRequester));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_ProviderFails_PersistsNothing()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedApprovedBudgetAsync(db);
        _mercadoPago.FailNextCreate = true;

        var act = () => CreateHandler(db).ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetFixtures.StaffRequester));

        await act.Should().ThrowAsync<PaymentProviderException>();
        (await db.Payments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Create_ProviderReturnsNoQrData_ThrowsProviderException()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var budget = await SeedApprovedBudgetAsync(db);
        var provider = new Mock<IMercadoPagoClient>();
        provider.Setup(p => p.CreateQrOrderAsync(It.IsAny<MercadoPagoQrOrderRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoOrder("ORDX", "created", null, null));

        var act = () => new CreatePaymentHandler(db, provider.Object, _history)
            .ExecuteAsync(new CreatePaymentCommand(budget.Id, BudgetFixtures.StaffRequester));

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*qr_data*");
        (await db.Payments.AnyAsync()).Should().BeFalse();
    }

    // --- Get / Verify -------------------------------------------------------------------

    [Fact]
    public async Task Get_UnknownPayment_ThrowsNotFound()
    {
        await using var db = InMemoryDbContextHelper.Create();

        var act = () => new GetPaymentHandler(db).ExecuteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Verify_ProcessedOrder_ConfirmsAndPublishesOnce()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);
        _mercadoPago.SetStatus(created.ProviderOrderId!, "processed");
        var handler = new VerifyPaymentHandler(db, Synchronizer(db));

        var first = await handler.ExecuteAsync(created.Id);
        var second = await handler.ExecuteAsync(created.Id);

        first.Status.Should().Be("CONFIRMED");
        first.ConfirmedAt.Should().NotBeNull();
        second.Status.Should().Be("CONFIRMED");
        _publisher.Verify(p => p.PublishPaymentConfirmedAsync(It.Is<Payment>(x => x.Id == created.Id), It.IsAny<CancellationToken>()), Times.Once);
        _publisher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("canceled")]
    [InlineData("failed")]
    public async Task Verify_TerminalFailureStatus_FailsAndPublishesPaymentFailed(string status)
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);
        _mercadoPago.SetStatus(created.ProviderOrderId!, status);

        var result = await new VerifyPaymentHandler(db, Synchronizer(db)).ExecuteAsync(created.Id);

        result.Status.Should().Be("FAILED");
        result.FailureReason.Should().Contain(status);
        _publisher.Verify(p => p.PublishPaymentFailedAsync(It.Is<Payment>(x => x.Id == created.Id), It.IsAny<CancellationToken>()), Times.Once);
        _publisher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("created")]
    [InlineData("action_required")]
    public async Task Verify_StillOpenStatus_KeepsPendingWithoutPublishing(string status)
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);
        _mercadoPago.SetStatus(created.ProviderOrderId!, status);

        var result = await new VerifyPaymentHandler(db, Synchronizer(db)).ExecuteAsync(created.Id);

        result.Status.Should().Be("PENDING");
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sync_PaymentWithoutProviderOrder_ThrowsConflict()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());

        var act = () => Synchronizer(db).SyncAsync(payment);

        await act.Should().ThrowAsync<ConflictException>();
    }

    // --- Webhook ------------------------------------------------------------------------

    [Fact]
    public async Task Notification_InvalidSignature_ThrowsWithoutStoringOrSyncing()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);
        _signature.Setup(v => v.IsValid(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>())).Returns(false);

        var act = () => NotificationHandler(db).ExecuteAsync(Notification(created.ProviderOrderId));

        await act.Should().ThrowAsync<InvalidWebhookSignatureException>();
        _store.VerifyNoOtherCalls();
        _mercadoPago.GetCalls.Should().Be(0);
    }

    [Fact]
    public async Task Notification_ForOurOrder_StoresRawAndConfirmsFromProviderState()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);
        _mercadoPago.SetStatus(created.ProviderOrderId!, "processed");

        var outcome = await NotificationHandler(db).ExecuteAsync(Notification(created.ProviderOrderId!.ToLowerInvariant()));

        outcome.Should().Be(NotificationOutcome.Synchronized);
        _store.Verify(s => s.SaveAsync(It.IsAny<MercadoPagoNotification>(), It.IsAny<CancellationToken>()), Times.Once);
        (await db.Payments.SingleAsync()).Status.ToString().Should().Be("CONFIRMED");
    }

    [Fact]
    public async Task Notification_BodySaysProcessedButProviderSaysCreated_StaysPending()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);

        await NotificationHandler(db).ExecuteAsync(Notification(created.ProviderOrderId, "order.processed"));

        (await db.Payments.SingleAsync()).Status.ToString().Should().Be("PENDING");
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Notification_UnknownOrder_IsStoredAndIgnored()
    {
        await using var db = InMemoryDbContextHelper.Create();

        var outcome = await NotificationHandler(db).ExecuteAsync(Notification("ORDUNKNOWN"));

        outcome.Should().Be(NotificationOutcome.Ignored);
        _store.Verify(s => s.SaveAsync(It.IsAny<MercadoPagoNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Notification_WithoutDataId_IsStoredAndIgnored()
    {
        await using var db = InMemoryDbContextHelper.Create();

        var outcome = await NotificationHandler(db).ExecuteAsync(Notification(null));

        outcome.Should().Be(NotificationOutcome.Ignored);
    }

    // --- History (MongoDB payment_events) and outbox ordering ---------------------------

    [Fact]
    public async Task Create_RecordsCreatedHistoryWithProviderSnapshot()
    {
        await using var db = InMemoryDbContextHelper.Create();

        var created = await SeedPaymentAsync(db);

        var entry = _history.For(created.Id).Single();
        entry.Event.Should().Be("created");
        entry.Status.Should().Be("PENDING");
        entry.ProviderOrderId.Should().Be(created.ProviderOrderId);
        entry.ProviderStatus.Should().Be("created");
        entry.ProviderSnapshotJson.Should().Contain(created.ProviderOrderId!);
        entry.AmountInCents.Should().Be(created.AmountInCents);
    }

    [Fact]
    public async Task Verify_Processed_RecordsConfirmedHistoryOnce()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);
        _mercadoPago.SetStatus(created.ProviderOrderId!, "processed");
        var handler = new VerifyPaymentHandler(db, Synchronizer(db));

        await handler.ExecuteAsync(created.Id);
        await handler.ExecuteAsync(created.Id);

        _history.For(created.Id).Select(e => e.Event).Should().Equal("created", "confirmed");
        var confirmed = _history.For(created.Id)[1];
        confirmed.Status.Should().Be("CONFIRMED");
        confirmed.ProviderStatus.Should().Be("processed");
        confirmed.ProviderSnapshotJson.Should().Contain("processed");
    }

    [Fact]
    public async Task Verify_Expired_RecordsFailedHistoryWithReason()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);
        _mercadoPago.SetStatus(created.ProviderOrderId!, "expired");

        await new VerifyPaymentHandler(db, Synchronizer(db)).ExecuteAsync(created.Id);

        var failed = _history.For(created.Id)[^1];
        failed.Event.Should().Be("failed");
        failed.Reason.Should().Contain("expired");
    }

    [Fact]
    public async Task Verify_StillPending_RecordsNoNewHistory()
    {
        await using var db = InMemoryDbContextHelper.Create();
        var created = await SeedPaymentAsync(db);

        await new VerifyPaymentHandler(db, Synchronizer(db)).ExecuteAsync(created.Id);

        _history.For(created.Id).Select(e => e.Event).Should().Equal("created");
    }

    [Fact]
    public async Task Sync_PublishesBeforeCommit_SoTheOutboxSharesTheStatusTransaction()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = InMemoryDbContextHelper.Create(databaseName);
        var created = await SeedPaymentAsync(db);
        _mercadoPago.SetStatus(created.ProviderOrderId!, "processed");
        string? committedStatusWhenPublished = null;
        _publisher
            .Setup(p => p.PublishPaymentConfirmedAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                using var other = InMemoryDbContextHelper.Create(databaseName);
                committedStatusWhenPublished = other.Payments.Single(p => p.Id == created.Id).Status.ToString();
            })
            .Returns(Task.CompletedTask);

        await new VerifyPaymentHandler(db, Synchronizer(db)).ExecuteAsync(created.Id);

        committedStatusWhenPublished.Should().Be("PENDING");
        await using var after = InMemoryDbContextHelper.Create(databaseName);
        after.Payments.Single(p => p.Id == created.Id).Status.ToString().Should().Be("CONFIRMED");
    }
}
