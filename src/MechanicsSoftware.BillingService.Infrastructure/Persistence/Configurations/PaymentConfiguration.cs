using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicsSoftware.BillingService.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    private static readonly Dictionary<string, PaymentStatus.Status> _statusMap = new()
    {
        ["PENDING"] = PaymentStatus.Status.Pending,
        ["CONFIRMED"] = PaymentStatus.Status.Confirmed,
        ["FAILED"] = PaymentStatus.Status.Failed,
    };

    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.BudgetId)
            .HasColumnName("budget_id")
            .IsRequired();

        // One payment per budget (creation is idempotent per budget).
        builder.HasIndex(p => p.BudgetId)
            .IsUnique()
            .HasDatabaseName("ux_payments_budget_id");

        builder.HasOne<Budget>()
            .WithMany()
            .HasForeignKey(p => p.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.ServiceOrderId)
            .HasColumnName("service_order_id")
            .IsRequired();

        builder.Property(p => p.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasConversion(m => m.Cents, v => new Money(v))
            .HasColumnName("amount")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion(s => s.ToString(), v => ParseStatus(v))
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.ProviderOrderId)
            .HasColumnName("provider_order_id")
            .HasMaxLength(64);

        builder.HasIndex(p => p.ProviderOrderId)
            .IsUnique()
            .HasDatabaseName("ux_payments_provider_order_id");

        builder.Property(p => p.QrData)
            .HasColumnName("qr_data")
            .HasMaxLength(1024);

        builder.Property(p => p.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(200);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.ConfirmedAt)
            .HasColumnName("confirmed_at");

        builder.Property(p => p.FailedAt)
            .HasColumnName("failed_at");
    }

    internal static PaymentStatus ParseStatus(string value) =>
        _statusMap.TryGetValue(value, out var status)
            ? new PaymentStatus(status)
            : throw new InvalidOperationException($"Unknown payment status: '{value}'");
}
