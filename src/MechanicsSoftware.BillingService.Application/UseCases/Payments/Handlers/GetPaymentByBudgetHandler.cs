using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;

public sealed class GetPaymentByBudgetHandler(IBillingDbContext db)
{
    public async Task<PaymentResponse> ExecuteAsync(Guid budgetId, CancellationToken cancellationToken = default) =>
        PaymentResponse.From(
            await db.Payments.FindByBudgetAsync(budgetId, cancellationToken)
            ?? throw new NotFoundException(nameof(Payment), budgetId));
}
