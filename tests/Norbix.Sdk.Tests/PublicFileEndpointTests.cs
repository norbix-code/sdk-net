using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The public file link — <c>GET /{version}/files/public/{publicId}/{name}</c>
/// (10b-files slice PUB, brought into the SDK by slice SDK-2).
/// <para>
/// The one thing these tests exist to prove is that the call goes out with
/// <b>no</b> <c>Authorization</c> header, even when the client is signed in.
/// That is what public means: the link has to work in an e-mail or in a
/// browser on a stranger's phone, and attaching the caller's credentials to it
/// would be worse than useless.
/// </para>
/// <para>
/// Every test builds its own client and its own fake response, so the order
/// the tests run in does not matter and no real storage provider is contacted.
/// </para>
/// </summary>
[TestFixture]
public sealed class PublicFileEndpointTests
{
    private const string PublicId = "nbpf_7hK2abc";
    private static readonly byte[] Pdf = "%PDF-1.7 hello"u8.ToArray();

    [Test]
    public async Task GetPublicFile_sends_no_authorization_header()
    {
        // An API key IS configured, and must still not be sent.
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondBytes("/invoice.pdf", Pdf, "application/pdf");

        await fixture.Client.Files.GetPublicFileAsync(
            new GetPublicFileRequest { PublicId = PublicId, Name = "invoice.pdf" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetPublicFile_returns_the_raw_bytes()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondBytes("/invoice.pdf", Pdf, "application/pdf");

        var bytes = await fixture.Client.Files.GetPublicFileAsync(
            new GetPublicFileRequest { PublicId = PublicId, Name = "invoice.pdf" });

        await Verifier.Verify(
            new { Length = bytes?.Length, RoundTripped = bytes is not null && bytes.SequenceEqual(Pdf) },
            VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// A folder link puts the path inside the folder after the id, and those
    /// slashes are real separators. If they were escaped the route would stop
    /// matching and every folder link would 404.
    /// </summary>
    [Test]
    public async Task A_folder_relative_path_keeps_its_slashes()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondBytes("/report.pdf", Pdf, "application/pdf");

        await fixture.Client.Files.GetPublicFileAsync(
            new GetPublicFileRequest { PublicId = "nbpf_folder1", Name = "2026/q1/report.pdf" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// Somebody who holds nothing but a link has no API key at all. Every other
    /// endpoint refuses that with NORBIX_NOT_AUTHENTICATED; this one must not.
    /// </summary>
    [Test]
    public async Task It_works_with_no_credentials_at_all()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondBytes("/invoice.pdf", Pdf, "application/pdf");

        var bytes = await fixture.Client.Files.GetPublicFileAsync(
            new GetPublicFileRequest { PublicId = PublicId, Name = "invoice.pdf" });

        Assert.That(bytes, Is.Not.Null);
        Assert.That(bytes, Is.EqualTo(Pdf));
    }

    /// <summary>
    /// The comparison that makes the test above mean something: the same
    /// client, calling a project-scoped Files endpoint, is refused.
    /// </summary>
    [Test]
    public void Another_files_endpoint_still_refuses_a_client_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);

        var thrown = Assert.ThrowsAsync<NorbixException>(async () =>
            await fixture.Client.Files.ListFilesAsync(
                new ListFilesRequest { FilesIntegrationId = "11111111-1111-1111-1111-111111111111" }));

        Assert.That(thrown!.Code, Is.EqualTo(NorbixErrorCodes.NotAuthenticated));
    }

    /// <summary>
    /// Every miss is the same plain 404 — unknown id, wrong name, made private
    /// again, gone from storage. Saying anything more precise would tell a
    /// stranger that the file exists.
    /// </summary>
    [Test]
    public void A_link_that_points_at_nothing_throws_a_404()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/invoice.pdf", new { message = "This link does not point to a file." },
            System.Net.HttpStatusCode.NotFound);

        var thrown = Assert.ThrowsAsync<NorbixException>(async () =>
            await fixture.Client.Files.GetPublicFileAsync(
                new GetPublicFileRequest { PublicId = "nbpf_gone", Name = "invoice.pdf" }));

        Assert.That(thrown!.StatusCode, Is.EqualTo(404));
    }

    /// <summary>The two fields slice PUB added to every file a listing returns.</summary>
    [Test]
    public async Task ListFiles_carries_the_new_public_fields()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/files/11111111-1111-1111-1111-111111111111",
            new
            {
                list = new
                {
                    items = new[]
                    {
                        new
                        {
                            path = "invoices/invoice.pdf",
                            isPublic = true,
                            publicUrl = "https://api.norbix.dev/v3/files/public/" + PublicId + "/invoice.pdf",
                        },
                    },
                },
                folders = TwoFolders,
                publicFolders = new[]
                {
                    new { path = "invoices", publicId = "nbpf_folder1", inherited = false },
                },
            });

        var response = await fixture.Client.Files.ListFilesAsync(
            new ListFilesRequest { FilesIntegrationId = "11111111-1111-1111-1111-111111111111" });

        await Verifier.Verify(
            new
            {
                FirstFileIsPublic = response?.List?.Items?[0].IsPublic,
                FirstFilePublicUrl = response?.List?.Items?[0].PublicUrl,
                PlainFolders = response?.Folders,
                PublicFolders = response?.PublicFolders,
            },
            VerifyConfig.VerifySettings);
    }

    private static readonly string[] TwoFolders = { "invoices", "drafts" };
}
