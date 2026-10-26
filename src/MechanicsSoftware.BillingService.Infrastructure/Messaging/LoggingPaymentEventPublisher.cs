using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

/// <summary>
/// Interim <see cref="IPaymentEventPublisher"/> — same role as <see cref="LoggingBudgetEventPublisher"/>:
/// replaced by the MassTransit publisher once the broker (F4-08) and consumers (F4-13) exist (ADR-011).
/// </summary>
public sealed partial class LoggingPaymentEventPublisher(ILogger<LoggingPaymentEventPublisher> logger)
    : IPaymentEventPublisher
{
    public Task PublishPaymentConfirmedAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        LogPaymentConfirmed(payment.ServiceOrderId, payment.Id, payment.Amount.Cents);
        return Task.CompletedTask;
    }

    public Task PublishPaymentFailedAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        LogPaymentFailed(payment.ServiceOrderId, payment.Id, payment.FailureReason ?? "unknown");
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 10, Level = LogLevel.Information,
        Message = "Saga event PaymentConfirmed: ServiceOrder {ServiceOrderId}, Payment {PaymentId}, amount {AmountInCents} cents.")]
    private partial void LogPaymentConfirmed(Guid serviceOrderId, Guid paymentId, int amountInCents);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning,
        Message = "Saga event PaymentFailed: ServiceOrder {ServiceOrderId}, Payment {PaymentId}, reason: {Reason}")]
    private partial void LogPaymentFailed(Guid serviceOrderId, Guid paymentId, string reason);
}
