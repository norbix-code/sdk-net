#nullable enable annotations
#nullable disable warnings

namespace Norbix.Sdk.Types.Hub;

// Hand-written companions to Generated/Hub.dtos.cs for the push integration
// endpoints.
//
// `POST /notifications/push/integrations` takes one body shape per provider.
// The server picks the shape from the `provider` field, and each shape accepts
// exactly one value. The export leaves `Provider` for the caller to set, so a
// forgotten or wrong value (e.g. `CodeMashChromePlugin` instead of
// `ChromePush` for the Chrome extension) is rejected by the server as
// "Unsupported provider". Each constructor below fixes the value the gateway
// expects (`PushIntegrationRequestDtoJsonConverter` in
// Hub.Push/Integrations/Save_.cs). The Fake shape lives in
// PushCampaignEndpoints.cs.

public partial class AndroidFirebasePushIntegrationRequest
{
    public AndroidFirebasePushIntegrationRequest() => Provider = PushProvider.AndroidFirebase;
}

public partial class AppleApnsPushIntegrationRequest
{
    public AppleApnsPushIntegrationRequest() => Provider = PushProvider.AppleApns;
}

public partial class ChromePluginPushIntegrationRequest
{
    public ChromePluginPushIntegrationRequest() => Provider = PushProvider.ChromePush;
}

public partial class ChromeWebPushIntegrationRequest
{
    public ChromeWebPushIntegrationRequest() => Provider = PushProvider.ChromeWeb;
}

public partial class EdgeWebPushIntegrationRequest
{
    public EdgeWebPushIntegrationRequest() => Provider = PushProvider.EdgeWeb;
}

public partial class FirefoxWebPushIntegrationRequest
{
    public FirefoxWebPushIntegrationRequest() => Provider = PushProvider.FirefoxWeb;
}

public partial class SafariPushIntegrationRequest
{
    public SafariPushIntegrationRequest() => Provider = PushProvider.SafariPush;
}
