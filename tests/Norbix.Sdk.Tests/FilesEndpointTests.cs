using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Public-API Files endpoints, one test per endpoint.
/// <para>
/// These are hand-written on purpose. The generated coverage snapshot
/// (<c>EndpointCoverageTests.Api.Files</c>) proves the whole group is wired up;
/// these tests prove the details a reader cares about — the file path travels
/// in the query string, the integration id lands in the URL, a download returns
/// the real bytes — and they fail with a readable message when one breaks.
/// </para>
/// <para>
/// Every test builds its own client and its own fake response, so the order the
/// tests run in does not matter, and no real storage provider is contacted.
/// </para>
/// </summary>
[TestFixture]
public sealed class FilesEndpointTests
{
    private const string IntegrationId = "11111111-1111-1111-1111-111111111111";
    private static readonly string[] GetFileStepErrors = { "Access denied" };

    [Test]
    public async Task ListFiles_sends_integration_id_and_path()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/" + IntegrationId, new { list = new { items = Array.Empty<object>() } });

        await fixture.Client.Files.ListFilesAsync(
            new ListFilesRequest { FilesIntegrationId = IntegrationId, Path = "invoices/" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetFileInfo_sends_integration_id_and_path()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/info", new { file = new { name = "invoice.pdf" } });

        await fixture.Client.Files.GetFileInfoAsync(
            new GetFileInfoRequest { FilesIntegrationId = IntegrationId, Path = "invoices/invoice.pdf" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetSignedUrl_sends_integration_id_and_path()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/sign", new { url = "https://storage.example/signed" });

        var response = await fixture.Client.Files.GetSignedUrlAsync(
            new GetSignedUrlRequest { FilesIntegrationId = IntegrationId, Path = "invoices/invoice.pdf" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, Url = response?.Url },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task RequestUploadUrl_posts_path_and_content_type()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/upload-url", new { url = "https://storage.example/put" });

        var response = await fixture.Client.Files.RequestUploadUrlAsync(
            new RequestUploadUrlRequest
            {
                FilesIntegrationId = IntegrationId,
                Path = "invoices/invoice.pdf",
                ContentType = "application/pdf",
            });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, Url = response?.Url },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task CommitUpload_posts_path_size_and_content_type()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/commit", new { responseStatus = new { isSuccess = true } });

        await fixture.Client.Files.CommitUploadAsync(
            new CommitUploadRequest
            {
                FilesIntegrationId = IntegrationId,
                Path = "invoices/invoice.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
            });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// Download streams the file itself, not a JSON envelope. Before this test
    /// existed the transport always parsed the body as JSON, so every download
    /// threw. See <c>HttpTransport</c>.
    /// </summary>
    [Test]
    public async Task Download_returns_the_raw_bytes()
    {
        var payload = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };  // "%PDF-"
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondBytes("/download", payload, "application/pdf");

        var bytes = await fixture.Client.Files.DownloadFileApiAsync(
            new DownloadFileApiRequest { FilesIntegrationId = IntegrationId, Path = "invoices/invoice.pdf" });

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Bytes = bytes,
                RoundTripped = bytes is not null && bytes.SequenceEqual(payload),
            },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task DeleteFile_sends_delete_with_path()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/" + IntegrationId, new { responseStatus = new { isSuccess = true } });

        await fixture.Client.Files.DeleteFileApiAsync(
            new DeleteFileApiRequest { FilesIntegrationId = IntegrationId, Path = "invoices/invoice.pdf" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task DeleteManyFiles_sends_every_path()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/bulk", new { responseStatus = new { isSuccess = true } });

        await fixture.Client.Files.DeleteManyFilesApiAsync(
            new DeleteManyFilesApiRequest
            {
                FilesIntegrationId = IntegrationId,
                Paths = new List<string> { "invoices/a.pdf", "invoices/b.pdf" },
            });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// The API-surface twin of the Hub's <c>TestFilesIntegrationAsync</c>
    /// (slice API-TEST, #39): the integration id goes in the URL, not in the
    /// body, and every probe step comes back parsed — a failed step included,
    /// because a failed step is an answer, not an error.
    /// </summary>
    [Test]
    public async Task TestFilesIntegration_posts_to_the_integration_route_and_parses_every_step()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/" + IntegrationId + "/test", new
        {
            items = new object[]
            {
                new { operation = "UploadFile", result = "OK" },
                new { operation = "GetFile", result = "FAILED", errors = GetFileStepErrors },
                new { operation = "GetAllFiles", result = "NOT_TESTED" },
                new { operation = "DeleteFile", result = "NOT_TESTED" },
            },
        });

        var response = await fixture.Client.Files.TestFilesIntegrationAsync(
            new TestFilesIntegrationRequest { FilesIntegrationId = IntegrationId });

        Assert.That(fixture.LastRequest!.Method, Is.EqualTo("POST"));
        Assert.That(fixture.LastRequest.Path, Is.EqualTo("/v2/files/" + IntegrationId + "/test"));
        Assert.That(response?.Items, Has.Count.EqualTo(4));
        Assert.That(response!.Items![1].Errors, Is.EqualTo(GetFileStepErrors));

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Results = response.Items.Select(i => new { i.Operation, i.Result, i.Errors }),
            },
            VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// A request the gateway refuses (here: the API key lacks
    /// <c>files:create</c>) surfaces as the SDK's usual <see cref="NorbixException"/>.
    /// </summary>
    [Test]
    public void TestFilesIntegration_throws_when_the_gateway_refuses()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/" + IntegrationId + "/test", new
        {
            responseStatus = new
            {
                errorCode = "Forbidden",
                message = "Missing permission files:create",
            },
        }, System.Net.HttpStatusCode.Forbidden);

        var thrown = Assert.ThrowsAsync<NorbixException>(async () =>
            await fixture.Client.Files.TestFilesIntegrationAsync(
                new TestFilesIntegrationRequest { FilesIntegrationId = IntegrationId }));

        Assert.That(thrown!.StatusCode, Is.EqualTo(403));
        Assert.That(thrown.Code, Is.EqualTo("Forbidden"));
        Assert.That(thrown.Message, Does.Contain("files:create"));
    }
}
