# Scheduler — .NET

[← Back to project README](../../README.md)

The scheduler runs a task on a cron schedule. Today there is one kind of task:
**send an email campaign** (`SchedulerTaskType.EmailCampaign`). The other
values of `SchedulerTaskType` (push, SMS, code function, webhook) exist in the
enum but the gateway does not accept them yet — a save with one of them is
rejected with a 400.

The methods are generated at build time from the route attributes in
`src/Norbix.Sdk.Types/Generated/Hub.dtos.cs`, so this list matches the shipped
surface exactly. They live on the Hub client:

```csharp
using Norbix.Sdk;
using Norbix.Sdk.Types.Hub;

var norbix = new NorbixClient(new NorbixClientOptions
{
    ApiKey = "sk_live_...",
    ProjectId = "proj_123",
});

await norbix.Scheduler.EnableSchedulerAsync(new EnableScheduler());
```

Tests (all in `tests/Norbix.Hub.Tests/`, none of them contact the gateway):

- `EndpointCoverageTests` (`test_results/EndpointCoverageTests.Hub.Scheduler.verified.txt`)
  — one snapshot per method: verb, full path, query and body.
- `SchedulerBodyVariantTests.cs` — the save body with the email campaign task:
  `task.type`, `task.campaign` with its audience fields, and the top-level fields.

## Methods

| method | verb | path | request | where the fields go |
|---|---|---|---|---|
| `EnableSchedulerAsync` | `PUT` | `/scheduler/enable` | `EnableScheduler` | body (`env`) |
| `DisableSchedulerAsync` | `PUT` | `/scheduler/disable` | `DisableScheduler` | body (`env`) |
| `GetSchedulerTasksAsync` | `GET` | `/scheduler/tasks` | `GetSchedulerTasks` | query: `type`, `enabled`, `pageSize`, `startingAfter`, `endingBefore` |
| `GetSchedulerTaskAsync` | `GET` | `/scheduler/tasks/{id}` | `GetSchedulerTask` | path: `Id` |
| `SaveSchedulerTaskAsync` | `POST` | `/scheduler/tasks` | `SaveSchedulerTaskRequest` | body (see below) |
| `EnableSchedulerTaskAsync` | `PUT` | `/scheduler/tasks/{Id}/enable` | `EnableSchedulerTask` | path: `Id` |
| `DisableSchedulerTaskAsync` | `PUT` | `/scheduler/tasks/{Id}/disable` | `DisableSchedulerTask` | path: `Id` |
| `DeleteSchedulerTaskAsync` | `DELETE` | `/scheduler/tasks/{Id}` | `DeleteSchedulerTask` | path: `Id` |

Every path starts with `/{version}` (`/v2`).

**Module enable / disable are `PUT`.** Before v3.6 the SDK sent them as `GET`;
the gateway now answers only `PUT`, so an older SDK gets a 404/405 for these
two calls. Update the package — the call in your code does not change.

## Saving a task

`SaveSchedulerTaskAsync` creates a task, or updates one when `TaskId` is set.
It answers with an `IdResponse` whose `Id` is the task id.

| field | required | what it is |
|---|---|---|
| `Name` | yes | Shown in the portal list. |
| `Description` | no | Free text. |
| `Cron` | yes | Five fields (minute, hour, day of month, month, day of week), always **UTC**. `0 8 * * 1` = every Monday 08:00 UTC. |
| `InitiatorUserId` | yes | The user the task runs as (`usr_…`). It must be a member of this project and either **you** (the caller) or a **service user** of the project. |
| `IsEnabled` | yes | `false` saves the task without scheduling it. |
| `StopOnError` | yes | `true` disables the task after a failed run. |
| `Task` | yes | What to run. Today always an `EmailCampaignSchedulerTaskRequest`. |
| `TaskId` | update only | The id of the task to update. Updating needs the `scheduler:update` permission, creating `scheduler:create`. |

`Task` is typed as the base `SchedulerTaskRequest`; pass an
`EmailCampaignSchedulerTaskRequest`:

| field | what it is |
|---|---|
| `Type` | `SchedulerTaskType.EmailCampaign` — the discriminator the gateway reads. It is also the default value. |
| `Campaign` | The email campaign to send on each run. Pick the audience by the subclass (table below); set `TemplateId`. |
| `DatabaseIntegrationId` | Optional. Leave it out to use the project's default database. |

