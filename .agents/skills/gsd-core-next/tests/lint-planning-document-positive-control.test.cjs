/**
 * Tests for the PlanningDoc positive-control lint (ADR-4910 §7, epic #4906
 * Phase 6 / #5007) — `scripts/lint-planning-document-positive-control.cjs`.
 *
 * Mirrors the structure of `tests/lint-planning-artifact-writer-drift.test.cjs`
 * and `tests/lint-table-schema-drift.cjs`'s sibling scripts: pure-function
 * unit checks with in-memory fakes, plus one real-tree regression test that
 * imports and calls `scanRepo` in-process, plus (unique to this guard) a
 * red-first child-process proof that the lint actually enforces rather than
 * only documents the requirement.
 */

'use strict';

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const { runNode } = require('./helpers/process-seam.cjs');
const { PROBE_TIMEOUT_MS } = require('./helpers/timeouts.cjs');
const lint = require('../scripts/lint-planning-document-positive-control.cjs');
const {
  readLiveGrammarKinds,
  findPositiveControlGaps,
  scanRepo,
  POSITIVE_CONTROL_REGISTRY,
  FIXTURE_ARTIFACT_NAME,
  FIXTURE_DIR,
  TEST_REGISTRY_OVERRIDE_ENV_VAR,
  TEST_REGISTRY_OVERRIDE_CLI_FLAG,
} = lint;

const REPO_ROOT = path.join(__dirname, '..');
const SCRIPT_PATH = path.join(REPO_ROOT, 'scripts', 'lint-planning-document-positive-control.cjs');

// Documented baseline (ADR-4910 §7 / #5007 Phase 6): the grammar count
// `NodeKind` (src/planning-document.cts) declared when this lint was
// written — frontmatter, section, boldField, table, checklist. The
// "registry completeness" tests below assert off the LIVE set read from
// source at test time, never off this constant; this exists only to give a
// human a concrete number to notice drifting. When `NodeKind` grows, update
// this constant AND add a registry entry + independently-authored fixture —
// the completeness test will otherwise (correctly) start failing on its own.
const DOCUMENTED_BASELINE_GRAMMAR_COUNT = 5;

function realReadFile(relPath) {
  try {
    return fs.readFileSync(path.join(REPO_ROOT, relPath), 'utf8');
  } catch {
    return null;
  }
}

// ─── Case 1: the live NodeKind union reads correctly off real source ───────

describe('readLiveGrammarKinds — reads the real NodeKind union', () => {
  test('finds the declared grammar kinds in src/planning-document.cts', () => {
    const live = readLiveGrammarKinds(realReadFile);
    assert.equal(live.ok, true, live.ok ? '' : live.reason);
    assert.ok(Array.isArray(live.value));
    assert.ok(live.value.length > 0);
    assert.equal(
      live.value.length,
      DOCUMENTED_BASELINE_GRAMMAR_COUNT,
      `NodeKind grammar count changed to ${live.value.length} (was ${DOCUMENTED_BASELINE_GRAMMAR_COUNT}); ` +
        'update DOCUMENTED_BASELINE_GRAMMAR_COUNT and confirm every new grammar also has a ' +
        'POSITIVE_CONTROL_REGISTRY entry plus an independently-authored fixture',
    );
  });

  test('reports a clear failure when the source file cannot be read', () => {
    const live = readLiveGrammarKinds(() => null);
    assert.equal(live.ok, false);
    assert.match(live.reason, /cannot read/);
  });
});

// ─── Case 2: registry completeness — every live grammar has an entry ──────

describe('registry completeness — every live grammar has a positive-control entry', () => {
  test('POSITIVE_CONTROL_REGISTRY has an entry for every declared NodeKind member', () => {
    const live = readLiveGrammarKinds(realReadFile);
    assert.equal(live.ok, true);
    for (const kind of live.value) {
      assert.ok(
        Object.prototype.hasOwnProperty.call(POSITIVE_CONTROL_REGISTRY, kind),
        `POSITIVE_CONTROL_REGISTRY is missing an entry for live grammar '${kind}'`,
      );
    }
  });

  test('no registry entry names a grammar the parser no longer declares (stale entry)', () => {
    const live = readLiveGrammarKinds(realReadFile);
    assert.equal(live.ok, true);
    for (const kind of Object.keys(POSITIVE_CONTROL_REGISTRY)) {
      assert.ok(
        live.value.includes(kind),
        `POSITIVE_CONTROL_REGISTRY entry '${kind}' names a grammar not in the live NodeKind union`,
      );
    }
  });
});

