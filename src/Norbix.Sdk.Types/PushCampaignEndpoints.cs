#nullable enable annotations
#nullable disable warnings

using System.Collections.Generic;
using System.Runtime.Serialization;
using Norbix.Sdk.Types;

namespace Norbix.Sdk.Types.Hub;

// Hand-written companions to Generated/Hub.dtos.cs for the push campaign
// endpoints. Two things the export drops:
//
//  1. DeletePushCampaignRequest and StopPushCampaignRequest carry an `Id` on
//     the gateway, but the exported class is empty, so the `{Id}` route token
//     could never be filled and neither call could be made at all. Both are
//     `partial`, so the property is added back here and survives the next
//     regeneration of Hub.dtos.cs.
//
//  2. PushCampaignRequest is abstract on the gateway with one subclass per
//     audience, routed by the wire-level `source` discriminator. The export
//     flattens it to a single concrete class with no audience fields, so no
//     campaign could actually be targeted. The five subclasses below restore
//     that, each fixing `Source` in its constructor.

///<summary>Deletes push campaign from queue</summary>
public partial class DeletePushCampaignRequest
{
    ///<summary>The push campaign id to delete. Get it from GetPushCampaignsAsync.</summary>
    [DataMember]
    public virtual string Id { get; set; }

    ///<summary>Optional database integration id; omit to use the project's default.</summary>
    [DataMember]
    public virtual string? DatabaseIntegrationId { get; set; }
}

///<summary>Stops a running push campaign</summary>
public partial class StopPushCampaignRequest
{
    ///<summary>The campaign id to stop.</summary>
    [DataMember]
    public virtual string Id { get; set; }

    ///<summary>Optional database integration id; omit to use the project's default.</summary>
    [DataMember]
    public virtual string? DatabaseIntegrationId { get; set; }
}

///<summary>
///Sends to every user in the project, optionally narrowed to roles or tags.
///</summary>
public partial class PushToAllUsersRequest : PushCampaignRequest
{
    public PushToAllUsersRequest() => Source = PushCampaignRecipientsSourceTypes.AllUsers;

    ///<summary>Only users holding one of these roles. Null means every role.</summary>
    public virtual HashSet<string>? RolesNames { get; set; }

    ///<summary>Only users carrying one of these tags. Null means every tag.</summary>
    public virtual HashSet<string>? UserTags { get; set; }
}

///<summary>Sends to a named list of project users.</summary>
public partial class PushToUsersRequest : PushCampaignRequest
{
    public PushToUsersRequest() => Source = PushCampaignRecipientsSourceTypes.SpecifiedUsers;

    ///<summary>The user ids to send to.</summary>
    public virtual HashSet<string> UserRecipients { get; set; } = new();
}

///<summary>Sends to a named list of account users (account scope, not project).</summary>
public partial class PushToAccountUsersRequest : PushCampaignRequest
{
    public PushToAccountUsersRequest() =>
        Source = PushCampaignRecipientsSourceTypes.AccountUsers;

    ///<summary>The account user ids to send to.</summary>
    public virtual HashSet<string> UserRecipients { get; set; } = new();
}

///<summary>
///Sends to recipients read out of a database collection — each record supplies
///the user (or email) through the named fields.
///</summary>
public partial class PushToCollectionRecordsRequest : PushCampaignRequest
{
    public PushToCollectionRecordsRequest() =>
        Source = PushCampaignRecipientsSourceTypes.Collection;

    ///<summary>The record fields that hold the recipient.</summary>
    public virtual HashSet<string> Fields { get; set; } = new();

    ///<summary>The collection to read recipients from.</summary>
    public virtual string SchemaName { get; set; }

    ///<summary>Whether <see cref="Fields"/> hold a user id or an email address.</summary>
    public virtual CollectionEmailCampaignRecipientField FieldType { get; set; }

    ///<summary>Only records whose user holds one of these roles. Null means every role.</summary>
    public virtual HashSet<string>? RoleNames { get; set; }

    ///<summary>Only records in one of these languages. Null means every language.</summary>
    public virtual HashSet<string>? Languages { get; set; }
}

///<summary>A single device token plus the platform it belongs to.</summary>
public partial class PushDeviceDeliveryTokenRequest
{
    ///<summary>The device token as the platform issued it.</summary>
    public virtual string Token { get; set; }

    ///<summary>The platform the token came from.</summary>
    public virtual PushDeviceDeliveryFamily DeliveryFamily { get; set; }
}

///<summary>Sends straight to a list of device tokens, bypassing users.</summary>
public partial class PushToDevicesRequest : PushCampaignRequest
{
    public PushToDevicesRequest() => Source = PushCampaignRecipientsSourceTypes.Devices;

    ///<summary>The devices to send to.</summary>
    public virtual HashSet<PushDeviceDeliveryTokenRequest> Devices { get; set; } = new();
}

///<summary>
///Wire shape for creating the Fake push integration — the sandbox provider
///that accepts a send and never contacts a real push service. It carries no
///settings: the server builds the whole integration itself, so the body is
///just the provider discriminator. Dropped by the export because it has no
///own fields, which left .NET unable to create a Fake integration at all.
///</summary>
public partial class FakePushIntegrationRequest : PushIntegrationRequest
{
    public FakePushIntegrationRequest() => Provider = PushProvider.Fake;
}
