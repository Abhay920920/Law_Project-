'use strict';

/**
 * #5139 (epic #5056, ADR-5057 Phase 6): two deliberate behaviour changes the gate modules made
 * when the raw parsers moved onto their owning seams, pinned in-process through the gate modules.
 *
 * 1. `check decision-coverage-verify` reads the SUMMARY's `files_modified` from the SUMMARY's
 *    FRONTMATTER, through the frontmatter reader (`rawFrontmatterField`): an inline list
 *    (`files_modified: [a.js, b.js]`) is honoured, a block list is honoured, and a
 *    `files_modified:` block that only appears in the SUMMARY BODY is ignored. (The pre-move
 *    regex over the whole SUMMARY text read a block anywhere in the document and no inline list.)
 *    Boundary rows pin the two caps `readModifiedFilesContent` carries: 50 files and 256 KiB per
 *    file, each at limit-1, limit and limit+1.
 * 2. `GSD_WORKSTREAM` selects the workstream's `config.json` for `check decision-coverage-plan`
 *    (the shared dot-path resolver `config-get workflow.*` uses: the workstream's config first;
 *    the project root's only when the workstream's does not carry the key).
 *
 * Every verdict payload below was derived by EXECUTING the gate in a fixture project.
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createTempGitProject, cleanup } = require('./helpers.cjs');

const { evaluateDecisionCoverageVerify } = require('../gsd-core/bin/lib/gate-decision-coverage-verify.cjs');
const { evaluateDecisionCoveragePlan } = require('../gsd-core/bin/lib/gate-decision-coverage-plan.cjs');
const { isGateUsageFailure } = require('../gsd-core/bin/lib/gate-verdict.cjs');

// The published caps of the shipped-artifact scan (the gate owns the constants privately; the
// boundary rows below pin them by BEHAVIOUR — a change to either cap fails a row).
const MODIFIED_FILES_MAX_COUNT = 50;
const MODIFIED_FILES_MAX_BYTES = 256 * 1024;

const PHASE_DIR = '.planning/phases/01-x';
const CONTEXT = `${PHASE_DIR}/01-CONTEXT.md`;
const SUMMARY = `${PHASE_DIR}/01-01-SUMMARY.md`;

const DECISIONS = {
  'D-01': 'Use PostgreSQL for the primary datastore layer',
  'D-02': 'Cache session tokens inside the edge gateway process',
  'D-03': 'Emit structured audit records for every admin action',
};

function w(dir, rel, content) {
  const p = path.join(dir, rel);
  fs.mkdirSync(path.dirname(p), { recursive: true });
  fs.writeFileSync(p, content);
}

/** A temp git project holding CONTEXT.md with the three decisions and no config. */
function project(t) {
  const dir = createTempGitProject('gate-fm-evidence-');
  t.after(() => cleanup(dir));
  w(dir, CONTEXT, ['<decisions>', ...Object.entries(DECISIONS).map(([id, text]) => `- **${id}:** ${text}`), '</decisions>', ''].join('\n'));
  return dir;
}

/** Run `fn` with GSD_WORKSTREAM set (or unset when `name` is null), restoring the ambient value. */
function withWorkstream(name, fn) {
  const had = Object.prototype.hasOwnProperty.call(process.env, 'GSD_WORKSTREAM');
  const before = process.env['GSD_WORKSTREAM'];
  if (name === null) delete process.env['GSD_WORKSTREAM'];
  else process.env['GSD_WORKSTREAM'] = name;
  try {
    return fn();
  } finally {
    if (had) process.env['GSD_WORKSTREAM'] = before;
    else delete process.env['GSD_WORKSTREAM'];
  }
}

function verify(dir) {
  return withWorkstream(null, () => evaluateDecisionCoverageVerify({ projectDir: dir, args: [PHASE_DIR, CONTEXT] }));
}

