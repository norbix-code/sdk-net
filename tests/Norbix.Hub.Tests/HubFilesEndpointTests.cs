using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The Files endpoints of the Hub (the dashboard-facing API): turning the
/// module on and off, browsing folders and files, and managing storage
/// integrations.
/// <para>
/// Each test builds its own client and its own fake response, so the order the
/// tests run in does not matter and no real storage provider is contacted.
/// </para>
/// </summary>
[TestFixture]
public sealed class HubFilesEndpointTests
{
    private const string IntegrationId = "22222222-2222-2222-2222-222222222222";
    private static readonly string[] OneFolder = { "invoices/" };

    [Test]
    public async Task EnableFiles_calls_the_enable_route()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Files.EnableFilesAsync(new EnableFiles());

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task DisableFiles_calls_the_disable_route()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Files.DisableFilesAsync(new DisableFiles());

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetFolderFiles_sends_integration_id_and_path()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/folder", new { folders = OneFolder });

        var response = await fixture.Client.Files.GetFolderFilesAsync(
            new GetFolderFiles { FilesIntegrationId = IntegrationId, Path = "invoices/" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, Folders = response?.Folders },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetFile_sends_integration_id_and_path()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/item", new { file = new { path = "invoices/invoice.pdf" }, isPublic = false });

        var response = await fixture.Client.Files.GetFileAsync(
            new GetFile { FilesIntegrationId = IntegrationId, Path = "invoices/invoice.pdf" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, FilePath = response?.File?.Path, response?.IsPublic },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetFilesIntegrations_lists_integrations()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/integrations", new { defaultIntegrationId = IntegrationId });

        var response = await fixture.Client.Files.GetFilesIntegrationsAsync(new GetFilesIntegrations());

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response?.DefaultIntegrationId },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetFilesIntegration_sends_the_id_in_the_url()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/integrations/" + IntegrationId, new { item = new { } });

        await fixture.Client.Files.GetFilesIntegrationAsync(
            new GetFilesIntegration { Id = IntegrationId });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task SaveFilesIntegration_posts_the_provider_settings()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/integrations", new { id = IntegrationId });

        await fixture.Client.Files.SaveFilesIntegrationAsync(
            new SaveFilesIntegration
            {
                Integration = new AwsS3FilesIntegrationRequest
                {
                    IntegrationName = "Invoices bucket",
                    IsEnabled = true,
                    BucketName = "norbix-invoices",
                    Region = "eu-central-1",
                },
            });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task DeleteFilesIntegration_sends_the_id_in_the_url()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Files.DeleteFilesIntegrationAsync(
            new DeleteFilesIntegrationRequest { Id = IntegrationId });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task EnableFilesIntegration_sends_the_id_in_the_url()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Files.EnableFilesIntegrationAsync(
            new EnableFilesIntegrationRequest { Id = IntegrationId });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task DisableFilesIntegration_sends_the_id_in_the_url()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Files.DisableFilesIntegrationAsync(
            new DisableFilesIntegrationRequest { Id = IntegrationId });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task SetFilesIntegrationAsDefault_sends_the_id_in_the_url()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Files.SetFilesIntegrationAsDefaultAsync(
            new SetFilesIntegrationAsDefaultRequest { Id = IntegrationId });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task TestFilesIntegration_posts_the_integration_id()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/integrations/test", new
        {
            items = new[] { new { operation = "ListFiles", result = "Ok" } },
        });

        var response = await fixture.Client.Files.TestFilesIntegrationAsync(
            new TestFilesIntegration { IntegrationId = IntegrationId });

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Results = response?.Items?.Select(i => new { i.Operation, i.Result }),
            },
            VerifyConfig.VerifySettings);
    }
}
