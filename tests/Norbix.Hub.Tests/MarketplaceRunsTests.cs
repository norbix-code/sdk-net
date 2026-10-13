using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// GET /{version}/code/marketplace/functions/runs takes exactly one of
/// functionId / triggerId / taskId. The coverage snapshot fills every field at
/// once; these cases pin a function's own runs (functionId alone, on the query,
/// no body) and the details helper over GET /{version}/logs/audit.
/// Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class MarketplaceRunsTests
{
    private const string CorrelationId = "0f8e2c9a4b7d4e1f9a3c5b6d7e8f9012";

    private static NorbixTestFixture NewFixture() =>
        NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });

    [Test]
    public async Task Function_runs_send_functionId_and_limit_on_the_query_with_no_body()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(new
        {
            runs = new[]
            {
                new
                {
                    functionId = "func_1p4R3gTobGZpD6fXvHT4rd",
                    source = "api-key",
                    @event = "invoke",
                    succeeded = true,
                    status = "ok",
                    correlationId = CorrelationId,
                    hasDetails = true,
                },
            },
        });

        var response = await fixture.Client.Code.GetMarketplaceFunctionRunsAsync(
            new GetMarketplaceFunctionRuns { FunctionId = "func_1p4R3gTobGZpD6fXvHT4rd", Limit = 50 });

        // The snapshot pins verb, path, the exact query (functionId + limit only) and no body,
        // and the new row fields read back from the generated response.
        await Verifier.Verify(new { Sent = fixture.LastRequest, response!.Runs }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Run_details_read_the_log_lines_by_targetCorrelationId()
    {
        using var fixture = NewFixture();

        await fixture.Client.GetMarketplaceFunctionRunDetailsAsync(CorrelationId);

        // GET /v2/logs/audit?targetCorrelationId=… — the DTO property name, not "correlationId".
        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Run_details_refuse_an_empty_correlation_id()
    {
        using var fixture = NewFixture();

        string? error = null;
        try
        {
            await fixture.Client.GetMarketplaceFunctionRunDetailsAsync(" ");
        }
        catch (ArgumentException ex)
        {
            error = $"{ex.GetType().Name}: {ex.ParamName}";
        }

        await Verifier.Verify(new { Error = error, NothingSent = fixture.RecordedRequests.Count == 0 }, VerifyConfig.VerifySettings);
    }
}
