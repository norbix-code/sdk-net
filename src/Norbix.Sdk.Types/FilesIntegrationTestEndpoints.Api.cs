#nullable enable annotations
#nullable disable warnings

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Norbix.Sdk.Types;

// ---------------------------------------------------------------------------
// Testing a files integration from the public API — 10b-files slice API-TEST
// (#39, decision 2026-09-15). Written by hand because this campaign does not
// regenerate the types; the next regeneration writes the same shapes into
// Generated/Api.dtos.cs and this file can go.
//
// The Hub client already has TestFilesIntegrationAsync
// (POST /{version}/files/integrations/test, integration id in the body). That
// one stays as it is. This is the API-surface twin, callable with an API key:
// the integration id travels in the URL, like every other API Files call.
// ---------------------------------------------------------------------------

namespace Norbix.Sdk.Types.Api;

///<summary>
///Runs a live probe against a files integration: uploads a small file, reads
///it, lists the folder and deletes the file again, then answers one result per
///step.
///<para>
///The gateway serves it at <c>POST /{version}/files/{filesIntegrationId}/test</c>.
///Because the probe writes to the storage, the gateway asks for the
///<c>files:create</c> permission (the one the upload calls ask), not
///<c>files:read</c>.
///</para>
///<para>
///A step that fails does not throw: it comes back in
///<see cref="TestFilesIntegrationResponse.Items"/> with <c>Result</c> set to
///<c>"FAILED"</c> and its <c>Errors</c>; the steps after it come back
///<c>"NOT_TESTED"</c>. Only a request the gateway refuses
///(bad id, no permission, unknown integration) throws a
///<c>NorbixException</c>.
///</para>
///</summary>
[NorbixRoute("/{version}/files/{filesIntegrationId}/test", "POST")]
[DataContract]
public partial class TestFilesIntegrationRequest
    : CodeMashRequestBase, INorbixRequest<TestFilesIntegrationResponse>
{
    ///<summary>The id of the files integration to probe.</summary>
    [DataMember]
    public virtual string FilesIntegrationId { get; set; }
}

///<summary>One result per probe step (upload, read, list, delete).</summary>
public partial class TestFilesIntegrationResponse
    : ResponseBase
{
    [DataMember]
    public virtual List<IntegrationTestResultItemDto>? Items { get; set; }
}

///<summary>The outcome of one probe step.</summary>
[DataContract]
public partial class IntegrationTestResultItemDto
{
    ///<summary>The step: <c>"UploadFile"</c>, <c>"GetFile"</c>, <c>"GetAllFiles"</c> or <c>"DeleteFile"</c>.</summary>
    [DataMember]
    public virtual string Operation { get; set; }

    ///<summary><c>"OK"</c>, <c>"FAILED"</c> or <c>"NOT_TESTED"</c> (skipped because an earlier step failed).</summary>
    [DataMember]
    public virtual string Result { get; set; }

    ///<summary>Why the step failed; empty or null when it passed.</summary>
    [DataMember]
    public virtual List<string>? Errors { get; set; }
}
