using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;

public sealed record CreatePaymentCommand(Guid BudgetId, BudgetRequester Requester);

public sealed class CreatePaymentHandler(
    IBillingDbContext db,
    IMercadoPagoClient mercadoPago,
    IPaymentHistory history,
    IReadOnlyList<TimeSpan>? providerLockedRetryDelays = null)
{
    private static readonly TimeSpan[] DefaultProviderLockedRetryDelays =
        [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

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

        var order = await CreateProviderOrderAsync(
            new MercadoPagoQrOrderRequest(
                ExternalReference: budget.Id.ToString(),
                Amount: payment.Amount,
                Description: $"Torque-OS service order {payment.ServiceOrderId}"),
            idempotencyKey: budget.Id.ToString(),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(order.QrData))
            throw PaymentSynchronizer.MissingQrData(order.Id);

        payment.AttachProviderOrder(PaymentQueryExtensions.NormalizeProviderId(order.Id), order.QrData);

        db.Payments.Add(payment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.DiscardChanges();
            var winner = await db.Payments.FindByBudgetAsync(budget.Id, cancellationToken);
            if (winner is null)
                throw;

            return new CreatePaymentResult(PaymentResponse.From(winner), Created: false);
        }

        await history.RecordAsync(PaymentSynchronizer.Entry(payment, "created", order), cancellationToken);

        return new CreatePaymentResult(PaymentResponse.From(payment), Created: true);
    }

    private async Task<MercadoPagoOrder> CreateProviderOrderAsync(
        MercadoPagoQrOrderRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        foreach (var delay in providerLockedRetryDelays ?? DefaultProviderLockedRetryDelays)
        {
            try
            {
                return await mercadoPago.CreateQrOrderAsync(request, idempotencyKey, cancellationToken);
            }
            catch (ProviderRequestInProgressException)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        try
        {
            return await mercadoPago.CreateQrOrderAsync(request, idempotencyKey, cancellationToken);
        }
        catch (ProviderRequestInProgressException)
        {
            throw new ConflictException("A payment for this budget is already being created. Try again in a moment.");
        }
    }
}
