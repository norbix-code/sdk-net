using Norbix.Sdk.Tests;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Saving a Montonio payment integration: the request is declared as the base
/// <see cref="PaymentIntegrationRequest"/>, so this proves the Montonio fields
/// (<c>accessKey</c>, <c>secretKey</c>, <c>isSandbox</c>, <c>locale</c>) and the
/// <c>provider</c> the server picks the shape by leave the client.
/// </summary>
[TestFixture]
public sealed class PaymentsMontonioIntegrationTests
{
    [Test]
    public async Task SavePaymentsIntegration_sends_the_Montonio_shape()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/payments/integrations", new { id = "int_montonio" });

        var response = await fixture.Client.Payments.SavePaymentsIntegrationAsync(new SavePaymentsIntegration
        {
            Integration = new MontonioPaymentIntegrationRequest
            {
                Provider = PaymentGatewayPlatform.Montonio,
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                IsSandbox = true,
                Locale = "lt",
            },
        });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response?.Id },
            VerifyConfig.VerifySettings);
    }
}
