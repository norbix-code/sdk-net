// Hand-maintained: gateway password endpoints not yet present in last Api.dtos regen.
// Remove this file once ChangePasswordRequest / RequestPasswordResetRequest /
// ConfirmPasswordResetRequest are regenerated into Generated/Api.dtos.cs.

#nullable enable annotations
#nullable disable warnings

using System.Runtime.Serialization;
using Norbix.Sdk.Types;

namespace Norbix.Sdk.Types.Api;

///<summary>
///Membership · Password
///</summary>
[NorbixRoute("/{version}/membership/userauth/password/change", "POST")]
[DataContract]
public partial class ChangePasswordRequest
    : CodeMashRequestBase, INorbixRequest<PasskeyOkResponse>
{
    [DataMember]
    public virtual string CurrentPassword { get; set; }

    [DataMember]
    public virtual string NewPassword { get; set; }

    [DataMember]
    public virtual string? DatabaseIntegrationId { get; set; }
}

///<summary>
///Membership · Password
///</summary>
[NorbixRoute("/{version}/membership/userauth/password/reset/request", "POST")]
[DataContract]
public partial class RequestPasswordResetRequest
    : CodeMashRequestBase, INorbixRequest<PasskeyOkResponse>
{
    [DataMember]
    public virtual string Email { get; set; }
}

///<summary>
///Membership · Password
///</summary>
[NorbixRoute("/{version}/membership/userauth/password/reset/confirm", "POST")]
[DataContract]
public partial class ConfirmPasswordResetRequest
    : CodeMashRequestBase, INorbixRequest<PasskeyOkResponse>
{
    [DataMember]
    public virtual string Token { get; set; }

    [DataMember]
    public virtual string NewPassword { get; set; }

    [DataMember]
    public virtual string? DatabaseIntegrationId { get; set; }
}
