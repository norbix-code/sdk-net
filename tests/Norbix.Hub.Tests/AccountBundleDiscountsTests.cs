using System.Net;

using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Bundle discount codes (gateway refactoringV2 9f73edb5b, task file
/// docs/tasks/bundle-discounts.md):
/// POST /{version}/account/stripe/discount/check (reads only) and
/// POST /{version}/account/stripe/discount (puts the code on the running
/// bundle subscription); <c>CreateStripeCheckoutSession</c> takes a
/// <c>DiscountCode</c>.
///
/// A refused code is HTTP 400 <c>CM-ERRORS-ACCOUNT-027</c> with the reason
/// (<c>not_found</c>, <c>expired</c>, <c>already_used</c>, …) in the error
/// context. The gateway reads the account from the session, so the client
/// needs only a token — no <c>AccountId</c>. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class AccountBundleDiscountsTests
{
    private static NorbixTestFixture TokenOnlyClient() =>
        NorbixTestFixture.Create(o => o.BearerToken = "test-bearer");

    [Test]
    public async Task Check_posts_the_code_bundle_and_billing_period_and_reads_the_discount()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(new
        {
            discount = new
            {
                code = "LAUNCH30",
                percentOff = 30m,
                currency = "usd",
                months = 3,
                endsAt = "2027-02-18T00:00:00Z",
            },
        });

        var response = await fixture.Client.Account.CheckBundleDiscountAsync(new CheckBundleDiscount
        {
            Code = "LAUNCH30",
            Bundle = ManagedServiceBundle.Growth,
            BillingPeriod = SubscriptionBillingPeriod.Monthly,
        });

        await Verifier.Verify(new { Sent = fixture.LastRequest, response!.Discount }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Apply_posts_only_the_code()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.ApplyBundleDiscountAsync(new ApplyBundleDiscount { Code = "LAUNCH30" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Checkout_sends_the_discount_code_with_the_bundle()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(new { url = "https://checkout.stripe.test/session" });

        await fixture.Client.Account.CreateStripeCheckoutSessionAsync(new CreateStripeCheckoutSession
        {
            SubscriptionType = SubscriptionType.ManagedService,
            Bundle = ManagedServiceBundle.Starter,
            BillingPeriod = SubscriptionBillingPeriod.Annual,
            DiscountCode = "LAUNCH30",
        });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public void A_refused_code_surfaces_the_error_code_and_the_reason()
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
                            message = "This discount code cannot be used.",
                            errorCode = "CM-ERRORS-ACCOUNT-027",
                            context = new { reason = "already_used" },
                        },
                    },
                },
            },
            HttpStatusCode.BadRequest);

        var ex = Assert.ThrowsAsync<NorbixException>(async () =>
            await fixture.Client.Account.ApplyBundleDiscountAsync(new ApplyBundleDiscount { Code = "LAUNCH30" }))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.HttpStatus, Is.EqualTo(400));
            Assert.That(ex.ErrorCode, Is.EqualTo("CM-ERRORS-ACCOUNT-027"));
            Assert.That(ex.Message, Is.EqualTo("This discount code cannot be used."));
            Assert.That(ex.Errors, Has.Count.EqualTo(1));
            Assert.That(ex.Errors[0].Context, Is.Not.Null);
            Assert.That(ex.Errors[0].Context!["reason"], Is.EqualTo("already_used"));
        });
    }
}
