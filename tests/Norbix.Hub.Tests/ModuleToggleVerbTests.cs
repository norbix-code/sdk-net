using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Every module on/off switch (Database, Files, Email, SMS, Push, Payments,
/// Logs, Membership, Code) changes project state, so the gateway serves it as
/// <c>PUT</c> since refactoringV2 (it was <c>GET</c> before). This test calls
/// all 18 generated methods against an in-memory handler and snapshots the
/// verb and path that actually left the client, so a regeneration that brings
/// <c>GET</c> back fails here in one place.
/// </summary>
[TestFixture]
public sealed class ModuleToggleVerbTests
{
    [Test]
    public async Task Every_module_enable_and_disable_is_sent_as_put()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();
        var client = fixture.Client;

        await client.Database.EnableDatabaseAsync(new EnableDatabase());
        await client.Database.DisableDatabaseAsync(new DisableDatabase());
        await client.Files.EnableFilesAsync(new EnableFiles());
        await client.Files.DisableFilesAsync(new DisableFiles());
        await client.Notifications.EnableEmailAsync(new EnableEmail());
        await client.Notifications.DisableEmailAsync(new DisableEmail());
        await client.Notifications.EnableSmsAsync(new EnableSms());
        await client.Notifications.DisableSmsAsync(new DisableSms());
        await client.Notifications.EnablePushAsync(new EnablePush());
        await client.Notifications.DisablePushAsync(new DisablePush());
        await client.Payments.EnablePaymentsAsync(new EnablePayments());
        await client.Payments.DisablePaymentsAsync(new DisablePayments());
        await client.Logs.EnableLoggingAsync(new EnableLogging());
        await client.Logs.DisableLoggingAsync(new DisableLogging());
        await client.Membership.EnableMembershipAsync(new EnableMembership());
        await client.Membership.DisableMembershipAsync(new DisableMembership());
        await client.Code.EnableCodeAsync(new EnableCode());
        await client.Code.DisableCodeAsync(new DisableCode());

        var sent = fixture
            .RecordedRequests.Select(r => $"{r.Method} {r.Path}")
            .ToList();

        await Verifier.Verify(new { Count = sent.Count, Sent = sent }, VerifyConfig.VerifySettings);
    }
}
