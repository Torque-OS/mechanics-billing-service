using System.Text;
using System.Text.Json;
using MechanicsSoftware.BillingService.Api.Extensions;
using MechanicsSoftware.BillingService.Api.Transport.Payments;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Payments;
using MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;
using MechanicsSoftware.BillingService.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace MechanicsSoftware.BillingService.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize(Policy = Policies.CustomerOrStaff)]
public partial class PaymentsController(
    CreatePaymentHandler createHandler,
    GetPaymentHandler getHandler,
    VerifyPaymentHandler verifyHandler,
    HandleMercadoPagoNotificationHandler notificationHandler,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Create(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        if (User.ToRequester() is not { } requester)
            return Forbid();

        var result = await createHandler.ExecuteAsync(new CreatePaymentCommand(request.BudgetId, requester), cancellationToken);

        return result.Created
            ? CreatedAtAction(nameof(Get), new { id = result.Payment.Id }, result.Payment)
            : Ok(result.Payment);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await getHandler.ExecuteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/verify")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Verify(Guid id, CancellationToken cancellationToken) =>
        Ok(await verifyHandler.ExecuteAsync(id, cancellationToken));

    [HttpGet("{id:guid}/qr.png")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "image/png")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> QrCode(Guid id, CancellationToken cancellationToken)
    {
        var payment = await getHandler.ExecuteAsync(id, cancellationToken);

        if (payment.Status != "PENDING")
            return Conflict(new { error = $"Payment is {payment.Status}; there is nothing to pay." });

        if (string.IsNullOrWhiteSpace(payment.QrData))
            return NotFound(new { error = "Payment has no QR code." });

        var png = PngByteQRCodeHelper.GetQRCode(payment.QrData, QRCodeGenerator.ECCLevel.M, 10);
        return File(png, "image/png");
    }

    [HttpPost("webhooks/mercadopago")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MercadoPagoWebhook(CancellationToken cancellationToken)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            rawBody = await reader.ReadToEndAsync(cancellationToken);

        var (bodyDataId, action) = ReadBody(rawBody);
        var notification = new MercadoPagoNotification(
            Signature: Request.Headers["x-signature"].FirstOrDefault(),
            RequestId: Request.Headers["x-request-id"].FirstOrDefault(),
            DataId: Request.Query["data.id"].FirstOrDefault() ?? bodyDataId,
            Action: action,
            RawBody: rawBody,
            ReceivedAt: DateTime.UtcNow);

        try
        {
            var outcome = await notificationHandler.ExecuteAsync(notification, cancellationToken);
            return Ok(new { outcome = outcome.ToString() });
        }
        catch (InvalidWebhookSignatureException)
        {
            LogRejectedWebhook(notification.RequestId, Request.Query["data.id"].FirstOrDefault(), bodyDataId, notification.Signature);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rejected Mercado Pago webhook: request-id {RequestId}, query data.id {QueryDataId}, body data.id {BodyDataId}, x-signature {Signature}")]
    private partial void LogRejectedWebhook(string? requestId, string? queryDataId, string? bodyDataId, string? signature);

    private static (string? DataId, string? Action) ReadBody(string rawBody)
    {
        try
        {
            using var json = JsonDocument.Parse(rawBody);
            var root = json.RootElement;
            var action = root.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
            var dataId = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object
                         && d.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String
                ? i.GetString()
                : null;
            return (dataId, action);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
