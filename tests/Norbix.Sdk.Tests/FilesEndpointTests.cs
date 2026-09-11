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
}
