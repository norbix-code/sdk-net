# Contributing to `Norbix.Sdk`

The .NET SDK is generated at compile time from the gateway DTOs. The flow below is the same one CI runs — if you mirror it locally you'll never get a surprise on merge.

## Local setup

```bash
dotnet tool restore       # installs the `x` ServiceStack CLI per .config/dotnet-tools.json
dotnet restore
dotnet build
dotnet test
```

Requires the .NET 10 SDK (pinned in `global.json`).

## Editing the SDK

The source of truth is **the DTO files** in `src/Norbix.Sdk.Types/Generated/{Api,Hub}.dtos.cs`. Almost everything else under `Norbix.Sdk` is *derived* from them by the Roslyn source generator at compile time.

### Refresh the DTOs from the gateways

```bash
./scripts/sync-types.sh                   # localhost gateways
./scripts/sync-types.sh --prod            # api.norbix.dev / hub.norbix.dev
./scripts/sync-types.sh --api-url <url>   # custom URL
```

PowerShell:

```pwsh
./scripts/sync-types.ps1
./scripts/sync-types.ps1 -Mode prod
```

The script:
1. Runs `x csharp <metadata-url> <out>` for both gateways.
2. Strips ServiceStack imports + types (`IReturn<T>` → `INorbixRequest<T>`, `[Route]` → `[NorbixRoute]`, etc.).
3. Applies the same regex fixes documented in `/cloud/src/types/README.md`.
4. Writes to `src/Norbix.Sdk.Types/Generated/{Api,Hub}.dtos.cs`.

### Regenerate the SDK

There's no manual step. Just:

```bash
dotnet build
```

The `Norbix.Sdk.Generators` source generator runs as part of `Norbix.Sdk`'s compilation, walks every type with `[NorbixRoute]`, and emits the `ApiNamespace`, `HubNamespace`, and per-group `XxxModule` classes directly into the in-memory compilation. Nothing is committed under a `Generated/` folder for the SDK methods — they live as compiler artifacts only, so there's no drift to police.

If you need to add behavior that isn't per-endpoint (e.g. a new auth helper, a transport feature, a DI extension), edit:

- `src/Norbix.Sdk/Norbix.cs` — main client
- `src/Norbix.Sdk/Transport/HttpTransport.cs` — HTTP layer
- `src/Norbix.Sdk/NorbixOptions.cs` — config + env-var loading
- `src/Norbix.Sdk/Auth/*.cs` — login flow

## Conventional commits

Required. The commit type drives the version bump:

| Type | Version | Example |
| --- | --- | --- |
| `feat:` | minor | `feat(database): add aggregate helper` |
| `fix:` | patch | `fix(transport): retry idempotent 5xx` |
| `perf:` `refactor:` | patch | |
| `docs(readme):` | patch | |
| `chore:` `test:` `ci:` `style:` | none | |
| any with `!` or `BREAKING CHANGE:` footer | major | `feat!: drop net6.0 support` |

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
| `NUGET_API_KEY` | repo settings → Secrets → Actions | NuGet push key (or GitHub Packages PAT). |
| `GITHUB_TOKEN` | provided automatically | tag + GH release. |

semantic-release runs `@semantic-release/exec` which:

1. Computes the next version from conventional commits.
2. Runs `dotnet pack ... -p:Version=<next>` for both `Norbix.Sdk` and `Norbix.Sdk.Types`.
3. Runs `dotnet nuget push *.nupkg --skip-duplicate`.
4. Pushes a tag and writes `CHANGELOG.md` back to the branch.

## Repo layout

```
.github/workflows/         CI / release / security workflows
.config/dotnet-tools.json  pins the `x` ServiceStack CLI version
scripts/                   sync-types.sh + sync-types.ps1
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
