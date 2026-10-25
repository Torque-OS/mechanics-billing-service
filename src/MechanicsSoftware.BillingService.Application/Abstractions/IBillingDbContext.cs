using MechanicsSoftware.BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.Application.Abstractions;

public interface IBillingDbContext
{
    DbSet<Budget> Budgets { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
