using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Public-API payment orders and checkout, one test per endpoint.
/// <para>
/// Hand-written on purpose, like <see cref="FilesEndpointTests"/>: the coverage
/// snapshot (<c>EndpointCoverageTests.Api.Payments</c>) proves the group is
/// wired up; these prove what a reader cares about — the order id lands in the
/// URL, the lines and the idempotency key travel in the body, the list filters
/// travel in the query string, and the stored order comes back typed.
/// </para>
/// </summary>
[TestFixture]
public sealed class PaymentOrderEndpointTests
{
    private const string OrderId = "ord_2ToIiRM03X63TZaJ2dJdv2";

    private static readonly object StoredOrder = new
    {
        id = OrderId,
        env = "PROD",
        buyerUserId = "usr_1szWVIyZFBV8HfvC4SfVw",
        merchantReference = "ad-1042-promo-1",
        currency = "EUR",
        priceVersion = "v1",
        lines = new[] { new { sku = "star-day", title = "Star × day", quantity = 21, unitAmountMinor = 30, amountMinor = 630 } },
        totalMinor = 630,
        metadata = new Dictionary<string, string> { ["adId"] = "ad_1042" },
        status = "Created",
        createdOnUtc = "2026-10-19T08:00:00Z",
        updatedOnUtc = "2026-10-19T08:00:00Z",
        checkouts = Array.Empty<object>(),
        refundedMinor = 0,
        mismatches = Array.Empty<string>(),
    };

    [Test]
    public async Task CreatePaymentOrder_posts_the_lines_and_the_idempotency_key()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/payments/orders", new { id = OrderId });

        var response = await fixture.Client.Payments.CreatePaymentOrderAsync(new CreatePaymentOrderRequest
        {
            BuyerUserId = "usr_1szWVIyZFBV8HfvC4SfVw",
            MerchantReference = "ad-1042-promo-1",
            IdempotencyKey = "ad-1042-promo-1-key",
            Currency = "EUR",
            PriceVersion = "v1",
            Lines = [new PaymentOrderLineInput { Sku = "star-day", Title = "Star × day", Quantity = 21, UnitAmountMinor = 30 }],
            Metadata = new Dictionary<string, string> { ["adId"] = "ad_1042" },
        });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response?.Id },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetPaymentOrder_puts_the_id_in_the_path_and_reads_the_order()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/payments/orders/" + OrderId, new { item = StoredOrder });

        var response = await fixture.Client.Payments.GetPaymentOrderAsync(new GetPaymentOrderRequest { Id = OrderId });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, Order = response?.Item },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetPaymentOrders_sends_the_filters_in_the_query()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/payments/orders", new { list = new { items = new[] { StoredOrder }, hasMore = false } });

        var response = await fixture.Client.Payments.GetPaymentOrdersAsync(new GetPaymentOrdersRequest
        {
            BuyerUserId = "usr_1szWVIyZFBV8HfvC4SfVw",
            MerchantReference = "ad-1042-promo-1",
            Status = "Paid",
        });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, Ids = response?.List?.Items.Select(o => o.Id) },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task StartPaymentCheckout_posts_to_the_order_and_reads_the_checkout()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/payments/orders/" + OrderId + "/checkout", new
        {
            checkout = new
            {
                attempt = 1,
                integrationId = "int_montonio",
                provider = "Montonio",
                providerOrderId = "mnt-1",
                providerReference = OrderId + ":1",
                paymentUrl = "https://sandbox-stargate.montonio.com/pay/mnt-1",
                method = "paymentInitiation",
                startedOnUtc = "2026-10-19T08:01:00Z",
                expiresAtUtc = "2026-10-19T08:31:00Z",
            },
            reused = false,
            order = StoredOrder,
        });

        var response = await fixture.Client.Payments.StartPaymentCheckoutAsync(new StartPaymentCheckoutRequest
        {
            Id = OrderId,
            IntegrationId = "int_montonio",
            Method = "paymentInitiation",
            ReturnUrl = "https://shop.example.test/orders/ad-1042",
            PreferredCountry = "LT",
            PreferredProvider = "SWEDBANK_LT",
        });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response?.Checkout, response?.Reused, OrderStatus = response?.Order?.Status },
            VerifyConfig.VerifySettings);
    }
}
