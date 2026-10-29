using System.Reflection;
using MassTransit;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.Infrastructure.Persistence;

public sealed class BillingDbContext : DbContext, IBillingDbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    public DbSet<Budget> Budgets { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // MassTransit transactional outbox (ADR-011): Saga events are stored in the same transaction as the
        // state change and delivered to RabbitMQ after commit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
