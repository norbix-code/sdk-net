using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using Norbix.Sdk.Types.Hub;

namespace Norbix.Sdk;

/// <summary>
/// The event envelope Norbix sends to a cloud function (AWS Lambda, Azure
/// Functions, Google Cloud Functions): the event name, the data of the source
/// event, the context — including a short-lived bearer token for the run-as
/// service user — and the developer's resolved config. One shape for every
/// source (<c>database</c>, <c>membership</c>, <c>files</c>, <c>payments</c>,
/// <c>ai</c>, <c>scheduler</c>, <c>manual</c>).
/// </summary>
/// <remarks>
/// The wire shape is the generated <see cref="CodeFunctionEventDto"/>; this
/// class parses the JSON the function received and adds the typed views
/// (<see cref="AsDatabaseRecordChange{T}"/>, <see cref="AsUserChange()"/>, <see cref="AsPaymentOrder"/>, …)
/// and <see cref="CreateClientOptions"/>, which turns
/// <c>context.runAs.accessToken</c> and the deployment urls into the options
/// a <c>NorbixApiClient</c> / <c>NorbixHubClient</c> takes.
/// </remarks>
/// <example>
/// <code>
/// var evt = CodeFunctionEvent.Parse(rawJson);
/// if (evt.Event == "database.record.updated")
/// {
///     var change = evt.AsDatabaseRecordChange&lt;Order&gt;();
///     using var api = new NorbixApiClient(evt.CreateClientOptions());
///     // … act as the run-as user
/// }
/// </code>
/// </example>
public sealed class CodeFunctionEvent
{
    /// <summary>The only envelope version this SDK reads.</summary>
    public const string SupportedSpecVersion = "1";

    // The gateway writes the envelope with CodeFunctionEventJson: camelCase
    // properties, camelCase enum names, nulls left out. Reading is
    // case-insensitive so a hand-written sample still parses.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly JsonElement _root;

    private CodeFunctionEvent(JsonElement root, CodeFunctionEventDto dto)
    {
        _root = root;
        Dto = dto;
    }

    /// <summary>The envelope as the generated wire type.</summary>
    public CodeFunctionEventDto Dto { get; }

    /// <summary>The envelope as it arrived, for a field this class has no view of.</summary>
    public JsonElement Raw => _root;

    /// <summary>Envelope version; always <see cref="SupportedSpecVersion"/> after a successful parse.</summary>
    public string SpecVersion => Dto.SpecVersion;

    /// <summary>This delivery.</summary>
    public string Id => Dto.Id;

    /// <summary>The change — the same id a webhook delivery of the same change carries.</summary>
    public string EventId => Dto.EventId;

    /// <summary>
    /// The handler key: the webhook event name of the source event
    /// (<c>database.record.updated</c>, <c>membership.user.registered</c>, …),
    /// <c>scheduler.task.fired</c> or <c>manual.invoke</c>.
    /// </summary>
    public string Event => Dto.Event;

    /// <summary>Which module raised it.</summary>
    public CodeFunctionSource Source => Dto.Source;

    /// <summary>When the envelope was built.</summary>
    public DateTimeOffset CreatedOn => Dto.CreatedOn;

    /// <summary>Ids, deployment urls, the caller (as data) and the run-as user with its token.</summary>
    public CodeFunctionContextDto Context => Dto.Context;

    /// <summary>The raw data slots — one is set per source.</summary>
    public CodeFunctionDataDto Data => Dto.Data;

    /// <summary>The developer's config, bound at fire time; <c>null</c> when the action has none.</summary>
    public IReadOnlyDictionary<string, object>? Config => Dto.Config;

