using System.Reflection;
using System.Runtime.CompilerServices;

using VerifyTests;

namespace Norbix.Sdk.Tests.Helpers;

/// <summary>
/// One-time configuration for Verify across all tests. Mirrors the gateway
/// tests' SimpleTestBase conventions: scrub volatile values so snapshots
/// are stable across runs.
/// </summary>
internal static class VerifyConfig
{
    public static VerifySettings VerifySettings
    {
        get
        {
            var settings = new VerifySettings();
            settings.DontSortDictionaries();

            var buildDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var testResultsPath = Path.GetFullPath(Path.Combine(buildDir ?? "", "../../../", "test_results"));
            settings.UseDirectory(testResultsPath);

            return settings;
        }
    }

    [ModuleInitializer]
    public static void Initialize()
    {
        // Strip GUIDs that change every run.
        VerifierSettings.AddScrubber(text =>
        {
            var input = text.ToString();
            var scrubbed = System.Text.RegularExpressions.Regex.Replace(
                input,
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
                "{guid}");
            text.Clear();
            text.Append(scrubbed);
        });
    }
}
