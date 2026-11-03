using FluentAssertions;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Enums;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;
using MechanicsSoftware.BillingService.UnitTests.Helpers;

namespace MechanicsSoftware.BillingService.UnitTests.Domain;

public class BudgetTests
{
    [Fact]
    public void Create_ValidItems_CreatesPendingBudgetWithTotal()
    {
        var serviceOrderId = Guid.NewGuid();

        var budget = Budget.Create(serviceOrderId, Guid.NewGuid(), [
            BudgetFixtures.ServiceDraft(unitPriceInCents: 15000, quantity: 1),
            BudgetFixtures.PartDraft(unitPriceInCents: 4500, quantity: 2)
        ]);

        budget.Id.Should().NotBeEmpty();
        budget.ServiceOrderId.Should().Be(serviceOrderId);
        budget.Status.Value.Should().Be(BudgetStatus.Status.Pending);
        budget.Total.Should().Be(new Money(24000));
        budget.DecidedAt.Should().BeNull();
        budget.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        budget.Items.Should().HaveCount(2);
        budget.Items.Should().OnlyContain(item => item.BudgetId == budget.Id);
    }

    [Fact]
    public void Create_ValidItems_MapsItemFields()
    {
        var draft = BudgetFixtures.PartDraft(unitPriceInCents: 4500, quantity: 2) with { Description = "  Oil filter  " };

        var budget = Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [BudgetFixtures.ServiceDraft(), draft]);
        var item = budget.Items.Single(i => i.Type == BudgetItemType.Part);

