using System.Text.Json;

using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

namespace Norbix.Hub.Tests;

/// <summary>
/// Email click ids (gateway docs/tasks/email-tracking-links.md): the project
/// switch that adds <c>?nblid=</c> to marketing links, and the route that
/// records the clicks a project's own site hands back. Where each field lands
/// on the wire, and how the per-click answer reads. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class EmailClickIdsTests
{
    private static readonly string[] Recipients = ["ann@example.com"];

    private static NorbixTestFixture Fixture() => NorbixTestFixture.Create(o =>
    {
        o.AccountId = "test-account";
        o.BearerToken = "test-bearer";
    });

    [TestCase(true)]
    [TestCase(false)]
    public async Task Switch_patches_click_tracking_and_always_sends_enabled(bool enabled)
    {
        using var fixture = Fixture();
        fixture.RespondNext(new { });

        await fixture.Client.Notifications.UpdateEmailClickTrackingAsync(
            new UpdateEmailClickTracking { Enabled = enabled });

        var sent = fixture.LastRequest!;
        Assert.That(sent.Method, Is.EqualTo("PATCH"));
        Assert.That(sent.Path, Does.EndWith("/notifications/email/settings/click-tracking"));
        Assert.That(sent.Headers["norbix-project-id"], Is.EqualTo("test-project"));

        // false must be on the wire too: a serializer that drops defaults
        // would leave the gateway nothing to switch off.
        var body = JsonSerializer.SerializeToElement(sent.Body);
        Assert.That(body.GetProperty("enabled").GetBoolean(), Is.EqualTo(enabled));
    }

    [Test]
    public async Task Record_posts_every_click_with_its_fields_in_camel_case()
    {
        using var fixture = Fixture();
        fixture.RespondNext(new { recorded = 1, duplicates = 0, flagged = 0, rejected = 0 });

        var clickedAt = new DateTime(2026, 11, 11, 9, 30, 15, DateTimeKind.Utc);
        await fixture.Client.Notifications.RecordEmailClicksAsync(new RecordEmailClicksRequest
        {
            Clicks =
            [
                new EmailClickDto
                {
                    Nblid = "nbl_full",
                    Url = "https://shop.example.com/sale?nblid=nbl_full",
                    ClickedAt = clickedAt,
                    UserAgent = "Mozilla/5.0",
                },
                new EmailClickDto { Nblid = "nbl_bare" },
            ],
        });

        var sent = fixture.LastRequest!;
        Assert.That(sent.Method, Is.EqualTo("POST"));
        Assert.That(sent.Path, Does.EndWith("/notifications/email/clicks"));

        var clicks = JsonSerializer.SerializeToElement(sent.Body).GetProperty("clicks");
        Assert.That(clicks.GetArrayLength(), Is.EqualTo(2));

        var full = clicks[0];
        Assert.That(full.GetProperty("nblid").GetString(), Is.EqualTo("nbl_full"));
        Assert.That(full.GetProperty("url").GetString(), Is.EqualTo("https://shop.example.com/sale?nblid=nbl_full"));
        Assert.That(full.GetProperty("userAgent").GetString(), Is.EqualTo("Mozilla/5.0"));
        // The time is part of the duplicate key (nblid + url + clickedAt), so it
        // must go out as the same UTC instant.
        Assert.That(full.GetProperty("clickedAt").GetDateTime().ToUniversalTime(), Is.EqualTo(clickedAt));

        var bare = clicks[1];
        Assert.That(bare.GetProperty("nblid").GetString(), Is.EqualTo("nbl_bare"));
        foreach (var optional in new[] { "url", "clickedAt", "userAgent" })
        {
            Assert.That(
                !bare.TryGetProperty(optional, out var v) || v.ValueKind == JsonValueKind.Null,
                $"unset {optional} must be absent or null");
        }
    }

    [Test]
    public async Task Record_reads_counts_and_each_click_result()
    {
        using var fixture = Fixture();
        fixture.RespondNext(new
        {
            recorded = 1,
            duplicates = 0,
            flagged = 1,
            rejected = 1,
            clicks = new object[]
            {
                new { index = 0, status = "rejected", reason = "not-valid" },
                new
                {
                    index = 1,
                    status = "recorded",
                    flag = "suspected-scanner",
                    flagReason = "user-agent",
                    campaignId = "cmp_1",
                    notificationId = "ntf_1",
                    linkIndex = 2,
                    to = Recipients,
                },
            },
        });

        var response = await fixture.Client.Notifications.RecordEmailClicksAsync(new RecordEmailClicksRequest
        {
            Clicks = [new EmailClickDto { Nblid = "bad" }, new EmailClickDto { Nblid = "nbl_ok" }],
        });

        Assert.That(response!.Recorded, Is.EqualTo(1));
        Assert.That(response.Duplicates, Is.EqualTo(0));
        Assert.That(response.Flagged, Is.EqualTo(1));
        Assert.That(response.Rejected, Is.EqualTo(1));
        Assert.That(response.Clicks, Has.Count.EqualTo(2));

        var rejected = response.Clicks![0];
        Assert.That(rejected.Index, Is.EqualTo(0));
        Assert.That(rejected.Status, Is.EqualTo("rejected"));
        Assert.That(rejected.Reason, Is.EqualTo("not-valid"));
        Assert.That(rejected.Flag, Is.Null);

        var flagged = response.Clicks[1];
        Assert.That(flagged.Status, Is.EqualTo("recorded"));
        Assert.That(flagged.Flag, Is.EqualTo("suspected-scanner"));
        Assert.That(flagged.FlagReason, Is.EqualTo("user-agent"));
        Assert.That(flagged.CampaignId, Is.EqualTo("cmp_1"));
        Assert.That(flagged.NotificationId, Is.EqualTo("ntf_1"));
        Assert.That(flagged.LinkIndex, Is.EqualTo(2));
        Assert.That(flagged.To, Is.EqualTo(Recipients));
    }
}
