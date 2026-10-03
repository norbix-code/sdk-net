# Project audit — .NET SDK docs (item F)

## Goal

One page, `docs/hub/project.md`, that shows every Project-module call of the
.NET SDK with a short C# example that uses the real generated method and
request type, linked from the README.

Not in scope: code changes to the SDK, new tests, other modules (environments,
the account AI chat, billing).

## Plan

1. docs(project): write `docs/hub/project.md` — settings, CORS, languages, regions, admin URL, legal, admin portal, AI settings / assistants / usage, LLM and MCP integrations, developer MCP endpoint, AI service users, public config + legal — todo
2. test(docs): compile every C# example of the page in a scratch console project against `Norbix.Hub` (and `Norbix.Api` for the public reads) — todo
3. docs(readme): link the page from the README module table and the regions section — todo
4. checks: `dotnet build` + `dotnet test` of the solution — todo
5. push + pull request — todo

## Changes

| file | what changed | plan step # |
| ---- | ------------ | ----------- |

## Findings

## Rejected / moved out

## Needs you

## Open questions

None.
