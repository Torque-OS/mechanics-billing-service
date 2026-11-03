using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

public static class InMemoryDbContextHelper
{
    public static BillingDbContext Create(string? dbName = null, IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString());

        if (interceptor is not null)
            builder.AddInterceptors(interceptor);

        return new BillingDbContext(builder.Options);
    }
}

/// <summary>Counts SaveChanges calls — with the outbox, a published event only leaves if SaveChanges runs.</summary>
public sealed class SaveChangesCounter : SaveChangesInterceptor
{
    public int Count { get; private set; }

    public void Reset() => Count = 0;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Count++;
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
