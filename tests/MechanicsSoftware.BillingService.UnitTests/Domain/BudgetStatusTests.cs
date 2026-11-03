using FluentAssertions;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.UnitTests.Domain;

public class BudgetStatusTests
{
    [Fact]
    public void CreatePending_ReturnsUndecidedPendingStatus()
    {
        var status = BudgetStatus.CreatePending();

        status.Value.Should().Be(BudgetStatus.Status.Pending);
        status.IsDecided.Should().BeFalse();
    }

    [Theory]
    [InlineData(BudgetStatus.Status.Approved)]
    [InlineData(BudgetStatus.Status.Rejected)]
    public void TransitionTo_FromPendingToDecision_ReturnsDecidedStatus(BudgetStatus.Status decision)
    {
        var status = BudgetStatus.CreatePending().TransitionTo(decision);

        status.Value.Should().Be(decision);
        status.IsDecided.Should().BeTrue();
    }

    [Fact]
    public void TransitionTo_FromPendingToPending_ThrowsInvalidStatusTransitionException()
    {
        var act = () => BudgetStatus.CreatePending().TransitionTo(BudgetStatus.Status.Pending);

        act.Should().Throw<InvalidStatusTransitionException>()
            .WithMessage("*'PENDING' to 'PENDING'*");
    }

    [Theory]
    [InlineData(BudgetStatus.Status.Approved, BudgetStatus.Status.Rejected)]
    [InlineData(BudgetStatus.Status.Approved, BudgetStatus.Status.Approved)]
    [InlineData(BudgetStatus.Status.Rejected, BudgetStatus.Status.Approved)]
    [InlineData(BudgetStatus.Status.Rejected, BudgetStatus.Status.Pending)]
    public void TransitionTo_FromDecidedStatus_ThrowsInvalidStatusTransitionException(
        BudgetStatus.Status current, BudgetStatus.Status target)
    {
        var act = () => new BudgetStatus(current).TransitionTo(target);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Theory]
    [InlineData(BudgetStatus.Status.Pending, "PENDING")]
    [InlineData(BudgetStatus.Status.Approved, "APPROVED")]
    [InlineData(BudgetStatus.Status.Rejected, "REJECTED")]
    public void ToString_KnownStatus_ReturnsUpperSnakeCase(BudgetStatus.Status value, string expected)
    {
        new BudgetStatus(value).ToString().Should().Be(expected);
    }

    [Fact]
    public void ToString_UnknownStatus_ReturnsEnumName()
    {
        new BudgetStatus((BudgetStatus.Status)99).ToString().Should().Be("99");
    }

    [Theory]
    [InlineData(BudgetStatus.Status.Pending)]
    [InlineData(BudgetStatus.Status.Approved)]
    public void TransitionTo_Cancelled_FromPendingOrApproved_IsAllowed(BudgetStatus.Status current)
    {
        var status = new BudgetStatus(current).TransitionTo(BudgetStatus.Status.Cancelled);

        status.Value.Should().Be(BudgetStatus.Status.Cancelled);
        status.ToString().Should().Be("CANCELLED");
    }

    [Theory]
    [InlineData(BudgetStatus.Status.Rejected, BudgetStatus.Status.Cancelled)]
    [InlineData(BudgetStatus.Status.Cancelled, BudgetStatus.Status.Cancelled)]
    [InlineData(BudgetStatus.Status.Cancelled, BudgetStatus.Status.Approved)]
    [InlineData(BudgetStatus.Status.Cancelled, BudgetStatus.Status.Pending)]
    public void TransitionTo_FromFinalStatus_ThrowsInvalidStatusTransitionException(
        BudgetStatus.Status current, BudgetStatus.Status target)
    {
        var act = () => new BudgetStatus(current).TransitionTo(target);

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
