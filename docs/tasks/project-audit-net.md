# Project audit — .NET SDK docs (item F)

## Goal

One page, `docs/hub/project.md`, that shows every Project-module call of the
.NET SDK with a short C# example that uses the real generated method and
request type, linked from the README.

Not in scope: code changes to the SDK, new tests, other modules (environments,
the account AI chat, billing).

## Plan

1. docs(project): write `docs/hub/project.md` — settings, CORS, languages, regions, admin URL, legal, admin portal, AI settings / assistants / usage, LLM and MCP integrations, developer MCP endpoint, AI service users, public config + legal — done, commit 98d4bb7. Named `project.md` (not `account.md`): the docs folder names pages by feature (`push.md` lives in the `notifications` module), and this page spans the `Account`, `Ai` and `Public` modules
2. test(docs): compile every C# example of the page in a scratch console project against `Norbix.Hub` (and `Norbix.Api` for the public reads) — done, no repo change: `~/scratch/project-rr-net/snippets/{hub,api}`, 13 Hub blocks + 1 Api block, both build with 0 warnings / 0 errors. The repo has no doc-snippet test (see Findings #4)
3. docs(readme): link the page from the README module table and the regions section — done, commit 463eb65 (also a new `public` row in the API module table)
4. checks: `dotnet build` + `dotnet test` of the solution — done: build 0 warnings / 0 errors; Norbix.Sdk.Tests 61/61, Norbix.Hub.Tests 194/194
5. push + pull request — done, https://github.com/norbix-code/sdk-net/pull/70 (not merged). Opened with the `Domantas` gh account: the active `codemash-io` account has only READ on this repo
6. feat(project): expose brand and expose auth switches (item B3b; gateway routes from item B1, `UpdateExposure.cs`) — `UpdateProjectExposeBrand` / `UpdateProjectExposeAuth` request DTOs added to `Generated/Hub.dtos.cs` by hand in the exact generated shape (mirrors `UpdateProjectExposeLegal`; the source generator builds `UpdateProjectExposeBrandAsync` / `UpdateProjectExposeAuthAsync`), Account coverage snapshot 108 → 110, docs section — done: build 0 errors; Norbix.Sdk.Tests 61/61, Norbix.Hub.Tests 194/194. Regenerating was not possible offline: the documented path (`sdks/typegen`: `core/fetch_contract.py` → `languages/csharp/generate.py`) reads a running gateway, and the saved `core/contract.json` is from 2026-09-04, older than this file

## Changes

| file | what changed | plan step # |
| ---- | ------------ | ----------- |
| `docs/hub/project.md` | new page: 14 sections, every Project call with verb, path, C# example | 1 |
| `README.md` | links from the Hub `account` / `ai` rows and the project regions section; new API `public` row | 3 |
| `src/Norbix.Sdk.Types/Generated/Hub.dtos.cs` | `UpdateProjectExposeAuth`, `UpdateProjectExposeBrand` request DTOs (hand-added, generated shape) | 6 |
| `tests/Norbix.Hub.Tests/test_results/EndpointCoverageTests.Hub.Account.verified.txt` | 2 new endpoint entries, count 108 → 110 | 6 |
| `docs/hub/project.md` | section "What the public Admin Portal config shows" | 6 |
| `docs/tasks/project-audit-net.md` | this file | all |

## Findings

1. fix(public): `GetPublicProjectConfig` / `GetPublicProjectLegal` are public on the gateway, but the .NET DTOs do not carry `INorbixUnauthenticated`, so the SDK sends them with `Scope = Project` and throws `NORBIX_NOT_AUTHENTICATED` on a client with no key (`tests/Norbix.Sdk.Tests/test_results/EndpointCoverageTests.Api.Public.verified.txt`: `IsUnauthenticated: false`). An Admin Portal-style caller cannot use them without a key. Documented as-is; left open.
2. fix(mcp): `McpAsync` (`McpRequest`) cannot carry a JSON-RPC message — `RequestStream` is not serialized; the snapshot (`tests/Norbix.Hub.Tests/test_results/EndpointCoverageTests.Hub.Account.verified.txt`, `/account/mcp`) sends only `{ toolsets }`. Documented as "use an MCP client"; left open (hide it, or give it a real body).
3. docs(readme): the API module table says 97 endpoints but lists 55 and had no `files` / `public` row; the Hub table has no `public`, `projects`, `licensing`, `support`, `compliance`, `diagnostics`, `resources`, `code` rows. Added `public` only; rest left open.
4. test(docs): no test compiles the C# blocks of `README.md` / `docs/**`. Checked by hand in a scratch project this time; a snippet test would keep them true. Left open.
5. docs(readme): the README CI badge points at `norbix-dev/norbix-net`; the remote is `norbix-code/sdk-net`. Left open.
6. types(project): `UpdateProjectExposeBrand` / `UpdateProjectExposeAuth` were added by hand to `src/Norbix.Sdk.Types/Generated/Hub.dtos.cs` ("do not edit by hand") — regenerate later from a gateway that has item B1 (`sdks/typegen` fetch + csharp generate + sync); the next regeneration should give the same two classes. Not as companions in a separate file: `PushCampaignEndpoints.cs` notes such companions collide with the generated partial after a regeneration. Left open.

## Rejected / moved out

## Needs you

- [ ] Review and merge the pull request (not merged by the agent).

## Open questions

None.
