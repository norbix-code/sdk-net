using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Cloning an email, SMS or push template answers the copy's id
/// (<see cref="IdResponse"/>, gateway 2026-10-23) — the source template stays
/// as it is and the copy gets a new id. Each test sends the clone, gets the
/// gateway's <c>{ "id": "…" }</c> answer back from the fake transport, and
/// the snapshot holds both the request and the id the caller read. Nothing
/// leaves the process.
/// </summary>
[TestFixture]
public sealed class TemplateCloneTests
{
    private const string SourceId = "tpl_source_1";
    private const string CopyId = "tpl_copy_1";

    [Test]
    public async Task CloneEmailTemplate_returns_the_copy_id()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/notifications/email/templates/" + SourceId + "/clone", new { id = CopyId });

        var response = await fixture.Client.Notifications.CloneEmailTemplateAsync(
            new CloneEmailTemplateRequest { Id = SourceId });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, CopyId = response?.Id },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task CloneSmsTemplate_returns_the_copy_id()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/notifications/sms/templates/" + SourceId + "/clone", new { id = CopyId });

        var response = await fixture.Client.Notifications.CloneSmsTemplateAsync(
            new CloneSmsTemplateRequest { Id = SourceId });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, CopyId = response?.Id },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task ClonePushTemplate_returns_the_copy_id()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/notifications/push/templates/" + SourceId + "/clone", new { id = CopyId });

        var response = await fixture.Client.Notifications.ClonePushTemplateAsync(
            new ClonePushTemplateRequest { Id = SourceId });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, CopyId = response?.Id },
            VerifyConfig.VerifySettings);
    }
}
