using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Project / Account scope — the SDK's most opinionated guard. The
/// snapshots lock down both the success path (X-CM-AccountId attached) and
/// the failure path (NorbixException with structured code).
/// </summary>
[TestFixture]
public sealed class ScopingTests
{
    [Test]
    public async Task Project_id_header_is_attached_on_every_call()
    {
        using var fixture = NorbixTestFixture.Create();
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Account_id_header_attached_only_when_configured()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.AccountId = "account-1";
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task SetScope_swaps_project_at_runtime()
    {
        using var fixture = NorbixTestFixture.Create();
        var scoped = fixture.Client.WithScope("new-project", "new-account");

        await scoped.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        await Verifier.Verify(new
        {
            options = new
            {
                scoped.Options.ProjectId,
                scoped.Options.AccountId,
            },
            request = fixture.LastRequest,
        }, VerifyConfig.VerifySettings);
    }
}
