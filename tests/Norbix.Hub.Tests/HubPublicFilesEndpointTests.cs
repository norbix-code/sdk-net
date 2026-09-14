using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The four "make it public / make it private" endpoints slice PUB added to
/// the Hub (10b-files, slice SDK-2).
/// <para>
/// Each test builds its own client and its own fake response, so the order the
/// tests run in does not matter and no real storage provider is contacted.
/// </para>
/// </summary>
[TestFixture]
public sealed class HubPublicFilesEndpointTests
{
    private const string IntegrationId = "22222222-2222-2222-2222-222222222222";
    private const string FilePath = "invoices/invoice.pdf";
    private const string PublicId = "nbpf_7hK2abc";
    private static readonly string[] TwoFolders = { "invoices", "drafts" };

    [Test]
    public async Task MakeFilePublic_posts_to_the_item_public_route()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/item/public", new { id = PublicId, status = "Success" });

        var response = await fixture.Client.Files.MakeFilePublicAsync(
            new MakeFilePublicRequest { FilesIntegrationId = IntegrationId, Path = FilePath });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, PublicId = response?.Id },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task MakeFilePrivate_posts_to_the_item_private_route()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Files.MakeFilePrivateAsync(
            new MakeFilePrivateRequest { FilesIntegrationId = IntegrationId, Path = FilePath });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task MakeFolderPublic_posts_to_the_folder_public_route()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/folder/public", new { id = "nbpf_folder1", status = "Success" });

        var response = await fixture.Client.Files.MakeFolderPublicAsync(
            new MakeFolderPublicRequest { FilesIntegrationId = IntegrationId, Path = "invoices" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, PublicId = response?.Id },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task MakeFolderPrivate_posts_to_the_folder_private_route()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Files.MakeFolderPrivateAsync(
            new MakeFolderPrivateRequest { FilesIntegrationId = IntegrationId, Path = "invoices" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// The read side now answers whether a file is public, and which folders in
    /// a listing are. The plain <c>Folders</c> list is untouched, so a client
    /// that does not know about public folders keeps working.
    /// </summary>
    [Test]
    public async Task GetFolderFiles_carries_the_new_public_fields()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/files/folder",
            new
            {
                list = new
                {
                    items = new[]
                    {
                        new
                        {
                            path = FilePath,
                            isPublic = true,
                            publicUrl = "https://api.norbix.dev/v3/files/public/" + PublicId + "/invoice.pdf",
                        },
                    },
                },
                folders = TwoFolders,
                publicFolders = new[]
                {
                    new
                    {
                        path = "invoices",
                        publicId = "nbpf_folder1",
                        publicUrl = "https://api.norbix.dev/v3/files/public/nbpf_folder1/",
                        inherited = false,
                    },
                },
            });

        var response = await fixture.Client.Files.GetFolderFilesAsync(
            new GetFolderFiles { FilesIntegrationId = IntegrationId, Path = "invoices" });

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

    /// <summary>
    /// A file that nobody published reports <c>IsPublic: false</c> and no URL.
    /// That is the honest default, and it is what an older gateway (one that
    /// does not send the fields at all) also produces.
    /// </summary>
    [Test]
    public async Task A_file_nobody_published_is_not_public()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/files/item", new { file = new { path = FilePath } });

        var response = await fixture.Client.Files.GetFileAsync(
            new GetFile { FilesIntegrationId = IntegrationId, Path = FilePath });

        await Verifier.Verify(
            new { response?.File?.IsPublic, response?.File?.PublicUrl },
            VerifyConfig.VerifySettings);
    }
}