    /// <summary>Parse the JSON body the function received.</summary>
    /// <exception cref="NorbixException">
    /// Not JSON, not an envelope (no <c>event</c>) or a <c>specVersion</c> this
    /// SDK does not read — <see cref="NorbixErrorCodes.InvalidFunctionEvent"/> /
    /// <see cref="NorbixErrorCodes.UnsupportedFunctionEvent"/>.
    /// </exception>
    public static CodeFunctionEvent Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw Invalid("The function event is not valid JSON.", ex);
        }

        using (document)
        {
            return Parse(document.RootElement);
        }
    }

    /// <summary>Parse an envelope the host runtime already read as a <see cref="JsonElement"/>.</summary>
    /// <exception cref="NorbixException">See <see cref="Parse(string)"/>.</exception>
    public static CodeFunctionEvent Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid($"The function event must be a JSON object, not {element.ValueKind}.");
        }

        // Clone so the event outlives the caller's JsonDocument.
        var root = element.Clone();

        if (!root.TryGetProperty("specVersion", out var specVersion) || specVersion.ValueKind != JsonValueKind.String)
        {
            throw Invalid("The function event has no specVersion; it is not a Norbix code function envelope.");
        }

        if (!string.Equals(specVersion.GetString(), SupportedSpecVersion, StringComparison.Ordinal))
        {
            throw new NorbixException(
                $"The function event has specVersion '{specVersion.GetString()}'; this SDK reads specVersion '{SupportedSpecVersion}'. Update the Norbix.Hub package.",
                code: NorbixErrorCodes.UnsupportedFunctionEvent);
        }

        CodeFunctionEventDto? dto;
        try
        {
            dto = root.Deserialize<CodeFunctionEventDto>(JsonOptions);
        }
        catch (JsonException ex)
        {
            throw Invalid("The function event does not match the envelope shape: " + ex.Message, ex);
        }

        if (dto is null || string.IsNullOrEmpty(dto.Event))
        {
            throw Invalid("The function event has no event name.");
        }

        dto.Context ??= new CodeFunctionContextDto();
        dto.Data ??= new CodeFunctionDataDto();
        return new CodeFunctionEvent(root, dto);
    }

    /// <summary>
    /// The Database slot: <c>old</c> / <c>new</c> read as <typeparamref name="T"/>
    /// (insert: <c>Old</c> null; delete: <c>New</c> null). When both documents
    /// were above the 200 KB cap they are left out, <see cref="DatabaseRecordChange{T}.Truncated"/>
    /// is true and <see cref="DatabaseRecordChange{T}.OldRef"/> / <see cref="DatabaseRecordChange{T}.NewRef"/>
    /// say where to fetch them.
    /// </summary>
    /// <exception cref="InvalidOperationException">The envelope carries no Database slot.</exception>
    public DatabaseRecordChange<T> AsDatabaseRecordChange<T>()
    {
        var slot = Dto.Data.Database ?? throw MissingSlot("database", nameof(AsDatabaseRecordChange));
        var raw = Slot("database");
        return new DatabaseRecordChange<T>
        {
            Collection = slot.Collection,
            SchemaId = slot.SchemaId,
            SchemaVersion = slot.SchemaVersion,
            RecordId = slot.RecordId,
            Old = ReadDocument<T>(raw, "old"),
            New = ReadDocument<T>(raw, "new"),
            Truncated = slot.Truncated,
            OldRef = slot.OldRef,
            NewRef = slot.NewRef,
        };
    }

    /// <summary>The Membership slot as the gateway sent it: <c>userId</c>, <c>old</c> / <c>new</c> user documents.</summary>
    /// <exception cref="InvalidOperationException">The envelope carries no Membership slot.</exception>
    public UserChangeDto AsUserChange() =>
        Dto.Data.User ?? throw MissingSlot("user", nameof(AsUserChange));

    /// <summary>The Membership slot with <c>old</c> / <c>new</c> read as <typeparamref name="T"/> (<c>OnInvited</c> has only <c>new.email</c>).</summary>
    /// <exception cref="InvalidOperationException">The envelope carries no Membership slot.</exception>
    public UserChange<T> AsUserChange<T>()
    {
        var slot = AsUserChange();
        var raw = Slot("user");
        return new UserChange<T>
        {
            UserId = slot.UserId,
            Old = ReadDocument<T>(raw, "old"),
            New = ReadDocument<T>(raw, "new"),
        };
    }

    /// <summary>The Files slot: upload → <c>New</c>, delete → <c>Old</c> (today only the path).</summary>
    /// <exception cref="InvalidOperationException">The envelope carries no Files slot.</exception>
    public FileChangeDto AsFileChange() =>
        Dto.Data.File ?? throw MissingSlot("file", nameof(AsFileChange));

    /// <summary>The Files slot with <c>old</c> / <c>new</c> read as <typeparamref name="T"/>.</summary>
    /// <exception cref="InvalidOperationException">The envelope carries no Files slot.</exception>
    public FileChange<T> AsFileChange<T>()
    {
        var slot = AsFileChange();
        var raw = Slot("file");
        return new FileChange<T>
        {
            Old = ReadDocument<T>(raw, "old"),
            New = ReadDocument<T>(raw, "new"),
            Path = slot.Path,
        };
    }

    /// <summary>The Payments slot: the webhook fact (provider, event name, status, amount, external ids); no "before".</summary>
    /// <exception cref="InvalidOperationException">The envelope carries no Payments slot.</exception>
    public PaymentFactDto AsPayment() =>
        Dto.Data.Payment ?? throw MissingSlot("payment", nameof(AsPayment));

    /// <summary>
    /// The payment order slot of a <c>payments.order.*</c> event
    /// (<c>payments.order.paid</c>, <c>.partially_refunded</c>, <c>.refunded</c>,
    /// <c>.reversed</c>, <c>.expired</c>): the stored order after the change
    /// (<see cref="PaymentOrderEventDto.Order"/>), the status move
    /// (<see cref="PaymentOrderEventDto.From"/> → <see cref="PaymentOrderEventDto.To"/>,
    /// <see cref="PaymentOrderEventDto.Reason"/>) and the provider callback that
    /// made it (<see cref="PaymentOrderEventDto.Payment"/>).
    /// <see cref="PaymentOrderEventDto.OrderEventId"/> is the de-duplication key:
    /// the same order change delivered twice carries the same id. <see cref="AsPayment"/>
    /// still returns the callback.
    /// </summary>
    /// <exception cref="InvalidOperationException">The envelope carries no order slot (not a <c>payments.order.*</c> event).</exception>
    public PaymentOrderEventDto AsPaymentOrder() =>
        Dto.Data.Order ?? throw MissingSlot("order", nameof(AsPaymentOrder));

    /// <summary>The Scheduler slot: which task fired and when.</summary>
    /// <exception cref="InvalidOperationException">The envelope carries no Scheduler slot.</exception>
    public ScheduledTaskInfoDto AsScheduledTask() =>
        Dto.Data.Task ?? throw MissingSlot("task", nameof(AsScheduledTask));

    /// <summary>The AI slot: the usage payload and the user it concerns.</summary>
    /// <exception cref="InvalidOperationException">The envelope carries no AI slot.</exception>
    public AiUsageEventDto AsAiUsage() =>
        Dto.Data.Ai ?? throw MissingSlot("ai", nameof(AsAiUsage));

    /// <summary>
    /// The caller's JSON object of a manual call (or a scheduler task's static
    /// payload) read as <typeparamref name="T"/>; <c>null</c> when the envelope has none.
    /// </summary>
    public T? Payload<T>() => ReadDocument<T>(Slot("payload"), null);

    /// <summary>The developer's config read as <typeparamref name="T"/>; <c>null</c> when the action has none.</summary>
    public T? ConfigAs<T>() =>
        _root.TryGetProperty("config", out var config) ? ReadDocument<T>(config, null) : default;

    /// <summary>
    /// The options a <c>NorbixApiClient</c> / <c>NorbixHubClient</c> takes to act
    /// as the run-as user: the bearer token from <c>context.runAs</c> (valid 15
    /// minutes), the project, account, environment and region of the event and
    /// the deployment's Hub and Api urls.
    /// </summary>
    /// <exception cref="InvalidOperationException">The envelope carries no run-as token.</exception>
    public NorbixClientOptions CreateClientOptions()
    {
        var context = Dto.Context;
        var token = context.RunAs?.AccessToken;
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                "The function event carries no context.runAs.accessToken, so no client can act as the run-as user.");
        }

        var options = new NorbixClientOptions
        {
            BearerToken = token,
            ProjectId = context.ProjectId,
            AccountId = context.AccountId,
            Region = context.Region,
        };
        if (!string.IsNullOrEmpty(context.Environment))
        {
            options.Env = context.Environment;
        }

        if (!string.IsNullOrEmpty(context.HubUrl))
        {
            options.HubBaseUrl = context.HubUrl;
        }

        if (!string.IsNullOrEmpty(context.ApiUrl))
        {
            options.ApiBaseUrl = context.ApiUrl;
        }

        return options;
    }

    private JsonElement Slot(string name)
    {
        if (_root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty(name, out var slot))
        {
            return slot;
        }

        return default;
    }

    private static T? ReadDocument<T>(JsonElement parent, string? property)
    {
        var element = parent;
        if (property is not null)
        {
            if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(property, out element))
            {
                return default;
            }
        }

        return element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? default
            : element.Deserialize<T>(JsonOptions);
    }

    private InvalidOperationException MissingSlot(string slot, string view)
    {
        var sourceName = JsonNamingPolicy.CamelCase.ConvertName(Dto.Source.ToString());
        return new InvalidOperationException(
            $"{view}() needs data.{slot}, but this '{Dto.Event}' event (source '{sourceName}') carries no such slot.");
    }

    private static NorbixException Invalid(string message, Exception? inner = null) =>
        new(message, code: NorbixErrorCodes.InvalidFunctionEvent, innerException: inner);
}

