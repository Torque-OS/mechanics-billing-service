using MechanicsSoftware.BillingService.Application.Abstractions;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;

public sealed class GetPaymentHandler(IBillingDbContext db)
{
    public async Task<PaymentResponse> ExecuteAsync(Guid id, CancellationToken cancellationToken = default) =>
        PaymentResponse.From(await db.Payments.FindPaymentAsync(id, cancellationToken));
}
