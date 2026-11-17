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

public sealed class ConcurrentWriteSimulator(Func<Task> otherRequestWins, Func<Exception> failure) : SaveChangesInterceptor
{
    private bool _armed = true;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!_armed)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        _armed = false;
        await otherRequestWins();
        throw failure();
    }
}
