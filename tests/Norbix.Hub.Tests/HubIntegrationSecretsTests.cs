using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Saved secrets of an integration of any module (<c>GET /{version}/integrations/{IntegrationId}/secrets</c>,
/// <c>POST /{version}/integrations/{IntegrationId}/secrets/remove</c>): which secrets are saved and which are
/// optional — never a value — and removing optional ones. A fake response only — no gateway is contacted.
/// </summary>
[TestFixture]
public sealed class HubIntegrationSecretsTests
{
    private const string IntegrationId = "int_abc";

    [Test]
    public async Task GetIntegrationSecrets_puts_the_id_in_the_path_and_reads_rows_without_values()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/integrations/int_abc/secrets", new
        {
            result = new[]
            {
                new { key = "secretKey", field = "secretKey", isSet = true, isOptional = false },
                new { key = "webhookSigningSecret", field = "webhookSigningSecret", isSet = false, isOptional = true },
            },
        });

        var response = await fixture.Client.Integrations.GetIntegrationSecretsAsync(
            new GetIntegrationSecrets { IntegrationId = IntegrationId, Env = "TEST" });

        await Verifier.Verify(new { Sent = fixture.LastRequest, Response = response }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task RemoveIntegrationSecrets_sends_the_keys_in_the_body()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/integrations/int_abc/secrets/remove", new { id = IntegrationId });

        var response = await fixture.Client.Integrations.RemoveIntegrationSecretsAsync(
            new RemoveIntegrationSecretsRequest { IntegrationId = IntegrationId, Keys = ["webhookSigningSecret"] });

        await Verifier.Verify(new { Sent = fixture.LastRequest, Response = response }, VerifyConfig.VerifySettings);
    }
}
