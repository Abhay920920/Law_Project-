#!/usr/bin/env node
'use strict';

/**
 * PlanningDoc positive-control lint (ADR-4910 §7, "Every parser ships a
 * positive control per accepted grammar", epic #4906 Phase 6 / #5007).
 *
 * `NodeKind` (`src/planning-document.cts`, compiled to
 * `gsd-core/bin/lib/planning-document.cjs`) is the single-source union naming
 * every grammar `parsePlanningDoc` accepts: `frontmatter` / `section` /
 * `boldField` / `table` / `checklist`. This lint asserts one positive-control
 * fixture per declared grammar, under `tests/fixtures/planning-document/`,
 * each of which must round-trip through the real `parsePlanningDoc` and
 * actually produce a node of that kind — not merely "the parser didn't
 * throw". Per CONTRIBUTING.md's fixture-provenance rule (#2371), every
 * fixture here is authored independently, never copied from
 * `src/planning-document.cts`'s own docstring examples or from
 * `tests/planning-document.test.cjs`'s existing fixtures.
 *
 * The `accepted` set (`NodeKind`) is a TypeScript-only type, erased by
 * compilation — it carries no runtime value in the built `.cjs` seam. So this
 * lint reads it directly out of `src/planning-document.cts`'s own source
 * text (the `export type NodeKind = ...;` declaration), the same way
 * `scripts/lint-phase-id-drift.cjs` reads source text for its own literal
 * checks — NOT by importing a type, which is impossible from a `.cjs`
 * script. This is what makes the guarantee live: a NEW grammar kind added to
 * `NodeKind` with no registry entry fails this lint immediately, before any
 * fixture is even written, closing the "parser without one fails the build"
 * requirement in ADR-4910 §7.
 *
 * Modeled on `scripts/lint-table-schema-drift.cjs`'s registry/exit-code/
 * error-message structure — a hardcoded `{grammar -> fixture file}` map,
 * exit 0 clean / exit 1 with a per-entry reason on drift, wired into
 * `lint:ci` — but the ASSERTION is stricter than that script's verbatim-
 * substring check: proof the grammar is genuinely *accepted* (the fixture
 * parses without error and yields a node of that kind), per ADR-4910 §7's
 * own text, not just "a string appears somewhere".
 */

const fs = require('node:fs');
const path = require('node:path');

const PLANNING_DOCUMENT_SRC = path.join('src', 'planning-document.cts');
const PLANNING_DOCUMENT_LIB = path.join('gsd-core', 'bin', 'lib', 'planning-document.cjs');
const FIXTURE_DIR = path.join('tests', 'fixtures', 'planning-document');

// grammar kind (must match a `NodeKind` union member verbatim) -> the ONE
// canonical positive-control fixture file (relative to FIXTURE_DIR) that
// proves that grammar is accepted (ADR-4910 §7).
const POSITIVE_CONTROL_REGISTRY = {
  frontmatter: 'frontmatter.md',
  section: 'section.md',
  boldField: 'boldfield.md',
  table: 'table.md',
  checklist: 'checklist.md',
};

// Any real entry from `PLANNING_ARTIFACTS` satisfies `parsePlanningDoc`'s
// artifact-kind gate equally — the specific name does not affect which
// grammars a fixture's BODY is recognised as containing.
const FIXTURE_ARTIFACT_NAME = 'ROADMAP.md';

// Test-only env var name. `resolveRegistry` below JSON-parses this (when set)
// and shallow-merges it over `POSITIVE_CONTROL_REGISTRY`, so a test can spawn
// THIS SAME, otherwise-unmodified, script file as a real child process
// against a deliberately broken registry entry (a missing entry, or one
// pointed at a nonexistent fixture) — proving the lint genuinely fails the
// build, per ADR-4910 §7's "a parser without one fails the build" guarantee
// — without ever touching the real registry or fixture files.
//
// This env var alone is NEVER sufficient to activate the override — a bare
// `process.env` lookup would mean a stray, accidentally-inherited copy of
// this variable (a leftover shell `export`, a misconfigured CI environment,
// a compromised dependency's postinstall script) could silently swap the
// registry in a REAL `npm run lint:ci` / `npm run lint:planning-document-
// positive-control` invocation, with nothing in the code enforcing otherwise
// (flagged by both the security review and the code-review Standards axis
// on #5007 Phase 6: the old comment asserted "no real invocation ever sets
// this" as a convention, not a fact the code made true).
//
// What now makes it true, by construction: `scanRepo` only reads this env
// var via `resolveRegistry` when its caller ALSO passes `allowOverride ===
// true`, which `main()` only does when the `--test-registry-override` CLI
// flag is present in argv (see `parseArgs`). The test file spawns this
// script as a real child process (`tests/helpers/process-seam.cjs`'s
// `runNode`, over `spawnSync`) — a process boundary that can only carry data
// in via `env`/`argv`, so the override cannot be passed as an in-process
// function parameter here the way it can be for the pure functions below.
// Requiring BOTH the env var and the CLI flag closes the gap: neither
// `lint:ci` nor `lint:planning-document-positive-control` (see package.json)
// ever passes `--test-registry-override` in their fixed argv, and unlike an
// env var, a CLI flag is never accidentally "leaked" into a fixed npm-script
// invocation. See tests/lint-planning-document-positive-control.test.cjs.
const TEST_REGISTRY_OVERRIDE_ENV_VAR = 'LINT_PLANNING_DOCUMENT_POSITIVE_CONTROL_TEST_REGISTRY_OVERRIDE';

