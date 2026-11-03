using System.Diagnostics.CodeAnalysis;
using MechanicsSoftware.BillingService.Application.Abstractions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

/// <summary>
/// Payment lifecycle audit trail in MongoDB (<c>payment_events</c>, append-only, ADR-012). Failures are logged and
/// swallowed: the payment is already committed and its Saga event is in the outbox, so history never blocks them.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin MongoDB adapter; exercised manually against the compose stack.")]
public sealed partial class MongoPaymentHistory(IMongoDatabase database, ILogger<MongoPaymentHistory> logger)
    : IPaymentHistory
{
    public const string CollectionName = "payment_events";

    private readonly IMongoCollection<BsonDocument> _collection = database.GetCollection<BsonDocument>(CollectionName);

    public async Task RecordAsync(PaymentHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        var document = new BsonDocument
        {
            ["paymentId"] = entry.PaymentId.ToString(),
            ["serviceOrderId"] = entry.ServiceOrderId.ToString(),
            ["budgetId"] = entry.BudgetId.ToString(),
            ["event"] = entry.Event,
            ["status"] = entry.Status,
            ["amountInCents"] = entry.AmountInCents,
            ["providerOrderId"] = (BsonValue?)entry.ProviderOrderId ?? BsonNull.Value,
            ["providerStatus"] = (BsonValue?)entry.ProviderStatus ?? BsonNull.Value,
            ["providerSnapshot"] = Parse(entry.ProviderSnapshotJson),
            ["reason"] = (BsonValue?)entry.Reason ?? BsonNull.Value,
            ["occurredAt"] = entry.OccurredAt,
            ["recordedAt"] = DateTime.UtcNow,
        };

        try
        {
            await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            LogHistoryFailure(exception, entry.PaymentId, entry.Event);
        }
    }

    private static BsonValue Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return BsonNull.Value;
        try
        {
            return BsonDocument.Parse(json);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or BsonSerializationException)
        {
            return json;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not record payment history for Payment {PaymentId} ({Event})")]
    private partial void LogHistoryFailure(Exception exception, Guid paymentId, string @event);
}
