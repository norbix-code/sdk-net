# Contributing to `Norbix.Sdk`

The .NET SDK is generated at compile time from the gateway DTOs. The flow below is the same one CI runs — if you mirror it locally you'll never get a surprise on merge.

## Local setup

```bash
dotnet tool restore
dotnet restore
dotnet build
dotnet test
```

Requires the .NET 10 SDK (pinned in `global.json`).

## Editing the SDK

The source of truth is **the DTO files** in `src/Norbix.Sdk.Types/Generated/{Api,Hub}.dtos.cs`. Almost everything else under `Norbix.Sdk` is *derived* from them by the Roslyn source generator at compile time.

### Regenerate the SDK

There's no manual step. Just:

```bash
dotnet build
```

The `Norbix.Sdk.Generators` source generator runs as part of `Norbix.Sdk`'s compilation, walks every type with `[NorbixRoute]`, and emits the `ApiNamespace`, `HubNamespace`, and per-group `XxxModule` classes directly into the in-memory compilation. Nothing is committed under a `Generated/` folder for the SDK methods — they live as compiler artifacts only, so there's no drift to police.

### Regenerate the DTO files

The two DTO files come from a running gateway (Api host and Hub host). One
script does the export and the post-processing this repo needs:

```bash
dotnet tool install -g x        # once: the exporter
python3 scripts/sync-types.py --api http://localhost:5002 --hub http://localhost:5001
dotnet build && dotnet test
```

What the script does: runs `x csharp <url> <Api|Hub>`, flattens the output to
one namespace (`Norbix.Sdk.Types.Api` / `.Hub`), renames the upstream markers
(`[Route]` → `[NorbixRoute]`, `IReturn<T>` → `INorbixRequest<T>`), drops the
server-only attributes, and carries the auth markers (`INorbixUnauthenticated`,
`INorbixOptionalAuth`) over from the file it replaces — the contract does not
say which endpoint needs no credentials, the SDK does. A new endpoint that
needs a marker is added to the generated file once, by hand, and kept from
then on.

Two runs against the same gateway give byte-identical files, so a non-empty
`git diff` is a real contract change. Read it before committing: a type that
disappeared is a broken SDK method, not a tidy-up. The endpoint coverage
snapshots (`tests/*/test_results/EndpointCoverageTests.*.verified.txt`) then
change with the contract; accept a received snapshot only when every line in
it matches the diff.

Hand-written companions live next to the generated files as `partial` classes
(`PushIntegrationEndpoints.cs`, `SchemaFieldEndpoints.cs`, …) and survive a
regeneration.

If you need to add behavior that isn't per-endpoint (e.g. a new auth helper, a transport feature, a DI extension), edit:

- `src/Norbix.Sdk/NorbixClient.cs` — main client
- `src/Norbix.Sdk/Transport/HttpTransport.cs` — HTTP layer
- `src/Norbix.Sdk/NorbixClientOptions.cs` — config + env-var loading
- `src/Norbix.Sdk/Auth/*.cs` — login flow

## Versioning

The major version is frozen at **v3** until the public launch.

- A breaking change is released as a **minor** (for example v3.2.0 → v3.3.0), never as a new major.
- Write it as `feat(<scope>): <what>` and add a line `Breaking: <what changed and what callers must do>` in plain words, in the pull-request body and in the commit message.
- Never mark it the conventional-commits way: no `!` in the title (`feat!:`), no BREAKING CHANGE footer. The `PR title` check fails a pull request that does.
- As a safety net, the release config (`.releaserc.json` → `releaseRules`) maps breaking commits to a minor, so one that slips through still does not bump the major.

## Conventional commits

Required. The commit type drives the version bump:

