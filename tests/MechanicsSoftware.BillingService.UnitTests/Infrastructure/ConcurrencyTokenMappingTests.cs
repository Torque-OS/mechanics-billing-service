using FluentAssertions;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.UnitTests.Infrastructure;

public class ConcurrencyTokenMappingTests
{
    [Theory]
    [InlineData(typeof(Payment))]
    [InlineData(typeof(Budget))]
    public void Entity_UsesPostgresXminAsConcurrencyToken(Type entity)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql("Host=unused").Options;
        using var db = new BillingDbContext(options);

        var version = db.Model.FindEntityType(entity)!.FindProperty("Version")!;

        version.IsConcurrencyToken.Should().BeTrue();
        version.GetColumnName().Should().Be("xmin");
        version.GetColumnType().Should().Be("xid");
    }
}
