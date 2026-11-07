# Persistent planning references (`/gsd-reference add`, `/gsd-phase --ref`)

**Source:** [#5237](https://github.com/open-gsd/gsd-core/issues/5237)
**Decision:** wontfix — No-go as filed
**Date:** 2026-10-06

## Proposal summary

Reporter proposed a new planning concept: a `/gsd-reference add <path>` command that imports
external `.md` planning material (a file or directory, optionally filtered by `--include` or a
natural-language prompt) as an immutable snapshot under `.planning/references/` with a stable
`REF-NNN` identifier, a `/gsd-phase "..." --ref REF-NNN` flag that binds references when the
phase is created, and `/gsd-plan-phase` changes that feed bound references to the researcher,
planner and plan-checker. It also proposed a global `references.ask_name` preference in
`~/.gsd/defaults.json` and a new `src/references.cts` module.

## Why GSD does not own this

- **External documents already enter planning through shipped surfaces.** `/gsd-plan-phase`
  accepts `--prd <file>` and `--ingest <path-or-glob>`, and `/gsd-ingest-docs` imports document
  sets and derives phases and requirements from them. The issue weighs `--prd` and phase
  `CONTEXT.md` canonical references as alternatives but does not address `--ingest` or
  `/gsd-ingest-docs`.
- **The residual gap is narrow and the proposed cost is not.** What the shipped surfaces lack is
  a stable identifier and a binding made at phase creation. Closing that gap was proposed as a
  new command, a new storage module, `/gsd-phase` router and `add-phase` changes, a phase
  binding artifact, plan-phase and three agent dispatch changes, a new config namespace, docs
  and an ADR, which is a new persistent concept and a forever contract.
- **Natural-language file selection is non-deterministic**, which sits badly beside the
  snapshot-and-provenance guarantees the feature is meant to provide.

Not grounds for rejection: the proposal's problem statement is real, the opt-in and additive
shape is sound, and its snapshot-not-live-link and path-confinement requirements are the right
ones.

## What this does NOT cover

This entry denies a new first-class **reference** concept with its own command and store. It
does not deny:

- Improving `--prd`, `--ingest` or `/gsd-ingest-docs` for externally prepared plans.
- A small, additive way to attach existing ingested material to a phase at creation time.
- Documentation of how externally prepared planning material enters a project today.

## Re-open criteria

A narrower proposal is welcome when it names the specific workflow that `--prd`, `--ingest`
and `/gsd-ingest-docs` fail to serve, and it reuses their import machinery rather than adding a
parallel store and command.

## Related

- `commands/gsd/ingest-docs.md`
- `commands/gsd/plan-phase.md` — `--prd` and `--ingest`
- [#5237](https://github.com/open-gsd/gsd-core/issues/5237)
