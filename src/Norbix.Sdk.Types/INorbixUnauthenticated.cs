namespace Norbix.Sdk.Types;

/// <summary>
/// Marks a request that must go out with <b>no</b> <c>Authorization</c>
/// header, whatever the client is holding.
/// </summary>
/// <remarks>
/// <para>
/// Until now the only unauthenticated endpoints were the login routes under
/// <c>/auth</c>, and the source generator recognised them by their path. A
/// public file link (<c>GET /{version}/files/public/{publicId}/{name}</c>,
/// 10b-files slice PUB) is the first one somewhere else: it carries no session
/// and no project id on purpose, because the link has to work in an e-mail or
/// in a browser on a stranger's phone.
/// </para>
/// <para>
/// Sending a session with it would be worse than useless — it would attach
/// the caller's credentials to a request that is meant to prove nothing but
/// the unguessable id in the URL. Hence a marker on the DTO rather than one
/// more special case on a path.
/// </para>
/// </remarks>
public interface INorbixUnauthenticated { }
