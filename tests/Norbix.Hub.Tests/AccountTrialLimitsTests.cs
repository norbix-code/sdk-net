using System.Net;

using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Trial limits (gateway refactoringV2, trial limits 49bfc3864):
/// GET /{version}/account/trial-availability (can a free Managed Service
/// trial start in a region now) and POST /{version}/account/trial-waitlist
/// (put the account owner's email on the region's waiting list);
/// <c>CreateStripeCheckoutSession</c> takes <c>Region</c>,
/// <c>WithoutTrial</c> and <c>TrialInvite</c>.
///
/// A trial checkout in a full region is HTTP 400
/// <c>CM-ERRORS-ACCOUNT-040</c>. The intake route
/// (/account/trial-waitlist/intake) is website-server only and has no SDK
/// method. The gateway reads the account from the session, so the client
/// needs only a token — no <c>AccountId</c>. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class AccountTrialLimitsTests
{
    private static NorbixTestFixture TokenOnlyClient() =>
        NorbixTestFixture.Create(o => o.BearerToken = "test-bearer");

    [Test]
    public async Task Availability_sends_the_region_in_the_query_and_reads_a_full_region()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(new
        {
            limitsOn = true,
            region = "nb-eu-germany",
            trialsOpen = false,
            waitlistOpen = true,
            onWaitlist = false,
            refusal = "",
        });

        var response = await fixture.Client.Account.GetTrialAvailabilityAsync(new GetTrialAvailabilityRequest
        {
            Region = "nb-eu-germany",
        });

        Assert.Multiple(() =>
        {
            Assert.That(response!.TrialsOpen, Is.False);
            Assert.That(response.WaitlistOpen, Is.True);
        });
        await Verifier.Verify(new { Sent = fixture.LastRequest, response }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Join_waitlist_posts_region_consent_and_consent_version()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(new { status = "added", message = "You are on the waiting list." });

        var response = await fixture.Client.Account.JoinTrialWaitlistAsync(new JoinTrialWaitlistRequest
        {
            Region = "nb-eu-germany",
            Consent = true,
            ConsentVersion = "2026-11",
        });

        Assert.That(response!.Status, Is.EqualTo("added"));
        await Verifier.Verify(new { Sent = fixture.LastRequest, response }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Checkout_sends_region_without_trial_and_trial_invite()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(new { url = "https://checkout.stripe.test/session" });

        await fixture.Client.Account.CreateStripeCheckoutSessionAsync(new CreateStripeCheckoutSession
        {
            SubscriptionType = SubscriptionType.ManagedService,
            Bundle = ManagedServiceBundle.Starter,
            BillingPeriod = SubscriptionBillingPeriod.Monthly,
            Region = "nb-eu-germany",
            WithoutTrial = false,
            TrialInvite = "invite-token",
        });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public void A_trial_checkout_in_a_full_region_surfaces_the_error_code()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(
            new
            {
                responseStatus = new
                {
                    isSuccess = false,
                    errors = new object[]
                    {
                        new
                        {
                            message = "Free trials in this region are full.",
                            errorCode = "CM-ERRORS-ACCOUNT-040",
                        },
                    },
                },
            },
            HttpStatusCode.BadRequest);

        var ex = Assert.ThrowsAsync<NorbixException>(async () =>
            await fixture.Client.Account.CreateStripeCheckoutSessionAsync(new CreateStripeCheckoutSession
            {
                SubscriptionType = SubscriptionType.ManagedService,
                Bundle = ManagedServiceBundle.Starter,
                BillingPeriod = SubscriptionBillingPeriod.Monthly,
                Region = "nb-eu-germany",
            }))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.HttpStatus, Is.EqualTo(400));
            Assert.That(ex.ErrorCode, Is.EqualTo("CM-ERRORS-ACCOUNT-040"));
            Assert.That(ex.Message, Is.EqualTo("Free trials in this region are full."));
        });
    }
}
