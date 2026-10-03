# SMS — .NET

[← Back to project README](../../README.md)

Every SMS endpoint the gateway exposes, and the `NorbixClient` method that
calls it. The methods are generated at build time from the route attributes in
`src/Norbix.Sdk.Types/Generated/Hub.dtos.cs`, so this list matches the shipped
surface exactly. Each method carries a doc comment with its verb, path and
request type; the request types carry the gateway's own field descriptions.

They live on the Hub client:

```csharp
using Norbix.Sdk;
using Norbix.Sdk.Types.Hub;

var norbix = new NorbixClient(new NorbixClientOptions
{
    ApiKey = "sk_live_...",
    ProjectId = "proj_123",
});

await norbix.Notifications.EnableSmsAsync(new EnableSms());

var templates = await norbix.Notifications.GetSmsTemplatesAsync(new GetSmsTemplates());
```

Tests (all in `tests/Norbix.Hub.Tests/`, none of them contact an SMS service):

- `SmsEndpointTests.cs` — one test per method; snapshots the exact request it sends,
  plus a guard on the size of the SMS surface (34 endpoints).
- `SmsBodyVariantTests.cs` — the four campaign audiences and the Fake provider shape.
- `SmsFakeFlowTests.cs` — the Fake provider from enable to campaign statistics,
  with real JSON replies, so reading the answers is tested too.

## Quick start with the Fake provider

The Fake provider ("Test Me") accepts a send and never contacts a real SMS
service. Use it in tests and local development. The server builds the whole
Fake integration itself, so the request only needs the provider.

```csharp
var sms = norbix.Notifications;

await sms.EnableSmsAsync(new EnableSms());

var integration = await sms.SaveSmsIntegrationAsync(new SaveSmsIntegration
{
    Integration = new SmsIntegrationRequest { Provider = SmsProvider.Fake },
});
await sms.SetSmsIntegrationAsDefaultAsync(
    new SetSmsIntegrationAsDefaultRequest { Id = integration!.Id! });

var template = await sms.CreateSmsTemplateAsync(new CreateSmsTemplateRequest
{
    TemplateName = "welcome",
    CommunicationChannel = CommunicationChannel.Transactional,
    Translations =
    [
        new() { Language = "en", Content = new() { Subject = "Hi", Body = "Hi @Model.FirstName" } },
    ],
});

var campaign = await sms.CreateSmsCampaignAsync(new CreateSmsCampaignRequest
{
    TemplateId = template!.Id!,
    DeliveryType = SmsCampaignRecipientsSourceTypes.PhoneNumbers,
    PhoneNumbers = new SmsToPhoneNumbersDeliverySettingsDto
    {
        RecipientsSourceType = SmsCampaignRecipientsSourceTypes.PhoneNumbers,
        PhoneNumbers = ["+37060000000"],
    },
});

var stats = await sms.GetSmsCampaignStatisticsAsync(
    new GetSmsCampaignStatistics { Id = campaign!.Id! });
```

Tokens in a template body are Razor only (`@Model.FirstName`). Enums go over
the wire as camelCase names (`"fake"`, `"phoneNumbers"`), the same way the
gateway writes them.

## Module

| method | verb | path |
|---|---|---|
| `DisableSmsAsync` | `GET` | `/notifications/sms/disable` |
| `GetSmsDisableDependenciesAsync` | `GET` | `/notifications/sms/disable-dependencies` |
| `EnableSmsAsync` | `GET` | `/notifications/sms/enable` |
| `GetSmsSettingsAsync` | `GET` | `/notifications/sms/settings` |

## Integrations

| method | verb | path |
|---|---|---|
| `GetSmsIntegrationsAsync` | `GET` | `/notifications/sms/integrations` |
| `SaveSmsIntegrationAsync` | `POST` | `/notifications/sms/integrations` |
| `ConfirmSmsIntegrationHumanDeliveryAsync` | `POST` | `/notifications/sms/integrations/confirm-human-delivery` |
| `TestSmsIntegrationAsync` | `POST` | `/notifications/sms/integrations/test` |
| `DeleteSmsIntegrationAsync` | `DELETE` | `/notifications/sms/integrations/{Id}` |
| `SetSmsIntegrationAsDefaultAsync` | `PUT` | `/notifications/sms/integrations/{Id}/default` |
| `DisableSmsIntegrationAsync` | `PUT` | `/notifications/sms/integrations/{Id}/disable` |
| `EnableSmsIntegrationAsync` | `PUT` | `/notifications/sms/integrations/{Id}/enable` |
| `GetSmsIntegrationAsync` | `GET` | `/notifications/sms/integrations/{id}` |

## Templates

| method | verb | path |
|---|---|---|
| `GetSmsTemplatesAsync` | `GET` | `/notifications/sms/templates` |
| `CreateSmsTemplateAsync` | `POST` | `/notifications/sms/templates` |
| `UpdateSmsTemplateAsync` | `PUT` | `/notifications/sms/templates` |
| `RenderSmsAsync` | `POST` | `/notifications/sms/templates/render` |
| `DeleteSmsTemplateAsync` | `DELETE` | `/notifications/sms/templates/{Id}` |
| `ArchiveSmsTemplateAsync` | `PUT` | `/notifications/sms/templates/{Id}/archive` |
| `CloneSmsTemplateAsync` | `POST` | `/notifications/sms/templates/{Id}/clone` |
| `UnArchiveSmsTemplateAsync` | `PUT` | `/notifications/sms/templates/{Id}/unarchive` |
| `GetSmsTemplateAsync` | `GET` | `/notifications/sms/templates/{id}` |
| `GetSmsMessageContentTokensAsync` | `GET` | `/notifications/sms/templates/{id}/tokens` |

