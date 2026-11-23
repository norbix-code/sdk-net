using System.Net.Http.Headers;
using Norbix.Sdk.Transport;
using Norbix.Sdk.Types.Hub;

namespace Norbix.Sdk;

public sealed partial class NorbixHubClient
{
    private static readonly string[] CaseIdPathParam = { "CaseId" };

    /// <summary>
    /// Adds one file to a support case
    /// (POST /{version}/support/cases/{CaseId}/attachments, sent as
    /// <c>multipart/form-data</c> with one file part). At most 10 MB per file
    /// and 20 files per case; allowed: txt log md json xml yaml yml csv jpg jpeg
    /// png gif webp pdf doc docx xls xlsx ppt pptx odt ods zip. Answers the new
    /// attachment's id (<c>supf_…</c>). List them with
    /// <c>Support.GetSupportCaseAttachmentsAsync</c>, read one with
    /// <c>Support.DownloadSupportCaseAttachmentAsync</c>.
    /// </summary>
    /// <param name="caseId">The support case.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="fileName">The name shown on the case, with its extension (<c>app.log</c>).</param>
    /// <param name="contentType">For example <c>text/plain</c>; <c>application/octet-stream</c> when left out.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<IdResponse?> AddSupportCaseAttachmentAsync(
        string caseId,
        byte[] content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };

        return Transport.SendAsync<IdResponse>(
            new NorbixRequestSpec
            {
                Target = NorbixTarget.Hub,
                Path = "/{version}/support/cases/{CaseId}/attachments",
                Method = "POST",
                Request = new AddSupportCaseAttachmentRequest { CaseId = caseId },
                PathParams = CaseIdPathParam,
                Scope = NorbixScope.Project,
                Content = form,
            },
            cancellationToken);
    }
}
