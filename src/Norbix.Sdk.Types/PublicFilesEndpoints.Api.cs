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

namespace Norbix.Sdk.Types.Api;

///<summary>
///Reads a file somebody made public.
///<para>
///<b>No sign-in and no project id.</b> The SDK sends no
///<c>Authorization</c> header for this call, even when the client is
///signed in — that is what <see cref="INorbixUnauthenticated"/> means
///here. The link has to work in an e-mail, in an <c>&lt;img src&gt;</c>,
///or in a browser on a stranger's phone, and the unguessable
///<c>nbpf_…</c> id is the whole credential.
///</para>
///<para>
///Answers with the file's raw bytes. When the storage provider signs its
///own links (Amazon S3, Azure Blob, Google Cloud Storage) Norbix replies
///302 and <c>HttpClient</c> follows it, so the bytes come from the
///provider and never pass through Norbix.
///</para>
///<para>
///Every miss is the same plain 404 — an unknown id, a name that does not
///match, a file made private again, a file gone from storage. A more
///precise answer would tell a stranger that the file is there.
///</para>
///</summary>
[NorbixRoute("/{version}/files/public/{publicId}/{name*}", "GET")]
[DataContract]
public partial class GetPublicFileRequest
    : RequestBase, INorbixRequest<byte[]>, INorbixUnauthenticated
{
    ///<summary>The <c>nbpf_…</c> id from the link.</summary>
    [DataMember]
    public virtual string PublicId { get; set; }

    ///<summary>
    ///What follows the id: the file's name for a file link, or the path
    ///inside the folder for a folder link (<c>2026/q1/report.pdf</c>).
    ///</summary>
    [DataMember]
    public virtual string Name { get; set; }
}

///<summary>The two fields slice PUB added to every file the read side returns.</summary>
public partial class FileResourceRefDto
{
    ///<summary>
    ///The address anyone can open without signing in — set only when the
    ///file really is public, and null otherwise.
    ///</summary>
    [DataMember(Order = 5)]
    public virtual string? PublicUrl { get; set; }

    ///<summary>
    ///True when anyone holding <see cref="PublicUrl"/> can read this file
    ///without signing in.
    ///</summary>
    [DataMember(Order = 6)]
    public virtual bool IsPublic { get; set; }
}

///<summary>One public folder in a listing.</summary>
[DataContract]
public partial class PublicFolderDto
{
    [DataMember(Order = 1)]
    public virtual string Path { get; set; }

    [DataMember(Order = 2)]
    public virtual string PublicId { get; set; }

    [DataMember(Order = 3)]
    public virtual string? PublicUrl { get; set; }

    [DataMember(Order = 4)]
    public virtual bool Inherited { get; set; }
}

///<summary>The public folders that come back with a file listing.</summary>
public partial class ListFilesResponse
{
    ///<summary>
    ///The subset of <c>Folders</c> that anyone can read from without
    ///signing in, with the link to put a path after.
    ///</summary>
    public virtual IList<PublicFolderDto>? PublicFolders { get; set; }
}