`RenderSmsAsync` runs a template body through the gateway's Razor engine with
the tokens you pass and answers with the bound text, or the names of the
tokens that are still unresolved (`Variables`). `GetSmsMessageContentTokensAsync`
lists the tokens a saved template uses, per translation.

## Campaigns

| method | verb | path |
|---|---|---|
| `GetSmsCampaignsAsync` | `GET` | `/notifications/sms/campaigns` |
| `CreateSmsCampaignAsync` | `POST` | `/notifications/sms/campaigns` |
| `DeleteSmsCampaignAsync` | `DELETE` | `/notifications/sms/campaigns/{id}` |
| `StopSmsCampaignAsync` | `POST` | `/notifications/sms/campaigns/{Id}/stop` |
| `GetSmsCampaignMessagesAsync` | `GET` | `/notifications/sms/campaigns/{campaignId}/messages` |
| `GetSmsCampaignMessageAsync` | `GET` | `/notifications/sms/campaigns/{campaignId}/messages/{notificationId}` |
| `GetSmsCampaignAsync` | `GET` | `/notifications/sms/campaigns/{id}` |
| `GetSmsCampaignBatchesAsync` | `GET` | `/notifications/sms/campaigns/{id}/batches` |
| `GetSmsCampaignBatchNotificationsAsync` | `GET` | `/notifications/sms/campaigns/{id}/batches/{batchId}` |
| `GetSmsCampaignBatchNotificationAsync` | `GET` | `/notifications/sms/campaigns/{id}/batches/{batchId}/{notificationId}` |
| `GetSmsCampaignStatisticsAsync` | `GET` | `/notifications/sms/campaigns/{id}/stats` |
| `PreviewSmsNotificationAsync` | `GET` | `/notifications/sms/preview` |

`GetSmsCampaignsAsync` narrows the list with `TemplateId`, `From` and `To`
(unix seconds, UTC). `StopSmsCampaignAsync` stops a running campaign at its
next batch; `DeleteSmsCampaignAsync` removes a campaign that has not started.

## Choosing who a campaign goes to

`CreateSmsCampaignAsync` takes a `DeliveryType` and the one settings object
that matches it. Set `RecipientsSourceType` inside that object to the same
value — it is the discriminator the server reads.

| audience | `DeliveryType` | settings object | key fields |
|---|---|---|---|
| everyone in the project | `AllUsers` | `AllUsers` (`SmsToAllUsersDeliverySettingsDto`) | `RolesNames`, `UserTags` (both optional filters) |
| a named list of project users | `SpecifiedUsers` | `SpecifiedUsers` (`SmsToUsersDeliverySettingsDto`) | `Recipients` |
| raw phone numbers | `PhoneNumbers` | `PhoneNumbers` (`SmsToPhoneNumbersDeliverySettingsDto`) | `PhoneNumbers` (international format) |
| rows of a database collection | `Collection` | `Collection` (`SmsToCollectionRecordsDeliverySettingsDto`) | `SchemaName`, `Fields`, `FieldType` |

Every settings object also takes `CampaignTime` (unix seconds, UTC; omit to
send now) and `MappedTokens` for template tokens the recipient does not
provide.

```csharp
await norbix.Notifications.CreateSmsCampaignAsync(new CreateSmsCampaignRequest
{
    TemplateId = "tpl_123",
    DeliveryType = SmsCampaignRecipientsSourceTypes.AllUsers,
    AllUsers = new SmsToAllUsersDeliverySettingsDto
    {
        RecipientsSourceType = SmsCampaignRecipientsSourceTypes.AllUsers,
        UserTags = ["beta"],
    },
});
```

## Choosing an SMS provider

`SaveSmsIntegrationAsync` takes an `SmsIntegrationRequest` whose `Provider`
names the service. The gateway knows eight: Twilio, Vonage, Plivo, Telnyx,
Bird, Telesign, Sinch and Fake.

Today the generated types carry only the base request (`IntegrationId`,
`Provider`, `IntegrationName`, `IsEnabled`), which is all the **Fake**
provider needs. The seven real providers also need their own credential
fields (Twilio: `AccountSid`, `FromPhoneNumber`, `AuthToken`; and so on).
Those request shapes are not exported by the gateway's type generation yet,
so they cannot be typed from .NET until the gateway exports them and the
types are regenerated. Use the portal to set up a real provider; the SDK can
then list, enable, disable, test and default it like any other integration.

Use the Fake provider in tests and local development. It accepts a send and
contacts no SMS service, so nothing reaches a real phone.

## Reading one campaign message

`GetSmsCampaignBatchNotificationAsync` answers with the rendered message that
went to one recipient. It takes the campaign id (`Id`), the batch id (from
`GetSmsCampaignBatchesAsync`) and the notification id (from
`GetSmsCampaignMessagesAsync`):

```csharp
var message = await norbix.Notifications.GetSmsCampaignBatchNotificationAsync(new GetSmsCampaignBatchNotification
{
    Id = campaign.Id!,
    BatchId = batch.BatchId,
    NotificationId = notification.NotificationId,
});

Console.WriteLine(message!.CampaignNotification!.Body); // the text as it was sent
```

The older `GetSmsCampaignMessageAsync` (`…/campaigns/{campaignId}/messages/{notificationId}`)
was removed together with its gateway route.