/** CLI flag gate for the test-only override (see `TEST_REGISTRY_OVERRIDE_ENV_VAR`
 * above). Must be passed explicitly in argv — never inferred from env — for
 * `resolveRegistry`'s env-var override to be honored at all. */
const TEST_REGISTRY_OVERRIDE_CLI_FLAG = '--test-registry-override';

/** Resolve the registry to scan against: the real hardcoded registry, unless
 * BOTH `allowOverride` is `true` (only ever set by the
 * `--test-registry-override` CLI flag, see `TEST_REGISTRY_OVERRIDE_CLI_FLAG`)
 * AND the test-only override env var is set and parses as JSON, in which
 * case its keys are shallow-merged on top (see
 * `TEST_REGISTRY_OVERRIDE_ENV_VAR`). Without `allowOverride === true`, the
 * env var is never even read. */
function resolveRegistry(env, allowOverride) {
  if (!allowOverride) return POSITIVE_CONTROL_REGISTRY;
  const override = env[TEST_REGISTRY_OVERRIDE_ENV_VAR];
  if (!override) return POSITIVE_CONTROL_REGISTRY;
  try {
    const parsed = JSON.parse(override);
    return { ...POSITIVE_CONTROL_REGISTRY, ...parsed };
  } catch {
    return POSITIVE_CONTROL_REGISTRY;
  }
}

/**
 * Extract the live `NodeKind` union members from `planning-document.cts`'s
 * own source text — the "accepted grammars" set (ADR-4910 §4a) — read
 * directly from source (never from the compiled seam, which erases the TS
 * type) so a newly added grammar kind is detected even before any registry
 * entry or fixture exists for it.
 *
 * Pure: takes a `readFile(relPath) -> string|null` accessor so tests can
 * inject a scratch source file without touching the real repo tree.
 */
function readLiveGrammarKinds(readFile) {
  const text = readFile(PLANNING_DOCUMENT_SRC);
  if (text == null) {
    return { ok: false, reason: `cannot read ${PLANNING_DOCUMENT_SRC}` };
  }
  const m = /export type NodeKind\s*=\s*([^;]+);/.exec(text);
  if (!m) {
    return {
      ok: false,
      reason: `could not find 'export type NodeKind = ...;' in ${PLANNING_DOCUMENT_SRC} — has the type been renamed or removed?`,
    };
  }
  const kinds = [...m[1].matchAll(/'([^']+)'/g)].map((mm) => mm[1]);
  if (kinds.length === 0) {
    return {
      ok: false,
      reason: `the 'NodeKind' union in ${PLANNING_DOCUMENT_SRC} matched but contained no string-literal members`,
    };
  }
  return { ok: true, value: kinds };
}

/**
 * Pure: given the live grammar-kind list, the hardcoded registry, a
 * `readFixture(fileName) -> string|null` accessor, the real `parsePlanningDoc`
 * function, and the artifact name to parse fixtures as, find every gap.
 * Returns `[{ kind, fixture?, reason }]`; empty when clean.
 *
 * Checks, per live grammar kind:
 *  1. a registry entry exists at all (the §7 "parser without one fails the
 *     build" guarantee — this fires even with an EMPTY registry);
 *  2. the registered fixture file exists and is readable;
 *  3. the fixture parses successfully via the real parser;
 *  4. the parsed document actually contains a node of that kind (proof the
 *     grammar was recognised, not just "didn't throw").
 * Also flags a registry entry naming a grammar the parser no longer
 * declares (a stale entry), so the registry cannot silently drift wider
 * than the parser either.
 */