/// <summary>A Database trigger's record change, with the documents read as <typeparamref name="T"/>.</summary>
public sealed class DatabaseRecordChange<T>
{
    /// <summary>The collection (schema) name.</summary>
    public string Collection { get; init; } = "";

    /// <summary>The schema id — enough to read the schema over MCP / CLI; the schema itself is not sent.</summary>
    public string SchemaId { get; init; } = "";

    /// <summary>The schema version the record was written with, to compare with the one the code was written for.</summary>
    public int? SchemaVersion { get; init; }

    /// <summary>The record id.</summary>
    public string? RecordId { get; init; }

    /// <summary>The document before the change; <c>null</c> on insert or when truncated.</summary>
    public T? Old { get; init; }

    /// <summary>The document after the change; <c>null</c> on delete or when truncated.</summary>
    public T? New { get; init; }

    /// <summary>True when the documents were above the 200 KB cap and left out; fetch them with the refs.</summary>
    public bool Truncated { get; init; }

    /// <summary>Where to fetch the old document when truncated.</summary>
    public DocumentRefDto? OldRef { get; init; }

    /// <summary>Where to fetch the new document when truncated.</summary>
    public DocumentRefDto? NewRef { get; init; }
}

/// <summary>A Membership trigger's user change, with the user documents read as <typeparamref name="T"/>.</summary>
public sealed class UserChange<T>
{
    /// <summary>The user the event is about.</summary>
    public string UserId { get; init; } = "";

    /// <summary>The user before the change; <c>null</c> when there was none.</summary>
    public T? Old { get; init; }

    /// <summary>The user after the change; <c>null</c> on delete.</summary>
    public T? New { get; init; }
}

/// <summary>A Files trigger's file change, with the file documents read as <typeparamref name="T"/>.</summary>
public sealed class FileChange<T>
{
    /// <summary>The file before the change (delete).</summary>
    public T? Old { get; init; }

    /// <summary>The file after the change (upload).</summary>
    public T? New { get; init; }

    /// <summary>The file path.</summary>
    public string? Path { get; init; }
}
