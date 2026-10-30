using FluentAssertions;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;
using MechanicsSoftware.BillingService.UnitTests.Helpers;

namespace MechanicsSoftware.BillingService.UnitTests.Domain;

public class PaymentTests
{
    [Fact]
    public void CreateFor_ApprovedBudget_CreatesPendingPaymentWithBudgetTotal()
    {
        var budget = BudgetFixtures.CreateApprovedBudget();

        var payment = Payment.CreateFor(budget);

        payment.Id.Should().NotBeEmpty();
        payment.BudgetId.Should().Be(budget.Id);
        payment.ServiceOrderId.Should().Be(budget.ServiceOrderId);
        payment.CustomerId.Should().Be(budget.CustomerId);
        payment.Amount.Should().Be(budget.Total);
        payment.Status.Value.Should().Be(PaymentStatus.Status.Pending);
        payment.ProviderOrderId.Should().BeNull();
        payment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateFor_PendingBudget_ThrowsConflict()
    {
        var act = () => Payment.CreateFor(BudgetFixtures.CreateBudget());

        act.Should().Throw<ConflictException>().WithMessage("*approved*PENDING*");
    }

    [Fact]
    public void CreateFor_RejectedBudget_ThrowsConflict()
    {
        var budget = BudgetFixtures.CreateBudget();
        budget.Reject();

        var act = () => Payment.CreateFor(budget);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void CreateFor_NullBudget_Throws()
    {
        var act = () => Payment.CreateFor(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateFor_ZeroTotal_ThrowsDomainException()
    {
        var budget = Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [BudgetFixtures.ServiceDraft(unitPriceInCents: 0)]);
        budget.Approve();

        var act = () => Payment.CreateFor(budget);

        act.Should().Throw<DomainException>().WithMessage("*zero total*");
    }

    [Fact]
    public void AttachProviderOrder_StoresIdsOnce()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());

        payment.AttachProviderOrder("ORD1", "qr-data");

        payment.ProviderOrderId.Should().Be("ORD1");
        payment.QrData.Should().Be("qr-data");
        payment.Invoking(p => p.AttachProviderOrder("ORD2", "other")).Should().Throw<ConflictException>();
    }

    [Theory]
    [InlineData("", "qr")]
    [InlineData("ORD1", " ")]
    public void AttachProviderOrder_MissingValues_ThrowsDomainException(string orderId, string qrData)
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());

        payment.Invoking(p => p.AttachProviderOrder(orderId, qrData)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_Pending_SetsConfirmedAndDate()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());

        payment.Confirm();

        payment.Status.ToString().Should().Be("CONFIRMED");
        payment.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_Pending_SetsFailedReasonAndDate()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());

        payment.Fail("  Mercado Pago order expired  ");

        payment.Status.ToString().Should().Be("FAILED");
        payment.FailureReason.Should().Be("Mercado Pago order expired");
        payment.FailedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_BlankReason_StoresUnknown()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());

        payment.Fail(" ");

        payment.FailureReason.Should().Be("unknown");
    }

    [Fact]
    public void FinalPayment_CannotTransitionAgain()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());
        payment.Confirm();

        payment.Invoking(p => p.Fail("late")).Should().Throw<InvalidPaymentStatusTransitionException>();
        payment.Invoking(p => p.Confirm()).Should().Throw<InvalidPaymentStatusTransitionException>();
    }

    [Fact]
    public void BelongsTo_ComparesCustomer()
    {
        var customerId = Guid.NewGuid();
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget(customerId));

        payment.BelongsTo(customerId).Should().BeTrue();
        payment.BelongsTo(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void PaymentStatus_PendingToPending_IsInvalid()
    {
        var act = () => PaymentStatus.CreatePending().TransitionTo(PaymentStatus.Status.Pending);

        act.Should().Throw<InvalidPaymentStatusTransitionException>();
    }

    [Fact]
    public void Cancel_Pending_SetsCancelledAndDate()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());

        payment.Cancel();

        payment.Status.ToString().Should().Be("CANCELLED");
        payment.Status.IsFinal.Should().BeTrue();
        payment.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Refund_Confirmed_SetsRefundedAndDate()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());
        payment.Confirm();
        payment.Status.IsFinal.Should().BeFalse("a confirmed payment can still be refunded");

        payment.Refund();

        payment.Status.ToString().Should().Be("REFUNDED");
        payment.Status.IsFinal.Should().BeTrue();
        payment.RefundedAt.Should().NotBeNull();
    }

    [Fact]
    public void Refund_Pending_IsInvalid_NothingToGiveBack()
    {
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());

        payment.Invoking(p => p.Refund()).Should().Throw<InvalidPaymentStatusTransitionException>();
    }

    [Theory]
    [InlineData(PaymentStatus.Status.Confirmed, PaymentStatus.Status.Cancelled)]
    [InlineData(PaymentStatus.Status.Failed, PaymentStatus.Status.Cancelled)]
    [InlineData(PaymentStatus.Status.Failed, PaymentStatus.Status.Refunded)]
    [InlineData(PaymentStatus.Status.Cancelled, PaymentStatus.Status.Confirmed)]
    [InlineData(PaymentStatus.Status.Refunded, PaymentStatus.Status.Refunded)]
    [InlineData(PaymentStatus.Status.Refunded, PaymentStatus.Status.Confirmed)]
    public void PaymentStatus_InvalidCompensationTransitions_Throw(PaymentStatus.Status current, PaymentStatus.Status target)
    {
        var act = () => new PaymentStatus(current).TransitionTo(target);

        act.Should().Throw<InvalidPaymentStatusTransitionException>();
    }
}
