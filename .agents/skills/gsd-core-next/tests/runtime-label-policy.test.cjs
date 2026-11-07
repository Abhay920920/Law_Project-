'use strict';
/**
 * getRuntimeLabel is the SINGLE source of truth for the short install/uninstall
 * console display label, replacing the two duplicated `runtimeLabel` assignment
 * chains that previously lived in bin/install.js (uninstall() and install()) —
 * the add-a-host tax ADR-1239 Phase B (#1679) eliminates.
 *
 * Because 1.7.0 (ADR-1016 / ADR-1239) makes runtimes pluggable data, the label
 * table is CURATED (a runtime id → short label mapping that cannot be derived
 * from the id alone, e.g. "Claude Code", "Qwen Code", "ZCode"). This test
 * enforces the COVERAGE CONTRACT rather than a frozen per-runtime snapshot:
 *
 *   - every runtime in the capability registry MUST resolve to a distinct,
 *     non-default curated label (a newly-added runtime that forgets to add a
 *     label entry silently falls through to "Claude Code" and fails here);
 *   - the fail-closed fallback returns "Claude Code" for unknown / empty / alias
 *     inputs (raw-id match only — aliases are NOT auto-expanded).
 *
 * Adding a runtime descriptor requires adding its label to RUNTIME_LABELS (the
 * deliberate curation step); it does NOT require editing a count or golden
 * snapshot here.
 *
 * Voice: these SHORT UI labels are intentionally distinct from the descriptor
 * `title` (the long product name). Two prior-chain inconsistencies are resolved
 * by the canonical map: kimi → 'Kimi CLI'; cline → 'Cline'.
 *
 * ADR-1239 Phase B (#1679). Behavioral tests only: assert on returned values.
 */

const { test } = require('node:test');
const assert = require('node:assert/strict');
const runtimeNamePolicy = require('../gsd-core/bin/lib/runtime-name-policy.cjs');
const registry = require('../gsd-core/bin/lib/capability-registry.cjs');

const { getRuntimeLabel } = runtimeNamePolicy;

const FALLBACK = 'Claude Code';
const RUNTIME_IDS = Object.keys(registry.runtimes);

test('getRuntimeLabel: every registry runtime resolves to a non-empty curated label (coverage contract, count-agnostic)', () => {
  assert.ok(RUNTIME_IDS.length > 0, 'registry must contain at least one runtime');
  for (const id of RUNTIME_IDS) {
    const label = getRuntimeLabel(id);
    assert.strictEqual(typeof label, 'string', `getRuntimeLabel('${id}') must be a string`);
    assert.ok(label.length > 0, `getRuntimeLabel('${id}') must be non-empty`);
  }
});

test('getRuntimeLabel drift guard: no registry runtime except claude falls through to the default (forces a deliberate label per runtime)', () => {
  // claude's curated label IS the fallback string, so it is exempt. Every other
  // registry runtime must resolve to a DISTINCT label — otherwise it was added
  // without a RUNTIME_LABELS entry and is silently masking as "Claude Code".
  for (const id of RUNTIME_IDS) {
    if (id === 'claude') continue;
    assert.notStrictEqual(
      getRuntimeLabel(id),
      FALLBACK,
      `registry runtime '${id}' resolved to the fallback "${FALLBACK}" — add a distinct entry to RUNTIME_LABELS in src/runtime-name-policy.cts`);
  }
});

test('getRuntimeLabel: an absent id is the generic path; an unknown or alias id REFUSES (#5169, raw-id match only)', () => {
  // An absent id (`''`) is "no runtime selected" and keeps the documented default.
  assert.strictEqual(getRuntimeLabel(''), FALLBACK);
  // A non-empty id that is not a registered runtime used to fall through to
  // "Claude Code" silently; ADR-5057 §5 makes every descriptor accessor refuse it.
  assert.throws(() => getRuntimeLabel('unknown'), { name: 'UnknownRuntimeError' });
  assert.throws(() => getRuntimeLabel('claude-code'), { name: 'UnknownRuntimeError' },
    'getRuntimeLabel("claude-code") must refuse (raw-id match only; aliases are not expanded)');
});

// The per-runtime `/gsd-new-project` next-step command is no longer a label-
// policy table: it is generated from the registered trigger surface and pinned
// by tests/advertised-command-parity.test.cjs (#5215, ADR-5057 Phase 12).
