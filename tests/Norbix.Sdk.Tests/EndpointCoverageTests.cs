using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

[TestFixture]
public sealed class EndpointCoverageTests
{
    public static IEnumerable<TestCaseData> EndpointModules()
    {
        return EndpointCoverageDriver.GetModuleCases();
    }

    [TestCaseSource(nameof(EndpointModules))]
    public async Task Generated_endpoint_methods_cover_current_dtos_by_module(
        string target,
        string group
    )
    {
        var coverage = await EndpointCoverageDriver.CoverModuleAsync(target, group);
        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName(coverage.SnapshotFileName);
        await Verifier.Verify(coverage.Snapshot, settings);
    }
}
