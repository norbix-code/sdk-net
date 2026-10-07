# schema-content — .NET SDK (`norbix-net`), branch `audit/schema-content`

This file: /Users/djovaisas/Projects/norbix/worktrees/norbix-net/audit/schema-content/docs/tasks/schema-content.md
Gateway campaign: /Users/djovaisas/Projects/norbix/worktrees/gateway/audit/schema-content/campaign/docs/tasks/schema-content.md (branch `audit/schema-content`, NOT on `refactoringV2` yet).
Rules: /Users/djovaisas/Projects/norbix/worktrees/gateway/audit/schema-content/campaign/docs/tasks/sdk-management.md.

## Goal
Bring the .NET SDK to the schema-content contract of the gateway campaign branch: regenerate the two DTO files from the campaign hosts, type what the contract typed (schema field DTOs with their `$fieldType` discriminator, the `{ id, display }` reference value), cover the new request members and routes (`ExpandReferences`, `ArrayFilters`, files by id) with fake-transport tests, and document the new error codes — on a pull request that is NOT merged until the campaign lands.

Not in scope: merging (the gateway campaign is not on `refactoringV2`); typed record documents (records stay extended-JSON strings by design); other SDKs; the CLI.

## Plan
1. [done] chore(types): regenerate `Api.dtos.cs` / `Hub.dtos.cs` from the campaign hosts with a committed, repeatable script (`scripts/sync-types.py`, replaces the hand steps of 2026-09); accept the 4 coverage snapshots whose only change is the contract (new members / routes).
2. [done] feat(database): typed schema fields — a `$fieldType` converter on `JsonSchemaFieldDto` (Api + Hub) so a schema read yields `StringFieldDto` / `ObjectFieldDto` / `ArrayFieldDto` / `JsonFieldDto` … instead of the base class; unknown or missing discriminator → base class (never a throw).
3. [done] feat(database): reference expansion — `ExpandReferences` on find / find one / find own (Api) and find records / find one record (Hub) proven on the wire; `ReferenceValue { Id, Display }` reads an expanded value out of a record (`JsonElement`); CM-ERRORS-DATABASE-056 refusal surfaces with its code.
4. [done] feat(database): nested documents + `ArrayFilters` on update one / update many (Api + Hub): dotted paths, `$[]`, `$[name]` with `ArrayFilters` go out as sent; CM-ERRORS-DATABASE-014 / -039 / -040 / -046 / -047 refusals surface with their code; a nested filter / dotted `SortBy` travels unchanged.
5. [done] feat(files): file by id — `GetFileByIdAsync` (Api `GET /{version}/files/{filesIntegrationId}/by-id/{id}`, Hub `GET /{version}/files/item/by-id`) tests.
6. [done] docs: README section for the schema-content contract (expand references, nested documents + array filters, file by id, typed schema fields, term slug) + the new error codes 039–056, SCHEMA-036–041, TAXONOMIES-012/013; CONTRIBUTING regeneration steps point at the script.
7. [doing] full suite green (83 + 229, Release build `-warnaserror` clean); `nbx-ship --no-merge`; PR URL in this file.

