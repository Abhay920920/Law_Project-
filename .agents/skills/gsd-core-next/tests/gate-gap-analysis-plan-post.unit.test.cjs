'use strict';

/**
 * U9 — in-process GateVerdict tests for `check gap-analysis-plan-post` (#5139, epic #5056, ADR-5057 §4).
 *
 * FAILING-FIRST (RED): `gsd-core/bin/lib/gate-gap-analysis-plan-post.cjs` does not exist yet, so this file
 * fails at require time on origin/next. The module must export
 * `evaluateGapAnalysisPlanPost({ projectDir, args })` where `args` is the argv AFTER the verb, returning a
 * GateVerdict ({ outcome, block, payload }) for every arm that prints a payload today and a
 * GateUsageFailure ({ failure: { code, message } }) for every arm that calls `error()` today.
 *
 * Every payload below was captured by EXECUTING the pre-move router (`gsd-tools check gap-analysis-plan-post`)
 * on origin/next in a fixture project; the tests compare the deep value AND the serialized key
 * order (stdout is byte-identical only if the payload's insertion order is preserved).
 * `outcome`/`block` are the new GateVerdict fields; their mapping from today's payload is
 * recorded per case (see 50-test-matrix.md, API contract).
 *
 * Each case also asserts the gate never writes to process.stdout / process.stderr: only the
 * router formats output (design D2).
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createTempProject, cleanup } = require('./helpers.cjs');
const { tempRootAliases, canonicalizeTempPaths } = require('./helpers/path-compare.cjs');

const gate = require('../gsd-core/bin/lib/gate-gap-analysis-plan-post.cjs');
const { isGateUsageFailure } = require('../gsd-core/bin/lib/gate-verdict.cjs');

function w(dir, rel, content) {
  const p = path.join(dir, rel);
  fs.mkdirSync(path.dirname(p), { recursive: true });
  fs.writeFileSync(p, content);
}

const h = { w };

const CASES = [
  {
    id: 'U9a',
    title: 'requirement and decision both cited by the plan -> advisory table, passed true',
    setup(dir, h) {
      h.w(dir, '.planning/REQUIREMENTS.md', ['# Requirements', '', '- [ ] **REQ-01**: Users can log in', ''].join('\n'));
      h.w(dir, '.planning/phases/01-x/01-CONTEXT.md', ['<decisions>', '- **D-01:** Use PostgreSQL for the primary datastore layer', '</decisions>', ''].join('\n'));
      h.w(dir, '.planning/phases/01-x/01-01-PLAN.md', '<objective>REQ-01 and D-01</objective>\n');
    },
    args() { return ['.planning/phases/01-x', 'REQ-01']; },
    outcome: 'advisory',
    block: false,
    expected() {
      return {
        block: false,
        passed: true,
        enabled: true,
        table: '## Post-Planning Gap Analysis\n\n| Source | Item | Status |\n|--------|------|--------|\n| REQUIREMENTS.md | REQ-01 | ✓ Covered |\n| CONTEXT.md | D-01 | ✓ Covered |\n\n✓ All 2 items covered by plans\n',
        summary: '✓ All 2 items covered by plans',
        counts: {
          total: 2,
          covered: 2,
          uncovered: 0,
        },
        message: '## Post-Planning Gap Analysis\n\n| Source | Item | Status |\n|--------|------|--------|\n| REQUIREMENTS.md | REQ-01 | ✓ Covered |\n| CONTEXT.md | D-01 | ✓ Covered |\n\n✓ All 2 items covered by plans\n',
      };
    },
  },
  {
    id: 'U9b',
    title: 'requirement and decision not cited -> still block:false, passed:true, gaps in table',
    setup(dir, h) {
      h.w(dir, '.planning/REQUIREMENTS.md', ['# Requirements', '', '- [ ] **REQ-01**: Users can log in', ''].join('\n'));
      h.w(dir, '.planning/phases/01-x/01-CONTEXT.md', ['<decisions>', '- **D-01:** Use PostgreSQL for the primary datastore layer', '</decisions>', ''].join('\n'));
      h.w(dir, '.planning/phases/01-x/01-01-PLAN.md', '<objective>Unrelated</objective>\n');
    },
    args() { return ['.planning/phases/01-x', 'REQ-01']; },
    outcome: 'advisory',
    block: false,
    expected() {
      return {
        block: false,
        passed: true,
        enabled: true,
        table: '## Post-Planning Gap Analysis\n\n| Source | Item | Status |\n|--------|------|--------|\n| REQUIREMENTS.md | REQ-01 | ✗ Not covered |\n| CONTEXT.md | D-01 | ✗ Not covered |\n\n⚠ 2 of 2 items not covered by any plan\n',
        summary: '⚠ 2 of 2 items not covered by any plan',
        counts: {
          total: 2,
          covered: 0,
          uncovered: 2,
        },
        message: '## Post-Planning Gap Analysis\n\n| Source | Item | Status |\n|--------|------|--------|\n| REQUIREMENTS.md | REQ-01 | ✗ Not covered |\n| CONTEXT.md | D-01 | ✗ Not covered |\n\n⚠ 2 of 2 items not covered by any plan\n',
      };
    },
  },
  {
    id: 'U9c',
    title: 'missing phase-dir argument is a usage failure',
    args() { return []; },
    usage: { code: 'sdk_missing_arg', message: 'gap-analysis.plan-post requires a phase-dir argument: check gap-analysis.plan-post <phase-dir> [phase-req-ids]' },
  },
  {
    id: 'U9d',
    title: 'phase dir escaping the project root is a usage failure',
    args() { return ['../outside']; },
    usage: { code: 'usage', message: 'path escapes its allowed directory: ../outside' },
  },
];

function run(c) {
  const dir = createTempProject('gate-u9-');
  const real = fs.realpathSync(dir);
  const aliases = tempRootAliases(dir);
  const writes = [];
  const outWrite = process.stdout.write;
  const errWrite = process.stderr.write;
  let restore;
  let result;
  try {
    restore = c.setup ? c.setup(dir, h) : undefined;
    process.stdout.write = (chunk) => {
      writes.push({ stream: 'stdout', chunk: String(chunk) });
      return true;
    };
    process.stderr.write = (chunk) => {
      writes.push({ stream: 'stderr', chunk: String(chunk) });
      return true;
    };
    result = gate.evaluateGapAnalysisPlanPost({ projectDir: dir, args: c.args(dir) });
  } finally {
    process.stdout.write = outWrite;
    process.stderr.write = errWrite;
    if (typeof restore === 'function') restore();
    cleanup(dir);
  }
  return { result, writes, dir, real, aliases };
}

describe('U9 evaluateGapAnalysisPlanPost', () => {
  for (const c of CASES) {
    test(`${c.id}: ${c.title}`, () => {
      const { result, writes, dir, real, aliases } = run(c);
      const unexpected = writes.filter(
        (w) => !(c.stderrPrefix && w.stream === 'stderr' && w.chunk.startsWith(c.stderrPrefix)),
      );
      assert.deepStrictEqual(unexpected, [], 'a gate module must not write to stdout/stderr');
      if (c.usage) {
        assert.equal(isGateUsageFailure(result), true);
        assert.deepStrictEqual(result, { failure: { code: c.usage.code, message: c.usage.message } });
        return;
      }
      assert.equal(isGateUsageFailure(result), false);
      assert.equal(result.outcome, c.outcome);
      assert.equal(result.block, c.block);
      const expected = canonicalizeTempPaths(c.expected(dir, real), aliases);
      const actual = canonicalizeTempPaths(result.payload, aliases);
      assert.deepStrictEqual(actual, expected);
      assert.equal(JSON.stringify(actual), JSON.stringify(expected), 'payload key order is part of the contract');
    });
  }
});
