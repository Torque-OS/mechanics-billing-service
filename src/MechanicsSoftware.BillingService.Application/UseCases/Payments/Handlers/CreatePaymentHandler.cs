using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;

public sealed record CreatePaymentCommand(Guid BudgetId, BudgetRequester Requester);

/// <summary>
/// Charges an approved budget with a dynamic Mercado Pago QR. One payment per budget: a repeated request
/// returns the existing payment. Nothing is stored when Mercado Pago rejects the order.
/// </summary>
public sealed class CreatePaymentHandler(IBillingDbContext db, IMercadoPagoClient mercadoPago, IPaymentHistory history)
{
    public async Task<CreatePaymentResult> ExecuteAsync(
        CreatePaymentCommand command, CancellationToken cancellationToken = default)
    {
        var budget = await db.Budgets.FindWithItemsAsync(command.BudgetId, cancellationToken);

        if (!command.Requester.IsStaff && !budget.BelongsTo(command.Requester.UserId))
            throw new ForbiddenException("Customers can only pay their own budgets.");

        var existing = await db.Payments.FindByBudgetAsync(budget.Id, cancellationToken);
        if (existing is not null)
            return new CreatePaymentResult(PaymentResponse.From(existing), Created: false);

        var payment = Payment.CreateFor(budget);

        var order = await mercadoPago.CreateQrOrderAsync(
            new MercadoPagoQrOrderRequest(
                ExternalReference: payment.Id.ToString(),
                Amount: payment.Amount,
                Description: $"Torque-OS service order {payment.ServiceOrderId}"),
            idempotencyKey: payment.Id.ToString(),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(order.QrData))
            throw PaymentSynchronizer.MissingQrData(order.Id);

        payment.AttachProviderOrder(PaymentQueryExtensions.NormalizeProviderId(order.Id), order.QrData);

        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        await history.RecordAsync(PaymentSynchronizer.Entry(payment, "created", order), cancellationToken);

        return new CreatePaymentResult(PaymentResponse.From(payment), Created: true);
    }
}