/** The exact verdict `decision-coverage-verify` returns for these honored decision ids. */
function expectedVerify(honoredIds) {
  const notHonored = Object.keys(DECISIONS).filter((id) => !honoredIds.includes(id));
  const message = notHonored.length === 0
    ? 'All trackable CONTEXT.md decisions are honored by shipped artifacts.'
    : [
      '### Decision Coverage (warning)',
      '',
      `${notHonored.length} decision(s) not found in shipped artifacts:`,
      '',
      ...notHonored.map((id) => `- **${id}** (uncategorized): ${DECISIONS[id]}`),
      '',
      'This is a soft warning - verification status is unchanged.',
    ].join('\n');
  return {
    outcome: notHonored.length === 0 ? 'pass' : 'advisory',
    block: false,
    payload: {
      skipped: false,
      blocking: false,
      total: 3,
      honored: honoredIds.length,
      not_honored: notHonored.map((id) => ({ id, text: DECISIONS[id], category: '' })),
      message,
    },
  };
}

function assertVerdict(result, expected) {
  assert.equal(isGateUsageFailure(result), false);
  assert.equal(result.outcome, expected.outcome);
  assert.equal(result.block, expected.block);
  assert.deepStrictEqual(result.payload, expected.payload);
  assert.equal(JSON.stringify(result.payload), JSON.stringify(expected.payload), 'payload key order is part of the contract');
}

function summary(frontmatterLines, body) {
  return ['---', 'phase: 1', ...frontmatterLines, '---', body ?? '# Summary', ''].join('\n');
}

describe('decision-coverage-verify reads files_modified from the SUMMARY frontmatter', () => {
  test('an inline list `files_modified: [a.js, b.js]` is honoured', (t) => {
    const dir = project(t);
    w(dir, 'a.js', '// D-01 honored here\n');
    w(dir, 'b.js', '// D-02 honored here\n');
    w(dir, SUMMARY, summary(['files_modified: [a.js, b.js]']));
    assertVerdict(verify(dir), expectedVerify(['D-01', 'D-02']));
  });

  test('a block list is honoured', (t) => {
    const dir = project(t);
    w(dir, 'a.js', '// D-01 honored here\n');
    w(dir, 'b.js', '// D-02 honored here\n');
    w(dir, SUMMARY, summary(['files_modified:', '  - a.js', '  - b.js']));
    assertVerdict(verify(dir), expectedVerify(['D-01', 'D-02']));
  });

  test('a `files_modified:` block in the SUMMARY BODY (no such frontmatter key) is ignored', (t) => {
    const dir = project(t);
    w(dir, 'a.js', '// D-01 honored here\n');
    w(dir, SUMMARY, summary(['status: complete'], ['# Summary', '', 'files_modified:', '  - a.js', ''].join('\n')));
    assertVerdict(verify(dir), expectedVerify([]));
  });

  test('a body `files_modified:` block is ignored even beside a frontmatter list', (t) => {
    const dir = project(t);
    w(dir, 'a.js', '// D-01 honored here\n');
    w(dir, 'c.js', '// D-03 honored here\n');
    w(dir, SUMMARY, summary(['files_modified: [a.js]'], ['# Summary', '', 'files_modified:', '  - c.js', ''].join('\n')));
    assertVerdict(verify(dir), expectedVerify(['D-01']));
  });

  test('every decision honored by listed files -> pass', (t) => {
    const dir = project(t);
    w(dir, 'all.js', '// D-01 D-02 D-03\n');
    w(dir, SUMMARY, summary(['files_modified: [all.js]']));
    assertVerdict(verify(dir), expectedVerify(['D-01', 'D-02', 'D-03']));
  });

  test('a listed path that escapes the project directory is never read', (t) => {
    const dir = project(t);
    const outside = createTempGitProject('gate-fm-outside-');
    t.after(() => cleanup(outside));
    w(outside, 'secret.js', '// D-01 D-02 D-03\n');
    w(dir, SUMMARY, summary([`files_modified: [${path.join(outside, 'secret.js')}]`]));
    assertVerdict(verify(dir), expectedVerify([]));
  });
});