// ─── Case 3: fixtures exist and round-trip through the real parser ────────

describe('fixtures — each round-trips through the real parser and proves its grammar', () => {
  test('scanRepo reports zero violations against the real repo tree', () => {
    const violations = scanRepo(REPO_ROOT, {});
    assert.deepStrictEqual(
      violations,
      [],
      `unexpected positive-control gap(s): ${JSON.stringify(violations, null, 2)}`,
    );
  });

  test('every registered fixture file exists and yields a node of its declared kind', () => {
    const seam = require(path.join(REPO_ROOT, 'gsd-core', 'bin', 'lib', 'planning-document.cjs'));
    for (const [kind, fixtureName] of Object.entries(POSITIVE_CONTROL_REGISTRY)) {
      const fixturePath = path.join(REPO_ROOT, FIXTURE_DIR, fixtureName);
      assert.ok(fs.existsSync(fixturePath), `fixture missing: ${fixturePath}`);
      const content = fs.readFileSync(fixturePath, 'utf8');
      const result = seam.parsePlanningDoc(content, FIXTURE_ARTIFACT_NAME);
      assert.equal(result.ok, true, result.ok ? '' : result.reason);
      const kinds = result.value.nodes.map((n) => n.kind);
      assert.ok(
        kinds.includes(kind),
        `fixture ${fixtureName} did not produce a '${kind}' node (got: ${kinds.join(', ') || 'none'})`,
      );
    }
  });
});

// ─── Case 4: findPositiveControlGaps — pure-function unit checks ──────────

describe('findPositiveControlGaps — pure-function unit checks (boundary cases)', () => {
  test('flags a live grammar with no registry entry at all', () => {
    const violations = findPositiveControlGaps(
      ['frontmatter', 'section'],
      { frontmatter: 'frontmatter.md' },
      () => 'irrelevant',
      () => ({ ok: true, value: { nodes: [{ kind: 'frontmatter' }] } }),
      'ROADMAP.md',
    );
    assert.equal(violations.length, 1);
    assert.equal(violations[0].kind, 'section');
    assert.match(violations[0].reason, /no entry for it/);
  });

  test('flags a registry entry whose fixture file is missing', () => {
    const violations = findPositiveControlGaps(
      ['frontmatter'],
      { frontmatter: 'does-not-exist.md' },
      () => null,
      () => ({ ok: true, value: { nodes: [] } }),
      'ROADMAP.md',
    );
    assert.equal(violations.length, 1);
    assert.match(violations[0].reason, /not found or unreadable/);
  });

  test('flags a fixture that fails to parse', () => {
    const violations = findPositiveControlGaps(
      ['frontmatter'],
      { frontmatter: 'frontmatter.md' },
      () => 'bad content',
      () => ({ ok: false, reason: 'synthetic parse failure' }),
      'ROADMAP.md',
    );
    assert.equal(violations.length, 1);
    assert.match(violations[0].reason, /synthetic parse failure/);
  });

  test('flags a fixture that parses but never produces the declared kind', () => {
    const violations = findPositiveControlGaps(
      ['table'],
      { table: 'table.md' },
      () => 'not actually a table',
      () => ({ ok: true, value: { nodes: [{ kind: 'section' }] } }),
      'ROADMAP.md',
    );
    assert.equal(violations.length, 1);
    assert.match(violations[0].reason, /produced no 'table' node/);
  });

  test('flags a stale registry entry for a grammar the parser no longer declares', () => {
    const violations = findPositiveControlGaps(
      ['frontmatter'],
      { frontmatter: 'frontmatter.md', ghost: 'ghost.md' },
      () => 'x',
      () => ({ ok: true, value: { nodes: [{ kind: 'frontmatter' }] } }),
      'ROADMAP.md',
    );
    assert.equal(violations.length, 1);
    assert.equal(violations[0].kind, 'ghost');
    assert.match(violations[0].reason, /stale entry/);
  });

  test('reports zero violations for a fully-covered, non-stale registry', () => {
    const violations = findPositiveControlGaps(
      ['frontmatter'],
      { frontmatter: 'frontmatter.md' },
      () => 'irrelevant',
      () => ({ ok: true, value: { nodes: [{ kind: 'frontmatter' }] } }),
      'ROADMAP.md',
    );
    assert.deepStrictEqual(violations, []);
  });
});

