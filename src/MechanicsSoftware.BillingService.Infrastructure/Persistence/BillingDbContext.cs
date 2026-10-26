using System.Reflection;
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
    }
}