describe('readModifiedFilesContent caps (boundary rows)', () => {
  test('the file cap is 50: file 49 (limit-1) and 50 (limit) are read, file 51 (limit+1) is not', (t) => {
    const dir = project(t);
    const names = [];
    for (let i = 1; i <= MODIFIED_FILES_MAX_COUNT + 1; i++) {
      const name = `f${String(i).padStart(2, '0')}.txt`;
      names.push(name);
      const marker = i === MODIFIED_FILES_MAX_COUNT - 1 ? 'D-01' : i === MODIFIED_FILES_MAX_COUNT ? 'D-02' : i === MODIFIED_FILES_MAX_COUNT + 1 ? 'D-03' : 'filler';
      w(dir, name, `// ${marker}\n`);
    }
    w(dir, SUMMARY, summary([`files_modified: [${names.join(', ')}]`]));
    assertVerdict(verify(dir), expectedVerify(['D-01', 'D-02']));
  });

  test('the per-file cap is 256 KiB: a marker ending at limit-1 and at limit is read, at limit+1 it is cut', (t) => {
    const dir = project(t);
    const padded = (markerId, totalLength) => `${'x'.repeat(totalLength - ` ${markerId}`.length)} ${markerId}`;
    w(dir, 'below.txt', padded('D-01', MODIFIED_FILES_MAX_BYTES - 1));
    w(dir, 'at.txt', padded('D-02', MODIFIED_FILES_MAX_BYTES));
    w(dir, 'above.txt', padded('D-03', MODIFIED_FILES_MAX_BYTES + 1));
    w(dir, SUMMARY, summary(['files_modified: [below.txt, at.txt, above.txt]']));
    assertVerdict(verify(dir), expectedVerify(['D-01', 'D-02']));
  });
});

describe('GSD_WORKSTREAM selects the workstream config for decision-coverage-plan', () => {
  const SKIPPED = {
    outcome: 'skip',
    block: false,
    payload: {
      passed: true,
      skipped: true,
      reason: 'workflow.context_coverage_gate is false',
      total: 0,
      covered: 0,
      uncovered: [],
      message: 'Decision coverage gate disabled by config.',
    },
  };

  function planFor(dir, workstream) {
    return withWorkstream(workstream, () => evaluateDecisionCoveragePlan({
      projectDir: dir,
      args: [PHASE_DIR, '--context', CONTEXT],
    }));
  }

  function projectWithConfigs(t, { root, workstream }) {
    const dir = project(t);
    w(dir, `${PHASE_DIR}/01-01-PLAN.md`, '<objective>Unrelated work</objective>\n');
    if (root !== undefined) w(dir, '.planning/config.json', JSON.stringify({ workflow: { context_coverage_gate: root } }));
    if (workstream !== undefined) w(dir, '.planning/workstreams/alpha/config.json', JSON.stringify({ workflow: { context_coverage_gate: workstream } }));
    return dir;
  }

  test('a workstream config disabling the gate skips it, though the root config enables it (root not consulted)', (t) => {
    const dir = projectWithConfigs(t, { root: true, workstream: false });
    assertVerdict(planFor(dir, 'alpha'), SKIPPED);
  });

  test('a workstream config enabling the gate runs it, though the root config disables it (root not consulted)', (t) => {
    const dir = projectWithConfigs(t, { root: false, workstream: true });
    const result = planFor(dir, 'alpha');
    assert.equal(isGateUsageFailure(result), false);
    assert.equal(result.outcome, 'block');
    assert.equal(result.block, true);
    assert.equal(result.payload.skipped, false);
    assert.equal(result.payload.total, 3);
    assert.equal(result.payload.covered, 0);
    assert.deepStrictEqual(result.payload.uncovered, Object.entries(DECISIONS).map(([id, text]) => ({ id, text, category: '' })));
  });

  test('without GSD_WORKSTREAM the root config decides, and the workstream config is not consulted', (t) => {
    const dir = projectWithConfigs(t, { root: false, workstream: true });
    assertVerdict(planFor(dir, null), SKIPPED);
  });

  test('a workstream config that does not carry the key falls back to the root config', (t) => {
    const dir = projectWithConfigs(t, { root: false });
    w(dir, '.planning/workstreams/alpha/config.json', JSON.stringify({ workflow: {} }));
    assertVerdict(planFor(dir, 'alpha'), SKIPPED);
  });
});