// ─── Case 5: red-first — the lint actually enforces, not just documents ───
//
// Spawns the REAL, otherwise-unmodified, script file as its own child
// process — never a mutated copy, never touching the real registry or
// fixture files — using the script's supported test-only override hook
// (TEST_REGISTRY_OVERRIDE_ENV_VAR, gated by the TEST_REGISTRY_OVERRIDE_CLI_FLAG
// argv flag this test also passes) to substitute a deliberately broken
// registry entry, proving the exit-code/stderr contract is load-bearing.
//
// The CLI flag is required alongside the env var: `resolveRegistry` only
// reads the env var at all when `scanRepo`/`main` is told `allowOverride ===
// true`, which only happens via this argv flag (#5007 Phase 6 fix — see
// TEST_REGISTRY_OVERRIDE_ENV_VAR's header comment in the script for why an
// env var alone is not a sufficient gate for a real child-process
// invocation).

describe('red-first: the lint script (spawned as a real child process) actually enforces', () => {
  test('control: the real script passes clean with no override set', () => {
    const result = runNode([SCRIPT_PATH], { cwd: REPO_ROOT, timeoutMs: PROBE_TIMEOUT_MS });
    assert.equal(
      result.exitCode,
      0,
      `expected the real script to pass; got ${result.exitCode} (${result.outcome}). stdout: ${result.stdout} stderr: ${result.stderr}`,
    );
  });

  test('the override env var alone (no CLI flag) is IGNORED — proves the override cannot leak into a real invocation', () => {
    const result = runNode([SCRIPT_PATH], {
      cwd: REPO_ROOT,
      timeoutMs: PROBE_TIMEOUT_MS,
      env: {
        ...process.env,
        [TEST_REGISTRY_OVERRIDE_ENV_VAR]: JSON.stringify({ table: 'does-not-exist-nonexistent-fixture.md' }),
      },
    });
    assert.equal(
      result.exitCode,
      0,
      `expected the override to be ignored without --test-registry-override, and the real script to pass clean; ` +
        `got ${result.exitCode} (${result.outcome}). stdout: ${result.stdout} stderr: ${result.stderr}`,
    );
  });

  test('a fixture path pointed at a nonexistent file fails the build (non-zero exit, clear message)', () => {
    const result = runNode([SCRIPT_PATH, TEST_REGISTRY_OVERRIDE_CLI_FLAG], {
      cwd: REPO_ROOT,
      timeoutMs: PROBE_TIMEOUT_MS,
      env: {
        ...process.env,
        [TEST_REGISTRY_OVERRIDE_ENV_VAR]: JSON.stringify({ table: 'does-not-exist-nonexistent-fixture.md' }),
      },
    });
    assert.notEqual(
      result.exitCode,
      0,
      `expected a non-zero exit; got ${result.exitCode} (${result.outcome}). stdout: ${result.stdout} stderr: ${result.stderr}`,
    );
    assert.match(result.stderr, /table/);
    assert.match(result.stderr, /not found or unreadable/);
  });

  test('removing a registry entry entirely fails the build, naming the uncovered grammar', () => {
    // JSON cannot express "delete a key by merging", so this override
    // instead names a grammar that IS live but is deliberately left with an
    // empty-string fixture name -- `resolveRegistry`'s merge still leaves
    // every OTHER real key intact, and an empty string is falsy, so
    // `findPositiveControlGaps` takes the exact same "no entry for it"
    // branch a truly-removed key would.
    const result = runNode([SCRIPT_PATH, TEST_REGISTRY_OVERRIDE_CLI_FLAG], {
      cwd: REPO_ROOT,
      timeoutMs: PROBE_TIMEOUT_MS,
      env: {
        ...process.env,
        [TEST_REGISTRY_OVERRIDE_ENV_VAR]: JSON.stringify({ checklist: '' }),
      },
    });
    assert.notEqual(
      result.exitCode,
      0,
      `expected a non-zero exit; got ${result.exitCode} (${result.outcome}). stdout: ${result.stdout} stderr: ${result.stderr}`,
    );
    assert.match(result.stderr, /checklist/);
    assert.match(result.stderr, /no entry for it/);
  });
});