| Type | Version | Example |
| --- | --- | --- |
| `feat:` | minor | `feat(database): add aggregate helper` |
| `fix:` | patch | `fix(transport): retry idempotent 5xx` |
| `perf:` `refactor:` | patch | |
| `docs(readme):` | patch | |
| `chore:` `test:` `ci:` `style:` | none | |
| breaking: `feat(...)` + `Breaking:` note (see Versioning) | minor | `feat(targets): drop net6.0 support` |

PRs to `main` get a sticky comment from `release-preview.yml` showing the computed next version before you merge.

## CI overview

| Workflow | Triggers | What it does |
| --- | --- | --- |
| `ci.yml` build | every PR + push | restore → build (warnaserror) → test (NUnit + Verify, code coverage) → `dotnet pack` dry-run + content audit |
| `ci.yml` security | every PR + push | `dotnet list package --vulnerable` (fails on High/Critical) → Trivy → OSV scanner |
| `codeql.yml` | every PR + push + Mon 06:00 | CodeQL with `security-extended` |
| `security-nightly.yml` | daily 04:00 UTC | re-runs scans against `main` for newly published CVEs |
| `release.yml` | push to `main`/`next`/`beta` | full CI again, then semantic-release → `dotnet pack` → `dotnet nuget push` → tag → CHANGELOG |
| `release-preview.yml` | PR to `main` | semantic-release dry-run + sticky PR comment |

Plus `dependabot.yml` for weekly grouped NuGet + Actions updates and out-of-band CVE patches.

## Verify snapshots

We use [Verify](https://github.com/VerifyTests/Verify) for shape-style assertions. First time a `Verifier.Verify(...)` runs, it produces a `*.received.txt` file. Inspect it and rename to `*.verified.txt` if the shape is correct. CI fails if a `*.received.txt` survives — that means a snapshot drifted unintentionally.

## Releases

`release.yml` runs on every push to `main`, `next`, or `beta`.

Required secrets:

| Secret | Where | What it's for |
| --- | --- | --- |
| `NUGET_API_KEY` | repo settings → Secrets → Actions | nuget.org push key. Keys expire (max 365 days) — renew before it lapses; an expired key fails the push with 403. |
| `GITHUB_TOKEN` | provided automatically | tag + GH release. |

semantic-release (installed with pinned versions by `scripts/install-semantic-release.sh`):

1. Computes the next version from conventional commits.
2. Pushes the `vX.Y.Z` tag.
3. Runs `dotnet pack ... -p:Version=<next>` for `Norbix.Api` (`src/Norbix.Sdk`) and `Norbix.Hub` (`src/Norbix.Hub`) via `@semantic-release/exec`. Each package bundles `Norbix.Contracts` and its Types assembly; the source generator is not shipped.
4. Runs `dotnet nuget push *.nupkg --skip-duplicate`.
5. Creates the GitHub Release with the notes.

`main` only accepts changes through pull requests, so the release does **not** commit back: `CHANGELOG.md` stopped at 1.3.1, and release notes live on [GitHub Releases](https://github.com/norbix-code/sdk-net/releases).

If the tag was pushed but the NuGet push failed, run the Release workflow manually with `republish=true`: it packs the latest tag from the tag's own tree, pushes it, and creates the GitHub Release if it is missing.

## Repo layout

```
.github/workflows/         CI / release / security workflows
.config/dotnet-tools.json  local .NET tool configuration
scripts/                   SDK maintenance scripts
src/
  Norbix.Sdk.Types/        DTO contracts + post-processed Generated/*.dtos.cs
  Norbix.Sdk.Generators/   Roslyn IIncrementalGenerator (analyzer-only)
  Norbix.Sdk/              client, transport, options, login, DI helpers
tests/
  Norbix.Sdk.Tests/        NUnit + Verify suite + Helpers/MockHttpHandler
docs/
  integrations/aspnet-core.md
  integrations/di.md
Directory.Build.props      shared MSBuild defaults (TargetFramework, warnings)
Directory.Packages.props   centralized package management
.releaserc.json            semantic-release plugins + branches
.commitlintrc.json
.editorconfig
```
