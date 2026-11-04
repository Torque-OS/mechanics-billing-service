using MechanicsSoftware.BillingService.Application.Abstractions;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;

public sealed class VerifyPaymentHandler(IBillingDbContext db, PaymentSynchronizer synchronizer)
{
    public async Task<PaymentResponse> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.FindPaymentAsync(id, cancellationToken);

        await synchronizer.SyncAsync(payment, cancellationToken);

        return PaymentResponse.From(payment);
    }
}
