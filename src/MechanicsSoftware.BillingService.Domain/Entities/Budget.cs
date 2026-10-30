using MechanicsSoftware.BillingService.Domain.Enums;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Domain.Entities;

public sealed class Budget : Entity<Guid>
{
    private readonly List<BudgetItem> _items = [];

    public Guid ServiceOrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Money Total { get; private set; } = null!;
    public BudgetStatus Status { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public IReadOnlyCollection<BudgetItem> Items => _items.AsReadOnly();

    private Budget() { }

    public static Budget Create(Guid serviceOrderId, Guid customerId, IReadOnlyCollection<BudgetItemDraft> items)
    {
        if (serviceOrderId == Guid.Empty)
            throw new DomainException("ServiceOrderId is required.");

        if (customerId == Guid.Empty)
            throw new DomainException("CustomerId is required.");

        if (items is null || items.Count == 0)
            throw new DomainException("A budget requires at least one item.");

        if (!items.Any(item => item?.Type == BudgetItemType.Service))
            throw new DomainException("A budget requires at least one service item.");

        var budget = new Budget
        {
            Id = Guid.NewGuid(),
            ServiceOrderId = serviceOrderId,
            CustomerId = customerId,
            Status = BudgetStatus.CreatePending(),
            CreatedAt = DateTime.UtcNow
        };

        budget._items.AddRange(items.Select(item => BudgetItem.Create(budget.Id, item)));
        budget.Total = budget._items.Aggregate(Money.Zero, (total, item) => total.Add(item.Total));

        return budget;
    }

    public bool BelongsTo(Guid customerId) => CustomerId == customerId;

    public void Approve() => Decide(BudgetStatus.Status.Approved);

    public void Reject() => Decide(BudgetStatus.Status.Rejected);

    public bool IsCancelled => Status.Value == BudgetStatus.Status.Cancelled;

    /// <summary>
    /// Saga compensation (ADR-011): the service order was cancelled. Money must never be left behind — while the
    /// payment is open (<c>PENDING</c>) or received (<c>CONFIRMED</c>) it has to be cancelled or refunded first.
    /// </summary>
    public void Cancel(Payment? payment)
    {
        if (payment is not null && payment.BudgetId != Id)
            throw new DomainException("Payment does not belong to this budget.");

        if (payment?.Status.Value is PaymentStatus.Status.Pending or PaymentStatus.Status.Confirmed)
            throw new ConflictException(
                $"Budget cannot be cancelled while its payment is '{payment.Status}'; cancel or refund the payment first.");

        Status = Status.TransitionTo(BudgetStatus.Status.Cancelled);
        CancelledAt = DateTime.UtcNow;
    }

    private void Decide(BudgetStatus.Status decision)
    {
        Status = Status.TransitionTo(decision);
        DecidedAt = DateTime.UtcNow;
    }
}