The campaign is the same `EmailCampaignRequest` family that
`CreateEmailCampaignAsync` takes. Set `Source` to the value that matches the
subclass — it is the audience discriminator.

| audience | subclass | `Source` | key fields |
|---|---|---|---|
| everyone in the project | `EmailToAllUsersDeliverySettingsRequest` | `AllUsers` | `RolesNames`, `UserTags` (optional filters) |
| a named list of project users | `EmailToUsersDeliverySettingsRequest` | `SpecifiedUsers` | `UserRecipients` |
| a named list of account users | `EmailToAccountUsersDeliverySettingsRequest` | `AccountUsers` | `UserRecipients` |
| raw email addresses | `EmailToEmailsDeliverySettingsRequest` | `Email` | `Recipients` |
| rows of a database collection | `EmailToCollectionRecordsDeliverySettingsRequest` | `Collection` | `SchemaName`, `Fields`, `FieldType` (`RoleNames`, `Languages` optional) |

### Example: a weekly digest to all subscribers

```csharp
var saved = await norbix.Scheduler.SaveSchedulerTaskAsync(new SaveSchedulerTaskRequest
{
    Name = "Weekly digest",
    Description = "Every Monday 08:00 UTC",
    Cron = "0 8 * * 1",                    // 5 fields, UTC
    InitiatorUserId = "usr_service_1",     // you, or a service user of the project
    IsEnabled = true,
    StopOnError = false,
    Task = new EmailCampaignSchedulerTaskRequest
    {
        Type = SchedulerTaskType.EmailCampaign,
        Campaign = new EmailToAllUsersDeliverySettingsRequest
        {
            Source = EmailCampaignRecipientsSourceTypes.AllUsers,
            TemplateId = "tpl_weekly_digest",
            RolesNames = ["subscriber"],   // optional
        },
    },
});

var taskId = saved!.Id!;
```

The body that goes over the wire (enums are written as camelCase names, the
same way the gateway writes them; the gateway reads `type` and `source`
ignoring case):

```json
{
  "name": "Weekly digest",
  "description": "Every Monday 08:00 UTC",
  "cron": "0 8 * * 1",
  "initiatorUserId": "usr_service_1",
  "isEnabled": true,
  "stopOnError": false,
  "task": {
    "type": "emailCampaign",
    "campaign": {
      "source": "allUsers",
      "rolesNames": ["subscriber"],
      "templateId": "tpl_weekly_digest"
    }
  }
}
```

### Updating, pausing and deleting

```csharp
// Update: same body, plus the task id.
await norbix.Scheduler.SaveSchedulerTaskAsync(new SaveSchedulerTaskRequest
{
    TaskId = taskId,
    Name = "Weekly digest",
    Cron = "0 9 * * 1",
    InitiatorUserId = "usr_service_1",
    IsEnabled = true,
    StopOnError = true,
    Task = new EmailCampaignSchedulerTaskRequest
    {
        Type = SchedulerTaskType.EmailCampaign,
        Campaign = new EmailToUsersDeliverySettingsRequest
        {
            Source = EmailCampaignRecipientsSourceTypes.SpecifiedUsers,
            TemplateId = "tpl_weekly_digest",
            UserRecipients = ["usr_1", "usr_2"],
        },
    },
});

await norbix.Scheduler.DisableSchedulerTaskAsync(new DisableSchedulerTask { Id = taskId });
await norbix.Scheduler.EnableSchedulerTaskAsync(new EnableSchedulerTask { Id = taskId });
await norbix.Scheduler.DeleteSchedulerTaskAsync(new DeleteSchedulerTask { Id = taskId });
```

## Reading tasks

```csharp
var page = await norbix.Scheduler.GetSchedulerTasksAsync(new GetSchedulerTasks
{
    Type = SchedulerTaskType.EmailCampaign,   // optional filter
    Enabled = true,                           // optional filter
    PageSize = 20,
});

var one = await norbix.Scheduler.GetSchedulerTaskAsync(new GetSchedulerTask { Id = taskId });
```

`GetSchedulerTasksAsync` answers with a cursor page of `SchedulerTaskListProjection`
(`TaskId`, `Name`, `Cron`, `Type`, `IsEnabled`); pass `StartingAfter` /
`EndingBefore` to move through it. `GetSchedulerTaskAsync` answers with the
full `SchedulerTaskDto` — the saved task is in `PayloadJson`, and the user it
runs as in `InitiatorId`.
