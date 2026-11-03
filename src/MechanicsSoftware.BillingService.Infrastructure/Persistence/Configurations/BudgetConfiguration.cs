using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicsSoftware.BillingService.Infrastructure.Persistence.Configurations;

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    private static readonly Dictionary<string, BudgetStatus.Status> _statusMap = new()
    {
        ["PENDING"] = BudgetStatus.Status.Pending,
        ["APPROVED"] = BudgetStatus.Status.Approved,
        ["REJECTED"] = BudgetStatus.Status.Rejected,
        ["CANCELLED"] = BudgetStatus.Status.Cancelled,
    };

    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(b => b.ServiceOrderId)
            .HasColumnName("service_order_id")
            .IsRequired();

        builder.HasIndex(b => b.ServiceOrderId)
            .IsUnique()
            .HasDatabaseName("ux_budgets_service_order_id");

        builder.Property(b => b.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.HasIndex(b => b.CustomerId)
            .HasDatabaseName("ix_budgets_customer_id");

        builder.Property(b => b.Total)
            .HasConversion(m => m.Cents, v => new Money(v))
            .HasColumnName("total")
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion(s => s.ToString(), v => ParseStatus(v))
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.DecidedAt)
            .HasColumnName("decided_at");

        builder.Property(b => b.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Navigation(b => b.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(b => b.Items, item =>
        {
            item.ToTable("budget_items");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();
            item.WithOwner().HasForeignKey(i => i.BudgetId);
            item.Property(i => i.BudgetId).HasColumnName("budget_id");
            item.HasIndex(i => i.BudgetId).HasDatabaseName("ix_budget_items_budget_id");
            item.Property(i => i.Type)
                .HasConversion<string>()
                .HasColumnName("type")
                .HasMaxLength(20)
                .IsRequired();
            item.Property(i => i.ReferenceId)
                .HasColumnName("reference_id")
                .IsRequired();
            item.Property(i => i.Description)
                .HasColumnName("description")
                .HasMaxLength(200)
                .IsRequired();
            item.Property(i => i.UnitPrice)
                .HasConversion(m => m.Cents, v => new Money(v))
                .HasColumnName("unit_price")
                .IsRequired();
            item.Property(i => i.Quantity)
                .HasColumnName("quantity")
                .IsRequired();
            item.Ignore(i => i.Total);
        });
    }

    internal static BudgetStatus ParseStatus(string value) =>
        _statusMap.TryGetValue(value, out var status)
            ? new BudgetStatus(status)
            : throw new InvalidOperationException($"Unknown budget status: '{value}'");
}
