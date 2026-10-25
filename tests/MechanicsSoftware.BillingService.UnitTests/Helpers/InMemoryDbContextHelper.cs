using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

public static class InMemoryDbContextHelper
{
    public static BillingDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new BillingDbContext(options);
    }
}
