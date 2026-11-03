using FluentAssertions;
using MechanicsSoftware.BillingService.Infrastructure.MercadoPago;

namespace MechanicsSoftware.BillingService.UnitTests.Infrastructure;

public class MercadoPagoSignatureValidatorTests
{
    private const string Secret = "my-webhook-secret";
    private const string DataId = "ORD01JQ4S4KY8HWQASX1AW5V4F3E8";
    private const string RequestId = "bb56a2f1-6aae-46ac-982e-9dcd3581d08e";
    private const string Ts = "1742505638683";

    // Reference values computed outside .NET (openssl dgst -sha256 -hmac my-webhook-secret):
    // documented form, id lower-cased: 'id:ord01jq4s4ky8hwqasx1aw5v4f3e8;request-id:bb56a2f1-...;ts:1742505638683;'
    private const string ExpectedV1 = "940c1f391570af0cfc3bbe79a0044cae8452841a9e7ba68c0d5f404ebc3ef65f";
    // form observed in the sandbox, id as received and ts in seconds: 'id:ORD01JQ4S4KY8HWQASX1AW5V4F3E8;request-id:bb56a2f1-...;ts:1742505638;'
    private const string ExpectedV1AsReceived = "d525620008898457b6a5aa672e8edf96f572a057b992d35c27b61d8cdf92376f";
    private const string ExpectedV1WithoutDataId = "49013eb14d8a186ecff8e4311fd879578a12e5338538c09609ff27392bfdd7d6";

    private static MercadoPagoSignatureValidator Validator(string? secret = Secret) =>
        new(new MercadoPagoOptions(null, secret, null, MercadoPagoOptions.DefaultBaseAddress, "PT15M"));

    [Fact]
    public void IsValid_ReferenceVector_ReturnsTrue()
    {
        Validator().IsValid($"ts={Ts},v1={ExpectedV1}", RequestId, DataId).Should().BeTrue();
    }

    [Fact]
    public void IsValid_IdSignedAsReceived_ReturnsTrue()
    {
        // What Mercado Pago actually sent for Order notifications in the sandbox (2026-10-26).
        Validator().IsValid($"ts=1742505638,v1={ExpectedV1AsReceived}", RequestId, DataId).Should().BeTrue();
    }

    [Fact]
    public void IsValid_AsReceivedSignatureWithAnotherCasing_ReturnsFalse()
    {
        Validator().IsValid($"ts=1742505638,v1={ExpectedV1AsReceived}", RequestId, DataId.ToLowerInvariant()).Should().BeFalse();
    }

    [Fact]
    public void IsValid_HeaderWithSpacesAndUpperCaseHex_ReturnsTrue()
    {
        Validator().IsValid($"ts={Ts}, v1={ExpectedV1.ToUpperInvariant()}", RequestId, DataId).Should().BeTrue();
    }

    [Fact]
    public void IsValid_MissingDataIdIsOmittedFromManifest()
    {
        Validator().IsValid($"ts=1700000000000,v1={ExpectedV1WithoutDataId}", "req-1", null).Should().BeTrue();
    }

    [Theory]
    [InlineData("ORD01JQ4S4KY8HWQASX1AW5V4F3E9", RequestId, Ts)] // other order
    [InlineData(DataId, "another-request", Ts)]                  // other request id
    [InlineData(DataId, RequestId, "1742505638684")]             // replayed with another ts
    public void IsValid_AnyFieldChanged_ReturnsFalse(string dataId, string requestId, string ts)
    {
        Validator().IsValid($"ts={ts},v1={ExpectedV1}", requestId, dataId).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1=abc")]
    [InlineData("ts=123")]
    [InlineData("garbage")]
    public void IsValid_MalformedHeader_ReturnsFalse(string? header)
    {
        Validator().IsValid(header, RequestId, DataId).Should().BeFalse();
    }

    [Fact]
    public void IsValid_NoSecretConfigured_FailsClosed()
    {
        Validator(secret: null).IsValid($"ts={Ts},v1={ExpectedV1}", RequestId, DataId).Should().BeFalse();
    }

    [Fact]
    public void BuildManifest_FollowsDocumentedFormat()
    {
        MercadoPagoSignatureValidator.BuildManifest(DataId, RequestId, Ts)
            .Should().Be($"id:{DataId};request-id:{RequestId};ts:{Ts};");
    }
}
