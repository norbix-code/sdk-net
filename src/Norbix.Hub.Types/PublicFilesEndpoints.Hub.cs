#nullable enable annotations
#nullable disable warnings

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Norbix.Sdk.Types;

// ---------------------------------------------------------------------------
// Public file links — 10b-files slice PUB, brought into the SDK by slice
// SDK-2. Written by hand because this campaign does not regenerate the types;
// the next regeneration writes the same shapes into Generated/*.dtos.cs and
// this file can go.
//
// A file, or a whole folder, can be made readable by anyone holding a link —
// no sign-in, no project id, no account. Norbix keeps a record and mints an
// unguessable id that looks like "nbpf_7hK2…"; the link is then
//
//     https://<your api host>/v3/files/public/nbpf_7hK2…/invoice.pdf
//
// Four rules worth knowing before you call any of this:
//
//   * Publishing a folder is ONE record, whatever is under it, at any depth.
//   * Asking twice gives the same id back — the first link is already out in
//     somebody's hands, and a second id would leave it live and invisible.
//   * A file cannot be made private on its own while a folder above it is
//     public (CM-ERRORS-FILES-021); switch the folder off instead.
//   * The root cannot be published, and a folder link with nothing after it
//     is a 404: publishing a prefix must not publish its listing.
// ---------------------------------------------------------------------------

namespace Norbix.Sdk.Types.Hub;


///<summary>
///Makes one file readable by anyone holding its link. The file has to exist
///already — Norbix reads it from the provider first, so a link that points at
///nothing is never handed out. <see cref="IdResponse.Id"/> is the
///<c>nbpf_…</c> public id.
///</summary>
[NorbixRoute("/{version}/files/item/public", "POST")]
[DataContract]
public partial class MakeFilePublicRequest
    : CodeMashRequestBase, INorbixRequest<IdResponse>
{
    ///<summary>The files integration the file lives on.</summary>
    [DataMember]
    public virtual string FilesIntegrationId { get; set; }

    ///<summary>Path of the file to publish, relative to the integration.</summary>
    [DataMember]
    public virtual string Path { get; set; }
}

///<summary>
///Takes a file's public link away; opening it afterwards gives a 404.
///Refused while a folder above the file is public — the error names the
///folder to switch off instead.
///</summary>
[NorbixRoute("/{version}/files/item/private", "POST")]
[DataContract]
public partial class MakeFilePrivateRequest
    : CodeMashRequestBase, INorbixRequest<EmptyResponse>
{
    ///<summary>The files integration the file lives on.</summary>
    [DataMember]
    public virtual string FilesIntegrationId { get; set; }

    ///<summary>Path of the file, relative to the integration.</summary>
    [DataMember]
    public virtual string Path { get; set; }
}

///<summary>
///Publishes a whole folder prefix. Files already published inside it keep
///their own links — they agree with the folder, and those links are already
///shared. <see cref="IdResponse.Id"/> is the <c>nbpf_…</c> public id.
///</summary>
[NorbixRoute("/{version}/files/folder/public", "POST")]
[DataContract]
public partial class MakeFolderPublicRequest
    : CodeMashRequestBase, INorbixRequest<IdResponse>
{
    ///<summary>The files integration the folder lives on.</summary>
    [DataMember]
    public virtual string FilesIntegrationId { get; set; }

    ///<summary>Folder prefix to publish. The root cannot be published.</summary>
    [DataMember]
    public virtual string Path { get; set; }
}

///<summary>
///Takes back every link inside the folder, per-file links included. That is
///the point: after this call nothing under the prefix is public.
///</summary>
[NorbixRoute("/{version}/files/folder/private", "POST")]
[DataContract]
public partial class MakeFolderPrivateRequest
    : CodeMashRequestBase, INorbixRequest<EmptyResponse>
{
    ///<summary>The files integration the folder lives on.</summary>
    [DataMember]
    public virtual string FilesIntegrationId { get; set; }

    ///<summary>Folder prefix, relative to the integration.</summary>
    [DataMember]
    public virtual string Path { get; set; }
}

///<summary>
///One folder in a listing that anyone can read from without signing in. Sent
///alongside the plain <c>Folders</c> prefix list, so a client that does not
///know about public folders keeps working and simply shows no badge.
///</summary>
[DataContract]
public partial class PublicFolderDto
{
    ///<summary>The folder prefix, exactly as it appears in the Folders list.</summary>
    [DataMember(Order = 1)]
    public virtual string Path { get; set; }

    ///<summary>The record that makes it public — its own, or a folder above it.</summary>
    [DataMember(Order = 2)]
    public virtual string PublicId { get; set; }

    ///<summary>
    ///The base a file inside this folder is served from; put the path inside
    ///the folder after it. Ends with a slash.
    ///</summary>
    [DataMember(Order = 3)]
    public virtual string? PublicUrl { get; set; }

    ///<summary>True when a folder ABOVE this one is what makes it public.</summary>
    [DataMember(Order = 4)]
    public virtual bool Inherited { get; set; }
}

///<summary>The two fields slice PUB added to every file the read side returns.</summary>
public partial class FileResourceRefDto
{
    ///<summary>
    ///The address anyone can open without signing in — set only when the file
    ///really is public, and null otherwise.
    ///</summary>
    [DataMember(Order = 5)]
    public virtual string? PublicUrl { get; set; }

    ///<summary>
    ///True when anyone holding <see cref="PublicUrl"/> can read this file
    ///without signing in — because the file itself was made public, or
    ///because a folder above it was.
    ///</summary>
    [DataMember(Order = 6)]
    public virtual bool IsPublic { get; set; }
}

///<summary>The public folders that come back with a folder listing.</summary>
public partial class GetFolderFilesResponse
{
    ///<summary>
    ///The subset of <c>Folders</c> that anyone can read from without signing
    ///in, with the link to put a path after.
    ///</summary>
    public virtual IList<PublicFolderDto>? PublicFolders { get; set; }
}