function findPositiveControlGaps(liveKinds, registry, readFixture, parsePlanningDocFn, artifactName) {
  const violations = [];

  for (const kind of liveKinds) {
    const fixtureName = registry[kind];
    if (!fixtureName) {
      violations.push({
        kind,
        reason:
          `parser declares grammar '${kind}' (NodeKind) but POSITIVE_CONTROL_REGISTRY has no entry for it — ` +
          'add one and a matching fixture under tests/fixtures/planning-document/',
      });
      continue;
    }

    const content = readFixture(fixtureName);
    if (content == null) {
      violations.push({
        kind,
        fixture: fixtureName,
        reason: `fixture file not found or unreadable: ${path.join(FIXTURE_DIR, fixtureName)}`,
      });
      continue;
    }

    const result = parsePlanningDocFn(content, artifactName);
    if (!result.ok) {
      violations.push({
        kind,
        fixture: fixtureName,
        reason: `fixture failed to parse via parsePlanningDoc: ${result.reason}`,
      });
      continue;
    }

    const hasKind = result.value.nodes.some((n) => n.kind === kind);
    if (!hasKind) {
      violations.push({
        kind,
        fixture: fixtureName,
        reason: `fixture parsed successfully but produced no '${kind}' node — it does not actually exercise this grammar`,
      });
    }
  }

  for (const kind of Object.keys(registry)) {
    if (!liveKinds.includes(kind)) {
      violations.push({
        kind,
        fixture: registry[kind],
        reason: `registry entry names grammar '${kind}', which is not in the parser's live NodeKind union — stale entry, remove it`,
      });
    }
  }

  return violations;
}

/**
 * Load the built seam and the real source/fixture tree rooted at `root`, and
 * run `findPositiveControlGaps` against them. Returns the same shape as
 * `findPositiveControlGaps`. If the seam hasn't been built yet, or the
 * `NodeKind` union can't be located, reports a single actionable violation
 * rather than throwing.
 *
 * `allowOverride` (default `false`) must be explicitly `true` for the
 * test-only env-var registry override to be honored at all — see
 * `resolveRegistry` and `TEST_REGISTRY_OVERRIDE_ENV_VAR`'s header comment.
 */
function scanRepo(root, env, allowOverride = false) {
  const readFile = (relPath) => {
    try {
      return fs.readFileSync(path.join(root, relPath), 'utf8');
    } catch {
      return null;
    }
  };
  const readFixture = (fileName) => readFile(path.join(FIXTURE_DIR, fileName));

  const live = readLiveGrammarKinds(readFile);
  if (!live.ok) {
    return [{ kind: null, reason: live.reason }];
  }

  const libPath = path.join(root, PLANNING_DOCUMENT_LIB);
  let seam;
  try {
    seam = require(libPath);
  } catch (e) {
    return [
      {
        kind: null,
        reason: `cannot load the planning-document seam at ${path.relative(root, libPath)} — run 'npm run build:lib' first (${e.message})`,
      },
    ];
  }

  const registry = resolveRegistry(env || process.env, allowOverride);
  return findPositiveControlGaps(live.value, registry, readFixture, seam.parsePlanningDoc, FIXTURE_ARTIFACT_NAME);
}

/** Parse a bare `--root <path>` CLI override (defaults to the real repo root)
 * and the `--test-registry-override` flag that gates `resolveRegistry`'s
 * env-var override (see `TEST_REGISTRY_OVERRIDE_CLI_FLAG`'s header comment).
 * No real invocation (`npm run lint:ci`, `npm run lint:planning-document-
 * positive-control`) ever passes this flag. */
function parseArgs(argv) {
  let root = path.join(__dirname, '..');
  let allowTestRegistryOverride = false;
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--root' && argv[i + 1]) {
      root = path.resolve(argv[i + 1]);
      i += 1;
    } else if (argv[i] === TEST_REGISTRY_OVERRIDE_CLI_FLAG) {
      allowTestRegistryOverride = true;
    }
  }
  return { root, allowTestRegistryOverride };
}

function main() {
  const { root, allowTestRegistryOverride } = parseArgs(process.argv.slice(2));
  const violations = scanRepo(root, process.env, allowTestRegistryOverride);
  if (violations.length === 0) {
    process.stdout.write(
      'ok planning-document-positive-control: every declared PlanningDoc grammar has a passing positive-control fixture\n',
    );
    return;
  }
  process.stderr.write(
    'planning-document-positive-control: PlanningDoc grammar(s) missing or failing a positive-control fixture (ADR-4910 §7).\n',
  );
  process.stderr.write(
    'Add/fix a fixture under tests/fixtures/planning-document/ and register it in POSITIVE_CONTROL_REGISTRY in\n' +
      'scripts/lint-planning-document-positive-control.cjs — each fixture must round-trip through parsePlanningDoc\n' +
      'and yield a node of the declared kind.\n',
  );
  for (const v of violations) {
    const id = v.kind ?? '?';
    const loc = v.fixture ? ` (${path.join(FIXTURE_DIR, v.fixture)})` : '';
    process.stderr.write(`  ${id}${loc}: ${v.reason}\n`);
  }
  process.exitCode = 1;
}

if (require.main === module) main();

module.exports = {
  findPositiveControlGaps,
  readLiveGrammarKinds,
  resolveRegistry,
  scanRepo,
  parseArgs,
  POSITIVE_CONTROL_REGISTRY,
  FIXTURE_ARTIFACT_NAME,
  PLANNING_DOCUMENT_SRC,
  PLANNING_DOCUMENT_LIB,
  FIXTURE_DIR,
  TEST_REGISTRY_OVERRIDE_ENV_VAR,
  TEST_REGISTRY_OVERRIDE_CLI_FLAG,
};