## Changes
| file | what changed | plan step # |
|---|---|---|
| scripts/sync-types.py | NEW — runs `x csharp <url> <Api\|Hub>`, flattens to one namespace, renames the upstream markers, drops server-only attributes, carries the auth markers over from the previous file | 1 |
| src/Norbix.Sdk.Types/Generated/Api.dtos.cs, Hub.dtos.cs | regenerated: `ExpandReferences` (find / find one / find own; Hub find records / find one record), `ArrayFilters` (update one / many, both surfaces), `GetFileById[Request]` + `GetFileByIdResponse`, `ObjectFieldDto` / `ArrayFieldDto` / `JsonFieldDto` / `CurrencyDefaultDto`, `Default` / `Unique` / `DisplayField` / `MultipleOf` / `Minimum` / `Maximum` / `MinItems` / `MaxItems` / `AllowedFileType` / `MaxSizeMb`, `TermDto.Slug` / `TermTreeDto.Slug`, doc comments | 1 |
| tests/Norbix.Sdk.Tests/test_results/EndpointCoverageTests.Api.{Database,Files}.verified.txt, tests/Norbix.Hub.Tests/test_results/EndpointCoverageTests.Hub.{Database,Files}.verified.txt | accepted: `expandReferences=True` in the find queries, `arrayFilters` in the update bodies, the by-id route (Files count 12→13 / 22→23), `Slug` on the term sample | 1 |
| src/Norbix.Sdk.Types/SchemaFieldEndpoints.cs (Api), SchemaFieldEndpoints.Hub.cs (Hub) | NEW — `[JsonConverter]` on the partial `JsonSchemaFieldDto` + the `$fieldType` → type map (17 kinds) | 2 |
| src/Norbix.Sdk.Types/SchemaFieldJsonConverter.cs (shared, Norbix.Contracts) | NEW — the generic discriminator converter | 2 |
| src/Norbix.Sdk.Types/ReferenceValue.cs (shared, Norbix.Contracts) | NEW — `ReferenceValue { Id, Display, RawDisplay }` + `TryRead(JsonElement)` / `ReadMany` | 3 |
| tests/Norbix.Sdk.Tests/SchemaContentTests.cs (17 tests), tests/Norbix.Hub.Tests/SchemaContentTests.cs (11 tests) + 28 `SchemaContentTests.*.verified.txt` | NEW — one test per contract line, Verify snapshots (typed fields, expand references + 056, nested documents + ArrayFilters + 014 / 039 / 040 / 046 / 047 / 053, files by id, term slug + TAXONOMIES-012) | 2–5 (one commit `[4][5]`) |
| README.md | NEW section "Nested forms, reference expansion, files by id" (typed schema read, nested documents + `ArrayFilters`, `ReferenceValue`, file by id, term slug, behaviour changes, 24 error-code rows); module counts 97→98 / 525→526, Api `files` row (13), Hub `files` 22→23; sync section points at the script | 6 |
| CONTRIBUTING.md | "Regenerate the DTO files": the script, what it does, how to read the diff and the coverage snapshots | 6 |

## Findings
- F1 (open, SDK transport): a `PagingArgs` request property is still written to the query string as its type name (`pagingArgs=Norbix.Sdk.Types.Api.PagingArgs`) — visible in `EndpointCoverageTests.Api.Database.verified.txt`. The 2026-09 regeneration commit removed the same defect for `resolvedEnv` / `paging`; this one remains. A caller who pages with `PagingArgs` gets the default page. Not touched here (pre-existing, outside the contract).
- F2 (open, gateway): `JsonSchemaFieldDto` in `src/Isidos.CodeMash.Gateway.Contracts/Database/Schemas/JsonSchemaFieldDto.cs` carries `[JsonPolymorphic]` / `[JsonDerivedType]`, which `.claude/rules/dto-json.md` bans ("a Roslyn analyzer gates against it"). Either the rule has an exemption for Contracts or the analyzer does not cover that project. Recorded, not changed.
- F3 (note): the ServiceStack exporter emits a bool query member as `True` (`expandReferences=True`), like every other bool this SDK sends; the gateway parses it. Not a change.
- F4 (open, gateway, from cloud item refs-cloud-2 F12): a taxonomy term's expanded `display` can be the raw `name` language map, not a string. `ReferenceValue.Display` is therefore `string?` (null for a non-string) and `RawDisplay` keeps the element. When the gateway returns one string, `RawDisplay` is redundant.
- F5 (note): the Hub `GetFileById` request has no `[DataContract]` and its members no `[DataMember]`, unlike the Api twin — the gateway Hub DTO is written without them. No effect on the client (the transport reflects over public properties).
- F7 (open, SDK transport): a non-nullable `bool` request member is always written to the query string, also when false (`expandReferences=False` in `SchemaContentTests.FindWithoutExpand.verified.txt`). Harmless (the gateway parses it) but every plain-bool member behaves so; a `bool?` on the gateway DTO or a transport rule "skip default-valued value types" would quiet it. Not touched here.
- F6 (note): `sdks/typegen/languages/csharp/` has no `generate.py` (only an empty README / templates); `sdk-management.md` points at a path that does not exist. The regeneration recipe for .NET is the `x` tool + the post-processing that `scripts/sync-types.py` now holds in this repo. Worth fixing the pointer in `sdk-management.md` when the campaign closes.

## Rejected / moved out
- Typed record documents / typed `Find*` results: the record body is extended-JSON text by SDK design (`Documents = "[…]"`, `FindResponse.List` of `object`). Only the expanded reference value gets a type (`ReferenceValue`). Reason: the schema is per collection and only known at run time.
- Convenience overloads (`FindAsync(collection, expandReferences: true)`): the SDK's public shape is the generated request DTOs; adding a second shape is a public-surface change that rule 4 of `sdk-management.md` reserves for the owner. The flag is a request member, which is enough.

## Needs you
- [ ] Merge after the gateway campaign `audit/schema-content` is on `refactoringV2` — the PR is opened with `nbx-ship --no-merge` and left open on purpose. Order: SDKs first, `cli` last.

## Open questions
- none
