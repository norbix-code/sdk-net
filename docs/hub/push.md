# Push — .NET

[← Back to project README](../../README.md)

Every Push endpoint the gateway exposes, and the `NorbixClient` method that
calls it. The methods are generated at build time from the route attributes in
`src/Norbix.Sdk.Types/Generated/Hub.dtos.cs`, so this list matches the shipped
surface exactly.

They live on the Hub client:

```csharp
using Norbix.Sdk;
using Norbix.Sdk.Types.Hub;

var norbix = new NorbixClient(new NorbixClientOptions
{
    ApiKey = "sk_live_...",
    ProjectId = "proj_123",
});

await norbix.Notifications.EnablePushAsync();

var templates = await norbix.Notifications.GetPushTemplatesAsync(new GetPushTemplates());
```

Every method is covered by a test in `tests/Norbix.Hub.Tests/PushEndpointTests.cs`
that snapshots the exact request it sends.


## Module

| method | verb | path |
|---|---|---|
| `DisablePushAsync` | `GET` | `/notifications/push/disable` |
| `GetPushDisableDependenciesAsync` | `GET` | `/notifications/push/disable-dependencies` |
| `EnablePushAsync` | `GET` | `/notifications/push/enable` |
| `GetPushSettingsAsync` | `GET` | `/notifications/push/settings` |

## Integrations

| method | verb | path |
|---|---|---|
| `GetPushIntegrationsAsync` | `GET` | `/notifications/push/integrations` |
| `SavePushIntegrationAsync` | `POST` | `/notifications/push/integrations` |
| `RegisterCodeMashAppPushIntegrationAsync` | `POST` | `/notifications/push/integrations/app/request` |
| `ConfirmPushIntegrationHumanDeliveryAsync` | `POST` | `/notifications/push/integrations/confirm-human-delivery` |
| `TestPushIntegrationAsync` | `POST` | `/notifications/push/integrations/test` |
| `DeletePushIntegrationAsync` | `DELETE` | `/notifications/push/integrations/{Id}` |
| `SetPushIntegrationAsDefaultAsync` | `PUT` | `/notifications/push/integrations/{Id}/default` |
| `DisablePushIntegrationAsync` | `PUT` | `/notifications/push/integrations/{Id}/disable` |
| `EnablePushIntegrationAsync` | `PUT` | `/notifications/push/integrations/{Id}/enable` |
| `GetPushIntegrationAsync` | `GET` | `/notifications/push/integrations/{id}` |

## Templates

| method | verb | path |
|---|---|---|
| `GetPushTemplatesAsync` | `GET` | `/notifications/push/templates` |
| `CreatePushTemplateAsync` | `POST` | `/notifications/push/templates` |
| `UpdatePushTemplateAsync` | `PUT` | `/notifications/push/templates` |
| `RenderPushAsync` | `POST` | `/notifications/push/templates/render` |
| `DeletePushTemplateAsync` | `DELETE` | `/notifications/push/templates/{Id}` |
| `ArchivePushTemplateAsync` | `PUT` | `/notifications/push/templates/{Id}/archive` |
| `ClonePushTemplateAsync` | `POST` | `/notifications/push/templates/{Id}/clone` |
| `UnArchivePushTemplateAsync` | `PUT` | `/notifications/push/templates/{Id}/unarchive` |
| `GetPushTemplateAsync` | `GET` | `/notifications/push/templates/{id}` |
| `GetPushMessageContentTokensAsync` | `GET` | `/notifications/push/templates/{id}/tokens` |

## Campaigns

| method | verb | path |
|---|---|---|
| `GetPushCampaignsAsync` | `GET` | `/notifications/push/campaigns` |
| `CreatePushCampaignAsync` | `POST` | `/notifications/push/campaigns` |
| `DeletePushCampaignAsync` | `DELETE` | `/notifications/push/campaigns/{Id}` |
| `StopPushCampaignAsync` | `POST` | `/notifications/push/campaigns/{Id}/stop` |
| `GetPushCampaignMessagesAsync` | `GET` | `/notifications/push/campaigns/{campaignId}/messages` |
| `GetPushCampaignMessageAsync` | `GET` | `/notifications/push/campaigns/{campaignId}/messages/{id}` |
| `GetPushCampaignAsync` | `GET` | `/notifications/push/campaigns/{id}` |
| `GetPushCampaignBatchesAsync` | `GET` | `/notifications/push/campaigns/{id}/batches` |
| `GetPushCampaignBatchNotificationsAsync` | `GET` | `/notifications/push/campaigns/{id}/batches/{batchId}` |
| `GetPushCampaignBatchNotificationAsync` | `GET` | `/notifications/push/campaigns/{id}/batches/{batchId}/{notificationId}` |
| `GetPushCampaignStatisticsAsync` | `GET` | `/notifications/push/campaigns/{id}/stats` |
| `PreviewPushNotificationAsync` | `GET` | `/notifications/push/preview` |

## Devices

| method | verb | path |
|---|---|---|
| `RegisterDeviceAsync` | `POST` | `/notifications/push/devices` |

## Choosing who a campaign goes to

`CreatePushCampaignAsync` takes one of five audience shapes. The server picks
the shape from the `source` field, so use the matching request type rather than
`PushCampaignRequest` itself:

| audience | request type | key fields |
|---|---|---|
| everyone in the project | `PushToAllUsersRequest` | `RolesNames`, `UserTags` (both optional filters) |
| a named list of project users | `PushToUsersRequest` | `UserRecipients` |
| a named list of account users | `PushToAccountUsersRequest` | `UserRecipients` |
| rows of a database collection | `PushToCollectionRecordsRequest` | `SchemaName`, `Fields`, `FieldType` |
| raw device tokens | `PushToDevicesRequest` | `Devices` |

```csharp
await norbix.Notifications.CreatePushCampaignAsync(new CreatePushCampaignRequest
{
    Campaign = new PushToAllUsersRequest
    {
        TemplateId = "tpl_123",
        UserTags = ["beta"],
    },
});
```

## Choosing a push provider

`SavePushIntegrationAsync` works the same way — one request type per provider:

| provider | request type |
|---|---|
| Fake (sandbox, never sends) | `FakePushIntegrationRequest` |
| Android / Firebase | `AndroidFirebasePushIntegrationRequest` |
| Apple APNs | `AppleApnsPushIntegrationRequest` |
| Chrome extension | `ChromePluginPushIntegrationRequest` |
| Chrome web | `ChromeWebPushIntegrationRequest` |
| Edge web | `EdgeWebPushIntegrationRequest` |
| Firefox web | `FirefoxWebPushIntegrationRequest` |
| Safari | `SafariPushIntegrationRequest` |

Use `FakePushIntegrationRequest` in tests and local development. It accepts a
send and contacts no push service, so nothing reaches a real device.

## Known gaps

Three things do not work yet. They are gateway contract problems, not client
bugs, and each is tracked separately.

| what | why |
|---|---|
| `GetPushCampaignMessageAsync` | the route declares a `{id}` token, but the request type has `CampaignId`, `CampaignBatchId` and `NotificationId` and no `Id`, so the token can never be filled. |
| `CreatePushCampaignAsync` and `SavePushIntegrationAsync` over the wire | the client writes enum values as numbers, and the server reads the `source` / `provider` discriminator as a string. The request is built correctly but the server rejects it. The same applies to email campaigns, so the fix belongs in the shared transport. |
| the two managed-app endpoints | `integrations/app/check` and `integrations/test/codemash-app` are commented out on the gateway and are not routed at all. |

