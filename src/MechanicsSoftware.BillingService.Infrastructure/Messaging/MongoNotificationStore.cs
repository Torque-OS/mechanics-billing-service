using System.Diagnostics.CodeAnalysis;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

/// <summary>
/// Raw Mercado Pago webhook payloads, append-only, in MongoDB (ADR-012). The body is stored as a document
/// when it is valid JSON and as a string otherwise, so nothing received is ever lost.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin MongoDB adapter; exercised manually against the compose stack.")]
public sealed class MongoNotificationStore(IMongoDatabase database) : INotificationStore
{
    public const string CollectionName = "mercadopago_notifications";

    private readonly IMongoCollection<BsonDocument> _collection = database.GetCollection<BsonDocument>(CollectionName);

    public Task SaveAsync(MercadoPagoNotification notification, CancellationToken cancellationToken = default)
    {
        var document = new BsonDocument
        {
            ["receivedAt"] = notification.ReceivedAt,
            ["dataId"] = (BsonValue?)notification.DataId ?? BsonNull.Value,
            ["action"] = (BsonValue?)notification.Action ?? BsonNull.Value,
            ["requestId"] = (BsonValue?)notification.RequestId ?? BsonNull.Value,
            ["signature"] = (BsonValue?)notification.Signature ?? BsonNull.Value,
            ["body"] = ParseBody(notification.RawBody),
        };

        return _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    private static BsonValue ParseBody(string rawBody)
    {
        try
        {
            return BsonDocument.Parse(rawBody);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException
                                              or BsonSerializationException)
        {
            return rawBody;
        }
    }
}
