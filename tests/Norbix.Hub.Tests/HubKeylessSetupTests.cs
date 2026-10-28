using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The keyless cloud connection setup (<c>GET /{version}/integrations/keyless/setup</c>):
/// the values a developer pastes into the cloud account before saving a keyless
/// integration, and the setup templates (CloudFormation / Bicep / Terraform). A fake response only — no gateway and no cloud are contacted.
/// </summary>
[TestFixture]
public sealed class HubKeylessSetupTests
{
    private const string IntegrationId = "33333333-3333-3333-3333-333333333333";

    [Test]
    public async Task GetKeylessIntegrationSetup_sends_module_cloud_and_id_in_the_query()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/integrations/keyless/setup", new
        {
            integrationId = IntegrationId,
            env = "TEST",
            module = "files",
            cloud = "aws",
            issuer = "https://issuer.example",
            available = true,
            subject = $"p/test-project/e/TEST/files/i/{IntegrationId}",
            audience = "sts.amazonaws.com",
            script = "aws iam create-role ...",
            templates = new[]
            {
                new { kind = "terraform", fileName = "norbix-files-aws.tf", content = "resource \"aws_iam_role\" ..." },
            },
        });

        var response = await fixture.Client.Integrations.GetKeylessIntegrationSetupAsync(
            new GetKeylessIntegrationSetup { Module = "files", Cloud = "aws", IntegrationId = IntegrationId });

        await Verifier.Verify(new { Sent = fixture.LastRequest, Response = response }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetKeylessIntegrationSetup_returns_the_reason_when_keyless_is_unavailable()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/integrations/keyless/setup", new
        {
            integrationId = IntegrationId,
            module = "email",
            cloud = "azure",
            available = false,
            unavailableReason = "no signing key",
        });

        var response = await fixture.Client.Integrations.GetKeylessIntegrationSetupAsync(
            new GetKeylessIntegrationSetup { Module = "email", Cloud = "azure" });

        await Verifier.Verify(
            new { response?.Available, response?.UnavailableReason },
            VerifyConfig.VerifySettings);
    }
}
