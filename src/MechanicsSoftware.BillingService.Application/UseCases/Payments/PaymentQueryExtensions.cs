using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments;

internal static class PaymentQueryExtensions
{
    internal static async Task<Payment> FindPaymentAsync(
        this IQueryable<Payment> query, Guid id, CancellationToken cancellationToken = default) =>
        await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(Payment), id);

    internal static Task<Payment?> FindByBudgetAsync(
        this IQueryable<Payment> query, Guid budgetId, CancellationToken cancellationToken = default) =>
        query.FirstOrDefaultAsync(p => p.BudgetId == budgetId, cancellationToken);

    /// <summary>Provider ids are stored upper-case (see <see cref="NormalizeProviderId"/>).</summary>
    internal static Task<Payment?> FindByProviderOrderAsync(
        this IQueryable<Payment> query, string providerOrderId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProviderId(providerOrderId);
        return query.FirstOrDefaultAsync(p => p.ProviderOrderId == normalized, cancellationToken);
    }

    internal static string NormalizeProviderId(string providerOrderId) => providerOrderId.Trim().ToUpperInvariant();
}
