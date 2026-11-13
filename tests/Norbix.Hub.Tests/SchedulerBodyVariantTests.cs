using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// <c>POST /scheduler/tasks</c> takes a polymorphic body twice over: the
/// <c>task</c> is picked by its <c>type</c> discriminator (email, SMS and
/// push campaigns, <c>CodeFunctionalCall</c>, <c>WorkflowCall</c>, …), and an
/// email task's <c>campaign</c> is picked by
/// its <c>source</c> discriminator (all users, named users, a collection, …).
/// <see cref="SaveSchedulerTaskRequest.Task"/> is typed as the base
/// <see cref="SchedulerTaskRequest"/> and the campaign as the base
/// <see cref="EmailCampaignRequest"/>, so these tests assert that the
/// subtype's own fields (the campaign, the audience filters) reach the wire —
/// a route test alone would not see them.
///
/// Nothing leaves the process and no email provider is contacted.
/// </summary>
[TestFixture]
public sealed class SchedulerBodyVariantTests
{
    private static async Task<object> SendAsync(
        Func<NorbixHubClient, Task> call,
        [System.Runtime.CompilerServices.CallerMemberName] string caller = ""
    )
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });
        fixture.RespondNoContentDefault();

        await call(fixture.Client);

        return fixture.LastRequest
            ?? throw new InvalidOperationException($"{caller} sent no request.");
    }

    [Test]
    public async Task Save_email_campaign_task_carries_the_task_and_campaign_shape()
    {
        var sent = await SendAsync(client =>
            client.Scheduler.SaveSchedulerTaskAsync(
                new SaveSchedulerTaskRequest
                {
                    Name = "Weekly digest",
                    Description = "Every Monday 08:00 UTC",
                    Cron = "0 8 * * 1",
                    InitiatorUserId = "usr_service_1",
                    IsEnabled = true,
                    StopOnError = false,
                    Task = new EmailCampaignSchedulerTaskRequest
                    {
                        Type = SchedulerTaskType.EmailCampaign,
                        Campaign = new EmailToAllUsersDeliverySettingsRequest
                        {
                            Source = EmailCampaignRecipientsSourceTypes.AllUsers,
                            TemplateId = "tpl_weekly_digest",
                            RolesNames = new HashSet<string> { "subscriber" },
                            UserTags = new HashSet<string> { "weekly" },
                        },
                    },
                }
            )
        );

        await Verifier.Verify(sent, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Update_email_campaign_task_sends_the_task_id_in_the_body()
    {
        var sent = await SendAsync(client =>
            client.Scheduler.SaveSchedulerTaskAsync(
                new SaveSchedulerTaskRequest
                {
                    TaskId = "tsk_1",
                    Name = "Weekly digest",
                    Cron = "0 8 * * 1",
                    InitiatorUserId = "usr_service_1",
                    IsEnabled = false,
                    StopOnError = true,
                    Task = new EmailCampaignSchedulerTaskRequest
                    {
                        Type = SchedulerTaskType.EmailCampaign,
                        DatabaseIntegrationId = "int_db_1",
                        Campaign = new EmailToUsersDeliverySettingsRequest
                        {
                            Source = EmailCampaignRecipientsSourceTypes.SpecifiedUsers,
                            TemplateId = "tpl_weekly_digest",
                            UserRecipients = new HashSet<string> { "usr_1", "usr_2" },
                        },
                    },
                }
            )
        );

        await Verifier.Verify(sent, VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// A "Code call" task: the function reference, the invocation type and the
    /// config (gateway <c>Hub.Scheduler/Tasks/Save.Code.cs</c>) reach the wire
    /// under <c>task</c> with type <c>CodeFunctionalCall</c>. Twins: sdk-go
    /// <c>TestSaveSchedulerTaskSendsCodeAndWorkflowTasks</c>, sdk-ts
    /// <c>tests/hub/scheduler-task-bodies.test.ts</c>.
    /// </summary>
    [Test]
    public async Task Save_code_call_task_carries_the_function_reference()
    {
        var sent = await SendAsync(client =>
            client.Scheduler.SaveSchedulerTaskAsync(
                new SaveSchedulerTaskRequest
                {
                    Name = "Nightly report",
                    Cron = "0 2 * * *",
                    InitiatorUserId = "usr_service_1",
                    IsEnabled = true,
                    StopOnError = false,
                    Task = new CodeFunctionSchedulerTaskRequest
                    {
                        Type = SchedulerTaskType.CodeFunctionalCall,
                        FunctionRef = new FunctionRefDto
                        {
                            IntegrationId = "int_lambda",
                            FunctionId = "nightly-report",
                            Qualifier = "live",
                        },
                        InvocationType = CodeInvocationType.Async,
                        Config = "{\"source\":\"net\"}",
                    },
                }
            )
        );

        await Verifier.Verify(sent, VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// A "Workflow call" task: the workflow reference and the string payload
    /// (gateway <c>Hub.Scheduler/Tasks/Save.Workflow.cs</c>) reach the wire
    /// under <c>task</c> with type <c>WorkflowCall</c>.
    /// </summary>
    [Test]
    public async Task Save_workflow_call_task_carries_the_workflow_and_payload()
    {
        var sent = await SendAsync(client =>
            client.Scheduler.SaveSchedulerTaskAsync(
                new SaveSchedulerTaskRequest
                {
                    Name = "Nightly onboarding sweep",
                    Cron = "0 3 * * *",
                    InitiatorUserId = "usr_service_1",
                    IsEnabled = true,
                    StopOnError = false,
                    Task = new WorkflowSchedulerTaskRequest
                    {
                        Type = SchedulerTaskType.WorkflowCall,
                        WorkflowRef = "wf_3kQ9mZ2vXc8LpR4wT7yH1n",
                        Payload = new Dictionary<string, string> { ["source"] = "net" },
                    },
                }
            )
        );

        await Verifier.Verify(sent, VerifyConfig.VerifySettings);
    }
}
