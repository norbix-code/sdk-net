using System.Net;

using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The Database contract changes of the gateway's "last wave"
/// (refactoringV2, 2026-10-05), seen from the public Api client.
/// <list type="bullet">
/// <item><c>UpdateManyAsync</c> / <c>DeleteManyAsync</c> carry
/// <c>AllRecords</c>. An empty filter <c>{}</c> without it is refused with
/// CM-ERRORS-DATABASE-037.</item>
/// <item>The new write refusals reach the caller with the gateway's own code:
/// <c>$</c> operators in an update body (CM-ERRORS-DATABASE-035) and a broken
/// record document on insert (CM-ERRORS-DATABASE-036).</item>
/// </list>
/// Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class DatabaseContractTests
{
    private static VerifySettings Named(string name)
    {
        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"DatabaseContractTests.{name}");
        return settings;
    }

    private static object Refusal(string code, string message, string? field = null) =>
        new
        {
            responseStatus = new
            {
                isSuccess = false,
                errors = new[]
                {
                    new
                    {
                        message,
                        errorCode = code,
                        fieldName = field,
                    },
                },
            },
        };

    [Test]
    public async Task Update_many_with_all_records_sends_the_flag_in_the_body()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.UpdateManyAsync(
            new UpdateManyRequest
            {
                CollectionName = "orders",
                Filter = "{}",
                AllRecords = true,
                Update = "{\"status\":\"archived\"}",
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("UpdateManyAllRecords"));
    }

    [Test]
    public async Task Delete_many_with_all_records_sends_the_flag_in_the_query()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.DeleteManyAsync(
            new DeleteManyRequest
            {
                CollectionName = "orders",
                Filter = "{}",
                AllRecords = true,
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("DeleteManyAllRecords"));
    }

    [Test]
    public async Task Update_many_with_an_empty_filter_and_no_flag_surfaces_DATABASE_037()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(
            Refusal(
                "CM-ERRORS-DATABASE-037",
                "An empty filter matches every record. Set allRecords to true to confirm.",
                "filter"
            ),
            HttpStatusCode.BadRequest
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.UpdateManyAsync(
                    new UpdateManyRequest
                    {
                        CollectionName = "orders",
                        Filter = "{}",
                        Update = "{\"status\":\"archived\"}",
                    }
                )
        )!;

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                ex.HttpStatus,
                ex.ErrorCode,
                ex.Message,
            },
            Named("UpdateManyEmptyFilterRefused")
        );
    }

    [Test]
    public async Task Update_one_with_an_operator_body_surfaces_DATABASE_035()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(
            Refusal(
                "CM-ERRORS-DATABASE-035",
                "The update document must not contain $ operators.",
                "update"
            ),
            HttpStatusCode.BadRequest
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.UpdateOneAsync(
                    new UpdateOneRequest
                    {
                        CollectionName = "orders",
                        Id = "ord_1",
                        Update = "{\"$inc\":{\"count\":1}}",
                    }
                )
        )!;

        await Verifier.Verify(
            new { ex.HttpStatus, ex.ErrorCode, ex.Message },
            Named("UpdateOneOperatorRefused")
        );
    }

    [Test]
    public async Task Insert_many_with_a_broken_record_surfaces_DATABASE_036()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(
            Refusal("CM-ERRORS-DATABASE-036", "Invalid record document", "documents"),
            HttpStatusCode.BadRequest
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.InsertManyAsync(
                    new InsertManyRequest
                    {
                        CollectionName = "orders",
                        Documents = "[{\"total\":10}, {not json]",
                    }
                )
        )!;

        await Verifier.Verify(
            new { ex.HttpStatus, ex.ErrorCode, ex.Message },
            Named("InsertManyBrokenRecordRefused")
        );
    }
}
