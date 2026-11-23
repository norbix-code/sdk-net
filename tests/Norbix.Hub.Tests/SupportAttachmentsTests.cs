using Norbix.Sdk.Tests;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Support case attachments: adding a file goes out as one multipart file part
/// (not JSON), listing / deleting are plain calls, and the download hands back
/// the file's bytes. Also the payments test payment, the other new route of
/// review batch 3.
/// </summary>
[TestFixture]
public sealed class SupportAttachmentsTests
{
    private const string CaseId = "case_1";

    [Test]
    public async Task AddSupportCaseAttachment_sends_one_multipart_file_part()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/attachments", new { id = "supf_1" });

        var response = await fixture.Client.AddSupportCaseAttachmentAsync(
            CaseId, "line one"u8.ToArray(), "app.log", "text/plain");

        var sent = fixture.LastRequest!;
        var raw = sent.Body as string ?? string.Empty;
        await Verifier.Verify(
            new
            {
                sent.Method,
                sent.Path,
                MediaType = sent.Headers["Content-Type"].Split(';')[0],
                PartIsNamedFile = raw.Contains("name=file", StringComparison.Ordinal),
                PartHasFileName = raw.Contains("filename=app.log", StringComparison.Ordinal),
                PartHasContentType = raw.Contains("Content-Type: text/plain", StringComparison.Ordinal),
                PartHasBytes = raw.Contains("line one", StringComparison.Ordinal),
                response?.Id,
            },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetSupportCaseAttachments_lists_the_files_of_a_case()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/attachments", new
        {
            result = new[]
            {
                new { id = "supf_1", caseId = CaseId, fileName = "app.log", sizeBytes = 8, contentType = "text/plain", addedByYou = true, canDelete = true },
            },
        });

        var response = await fixture.Client.Support.GetSupportCaseAttachmentsAsync(
            new GetSupportCaseAttachmentsRequest { CaseId = CaseId });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, Files = response?.Result?.Select(f => new { f.Id, f.FileName, f.SizeBytes, f.CanDelete }) },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task DownloadSupportCaseAttachment_returns_the_bytes()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondBytes("/download", new byte[] { 1, 2, 3 });

        var bytes = await fixture.Client.Support.DownloadSupportCaseAttachmentAsync(
            new DownloadSupportCaseAttachmentRequest { CaseId = CaseId, AttachmentId = "supf_1" });

        await Verifier.Verify(new { Sent = fixture.LastRequest, Bytes = bytes }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task DeleteSupportCaseAttachment_calls_the_delete_route()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/attachments/supf_1", new { id = "supf_1" });

        var response = await fixture.Client.Support.DeleteSupportCaseAttachmentAsync(
            new DeleteSupportCaseAttachmentRequest { CaseId = CaseId, AttachmentId = "supf_1" });

        await Verifier.Verify(new { Sent = fixture.LastRequest, response?.Id }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task StartPaymentsIntegrationTestPayment_sends_integration_and_return_url()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/test-payment", new { payUrl = "https://pay.example.test/1", reference = "ref-1", amountMinor = 1, currency = "EUR" });

        var response = await fixture.Client.Payments.StartPaymentsIntegrationTestPaymentAsync(
            new StartPaymentsIntegrationTestPaymentRequest { IntegrationId = "int_neopay", ReturnUrl = "https://app.example.test/back" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response?.PayUrl, response?.Reference, response?.AmountMinor, response?.Currency },
            VerifyConfig.VerifySettings);
    }
}
