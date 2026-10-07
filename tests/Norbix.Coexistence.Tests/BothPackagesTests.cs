using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Norbix.Sdk;

namespace Norbix.Coexistence.Tests;

/// <summary>
/// Norbix.Api and Norbix.Hub in one project: each client keeps its own name,
/// and the container keeps their options apart.
/// </summary>
public class BothPackagesTests
{
    [Test]
    public async Task Both_clients_build_side_by_side()
    {
        var options = new NorbixClientOptions("sk_test", "proj_api");
        using var api = new NorbixApiClient(options);
        using var hub = new NorbixHubClient(new NorbixClientOptions("sk_test", "proj_hub"));

        await Verify(new
        {
            Api = new { Type = api.GetType().FullName, Assembly = api.GetType().Assembly.GetName().Name, api.Options.ProjectId },
            Hub = new { Type = hub.GetType().FullName, Assembly = hub.GetType().Assembly.GetName().Name, hub.Options.ProjectId },
            SharedOptionsAssembly = typeof(NorbixClientOptions).Assembly.GetName().Name,
            SharedExceptionAssembly = typeof(NorbixException).Assembly.GetName().Name,
        });
    }

    [Test]
    public async Task One_container_registers_both_clients_with_separate_options()
    {
        var services = new ServiceCollection();
        services.AddNorbixApi(o => { o.ApiKey = "sk_api"; o.ProjectId = "proj_api"; });
        services.AddNorbixHub(o => { o.ApiKey = "sk_hub"; o.ProjectId = "proj_hub"; o.AccountId = "acc_1"; });

        await using var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<NorbixApiClient>();
        var hub = provider.GetRequiredService<NorbixHubClient>();
        var monitor = provider.GetRequiredService<IOptionsMonitor<NorbixClientOptions>>();

        await Verify(new
        {
            Api = new { api.Options.ProjectId, api.Options.AccountId },
            Hub = new { hub.Options.ProjectId, hub.Options.AccountId },
            ApiOptions = monitor.Get(NorbixApiServiceCollectionExtensions.OptionsName).ProjectId,
            HubOptions = monitor.Get(NorbixHubServiceCollectionExtensions.OptionsName).ProjectId,
        });
    }
}
