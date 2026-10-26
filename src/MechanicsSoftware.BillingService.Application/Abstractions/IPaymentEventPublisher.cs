using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.Abstractions;

public interface IPaymentEventPublisher
{
    Task PublishPaymentConfirmedAsync(Payment payment, CancellationToken cancellationToken = default);
    Task PublishPaymentFailedAsync(Payment payment, CancellationToken cancellationToken = default);
}