        item.Id.Should().NotBeEmpty();
        item.ReferenceId.Should().Be(draft.ReferenceId);
        item.Description.Should().Be("Oil filter");
        item.UnitPrice.Should().Be(new Money(4500));
        item.Quantity.Should().Be(2);
        item.Total.Should().Be(new Money(9000));
    }

    [Fact]
    public void Create_EmptyServiceOrderId_ThrowsDomainException()
    {
        var act = () => Budget.Create(Guid.Empty, Guid.NewGuid(), [BudgetFixtures.ServiceDraft()]);

        act.Should().Throw<DomainException>().WithMessage("ServiceOrderId is required.");
    }

    [Fact]
    public void Create_EmptyCustomerId_ThrowsDomainException()
    {
        var act = () => Budget.Create(Guid.NewGuid(), Guid.Empty, [BudgetFixtures.ServiceDraft()]);

        act.Should().Throw<DomainException>().WithMessage("CustomerId is required.");
    }

    [Fact]
    public void Create_ValidItems_KeepsCustomerId()
    {
        var customerId = Guid.NewGuid();

        var budget = BudgetFixtures.CreateBudget(customerId: customerId);

        budget.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public void BelongsTo_OwnerAndOtherCustomer_ComparesCustomerId()
    {
        var customerId = Guid.NewGuid();
        var budget = BudgetFixtures.CreateBudget(customerId: customerId);

        budget.BelongsTo(customerId).Should().BeTrue();
        budget.BelongsTo(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Create_NoItems_ThrowsDomainException()
    {
        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), []);

        act.Should().Throw<DomainException>().WithMessage("*at least one item*");
    }

    [Fact]
    public void Create_NullItems_ThrowsDomainException()
    {
        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), null!);

        act.Should().Throw<DomainException>().WithMessage("*at least one item*");
    }

    [Fact]
    public void Create_OnlyPartItems_ThrowsDomainException()
    {
        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [BudgetFixtures.PartDraft()]);

        act.Should().Throw<DomainException>().WithMessage("*at least one service item*");
    }

    [Fact]
    public void Create_NullItemAmongItems_ThrowsDomainException()
    {
        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [BudgetFixtures.ServiceDraft(), null!]);

        act.Should().Throw<DomainException>().WithMessage("Budget item is required.");
    }

    [Fact]
    public void Create_InvalidItemType_ThrowsDomainException()
    {
        var invalid = BudgetFixtures.PartDraft() with { Type = (BudgetItemType)42 };

        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [BudgetFixtures.ServiceDraft(), invalid]);

        act.Should().Throw<DomainException>().WithMessage("*type '42' is invalid*");
    }

    [Fact]
    public void Create_EmptyReferenceId_ThrowsDomainException()
    {
        var invalid = BudgetFixtures.ServiceDraft() with { ReferenceId = Guid.Empty };

        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [invalid]);

        act.Should().Throw<DomainException>().WithMessage("*reference id is required*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankDescription_ThrowsDomainException(string description)
    {
        var invalid = BudgetFixtures.ServiceDraft() with { Description = description };

        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [invalid]);

        act.Should().Throw<DomainException>().WithMessage("*description is required*");
    }

    [Fact]
    public void Create_NullUnitPrice_ThrowsDomainException()
    {
        var invalid = BudgetFixtures.ServiceDraft() with { UnitPrice = null! };

        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [invalid]);

        act.Should().Throw<DomainException>().WithMessage("*unit price is required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_NonPositiveQuantity_ThrowsDomainException(int quantity)
    {
        var invalid = BudgetFixtures.ServiceDraft(quantity: quantity);

        var act = () => Budget.Create(Guid.NewGuid(), Guid.NewGuid(), [invalid]);

        act.Should().Throw<DomainException>().WithMessage("*quantity must be greater than zero*");
    }

    [Fact]
    public void Approve_PendingBudget_SetsApprovedAndDecidedAt()
    {
        var budget = BudgetFixtures.CreateBudget();

        budget.Approve();

        budget.Status.Value.Should().Be(BudgetStatus.Status.Approved);
        budget.DecidedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Reject_PendingBudget_SetsRejectedAndDecidedAt()
    {
        var budget = BudgetFixtures.CreateBudget();

        budget.Reject();

        budget.Status.Value.Should().Be(BudgetStatus.Status.Rejected);
        budget.DecidedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_AlreadyRejectedBudget_ThrowsInvalidStatusTransitionException()
    {
        var budget = BudgetFixtures.CreateBudget();
        budget.Reject();

        var act = budget.Approve;

        act.Should().Throw<InvalidStatusTransitionException>();
        budget.Status.Value.Should().Be(BudgetStatus.Status.Rejected);
    }

    [Fact]
    public void Reject_AlreadyApprovedBudget_ThrowsInvalidStatusTransitionException()
    {
        var budget = BudgetFixtures.CreateBudget();
        budget.Approve();

        var act = budget.Reject;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Equals_SameInstance_ReturnsTrue()
    {
        var budget = BudgetFixtures.CreateBudget();

        budget.Equals(budget).Should().BeTrue();
        budget.GetHashCode().Should().Be(budget.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentBudgetsOrTypes_ReturnsFalse()
    {
        var budget = BudgetFixtures.CreateBudget();

        budget.Equals(BudgetFixtures.CreateBudget()).Should().BeFalse();
        budget.Equals(budget.Items.First()).Should().BeFalse();
        budget.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Cancel_PendingBudgetWithoutPayment_SetsCancelledAndKeepsDecision()
    {
        var budget = BudgetFixtures.CreateBudget();

        budget.Cancel(payment: null);

        budget.IsCancelled.Should().BeTrue();
        budget.Status.ToString().Should().Be("CANCELLED");
        budget.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        budget.DecidedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("refund")]
    [InlineData("fail")]
    public void Cancel_ApprovedBudgetWithClosedPayment_SetsCancelled(string closePayment)
    {
        var budget = BudgetFixtures.CreateApprovedBudget();
        var payment = Payment.CreateFor(budget);
        switch (closePayment)
        {
            case "cancel": payment.Cancel(); break;
            case "fail": payment.Fail("expired"); break;
            default: payment.Confirm(); payment.Refund(); break;
        }

        budget.Cancel(payment);

        budget.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public void Cancel_WithPendingPayment_ThrowsConflict()
    {
        var budget = BudgetFixtures.CreateApprovedBudget();
        var payment = Payment.CreateFor(budget);

        var act = () => budget.Cancel(payment);

        act.Should().Throw<ConflictException>().WithMessage("*PENDING*cancel or refund*");
        budget.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void Cancel_WithConfirmedPayment_ThrowsConflict_MoneyMustBeRefundedFirst()
    {
        var budget = BudgetFixtures.CreateApprovedBudget();
        var payment = Payment.CreateFor(budget);
        payment.Confirm();

        var act = () => budget.Cancel(payment);

        act.Should().Throw<ConflictException>().WithMessage("*CONFIRMED*");
    }

    [Fact]
    public void Cancel_WithAnotherBudgetsPayment_ThrowsDomainException()
    {
        var budget = BudgetFixtures.CreateApprovedBudget();
        var payment = Payment.CreateFor(BudgetFixtures.CreateApprovedBudget());
        payment.Cancel();

        var act = () => budget.Cancel(payment);

        act.Should().Throw<DomainException>().WithMessage("*does not belong*");
    }

    [Fact]
    public void Cancel_RejectedBudget_ThrowsInvalidStatusTransitionException()
    {
        var budget = BudgetFixtures.CreateBudget();
        budget.Reject();

        var act = () => budget.Cancel(payment: null);

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
