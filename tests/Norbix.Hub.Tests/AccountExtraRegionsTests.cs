using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Extra regions of the Managed Service subscription (gateway
/// Account/Stripe/SetExtraRegions.cs): POST /{version}/account/stripe/extra-regions.
/// The snapshot holds the verb, the exact Hub path, the headers and the body;
/// an <c>ExtraRegions</c> of 0 (remove them all) is sent, not dropped. The
/// gateway reads the account from the session, so the client needs only a
/// token. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class AccountExtraRegionsTests
{
    [Test]
    public async Task Extra_regions_posts_the_count_and_sends_zero_to_remove_them()
    {
        using var fixture = NorbixTestFixture.Create(o => o.BearerToken = "test-bearer");
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.SetExtraRegionsAsync(new SetExtraRegions { ExtraRegions = 0 });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }
}
