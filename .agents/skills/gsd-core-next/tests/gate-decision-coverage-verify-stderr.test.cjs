'use strict';

/**
 * `check decision-coverage-verify` on a project that is NOT a git repository writes nothing to
 * stderr (#5139, epic #5056, ADR-5057 Phase 6 review finding).
 *
 * `phaseCommitMessages` (src/decision-coverage-support.cts, #5164) asks the evaluation-scope resolver
 * for the phase's commits, which runs `git`; with no repository git
 * prints `fatal: not a git repository` on ITS stderr, and a child's stderr is inherited by the
 * parent unless the call pipes it — a write to file descriptor 2 that no `process.stderr.write`
 * spy can see. So the assertion runs the gate in a CHILD node process and captures that child's
 * real stderr, plus an in-process `process.stderr.write` spy. The verdict payload is the same as
 * before: the commit-message haystack is simply empty.
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { PROBE_TIMEOUT_MS } = require('./helpers/timeouts.cjs');
const { createTempProject, cleanup } = require('./helpers.cjs');

const GATE_PATH = path.join(__dirname, '..', 'gsd-core', 'bin', 'lib', 'gate-decision-coverage-verify.cjs');
const { evaluateDecisionCoverageVerify } = require(GATE_PATH);

const PHASE = '.planning/phases/01-x';
const CONTEXT = `${PHASE}/01-CONTEXT.md`;

function nonGitProject() {
  const dir = createTempProject('gate-verify-nogit-');
  fs.mkdirSync(path.join(dir, PHASE), { recursive: true });
  fs.writeFileSync(path.join(dir, CONTEXT), ['<decisions>', '- **D-01:** Use PostgreSQL for the primary datastore layer', '</decisions>', ''].join('\n'));
  fs.writeFileSync(path.join(dir, PHASE, '01-01-PLAN.md'), '<objective>Unrelated work</objective>\n');
  return dir;
}

const EXPECTED_PAYLOAD = {
  skipped: false,
  blocking: false,
  total: 1,
  honored: 0,
  not_honored: [{ id: 'D-01', text: 'Use PostgreSQL for the primary datastore layer', category: '' }],
  message: [
    '### Decision Coverage (warning)',
    '',
    '1 decision(s) not found in shipped artifacts:',
    '',
    '- **D-01** (uncategorized): Use PostgreSQL for the primary datastore layer',
    '',
    'This is a soft warning - verification status is unchanged.',
  ].join('\n'),
};

describe('decision-coverage-verify on a non-git project directory', () => {
  test('the gate, run in a child node process, writes nothing to stderr (its result is the only stdout)', () => {
    const dir = nonGitProject();
    try {
      const code = `
        const gate = require(${JSON.stringify(GATE_PATH)});
        const result = gate.evaluateDecisionCoverageVerify({ projectDir: ${JSON.stringify(dir)}, args: [${JSON.stringify(PHASE)}, ${JSON.stringify(CONTEXT)}] });
        process.stdout.write(JSON.stringify(result));
      `;
      const child = spawnSync(process.execPath, ['-e', code], {
        encoding: 'utf-8',
        timeout: PROBE_TIMEOUT_MS,
        // Keep git from walking up into an enclosing repository: the project dir must be non-git.
        env: { ...process.env, GIT_CEILING_DIRECTORIES: path.dirname(fs.realpathSync(dir)) },
      });
      assert.equal(child.status, 0, `child failed: ${child.stderr}`);
      assert.equal(child.stderr, '', 'nothing on the child stderr (no `fatal: not a git repository`)');
      const result = JSON.parse(child.stdout);
      assert.equal(result.outcome, 'advisory');
      assert.equal(result.block, false);
      assert.deepStrictEqual(result.payload, EXPECTED_PAYLOAD);
    } finally {
      cleanup(dir);
    }
  });

  test('in-process: process.stderr.write and process.stdout.write are never called', () => {
    const dir = nonGitProject();
    const writes = [];
    const errWrite = process.stderr.write;
    const outWrite = process.stdout.write;
    const prevCeiling = process.env.GIT_CEILING_DIRECTORIES;
    process.env.GIT_CEILING_DIRECTORIES = path.dirname(fs.realpathSync(dir));
    let result;
    try {
      process.stderr.write = (chunk) => { writes.push(String(chunk)); return true; };
      process.stdout.write = (chunk) => { writes.push(String(chunk)); return true; };
      result = evaluateDecisionCoverageVerify({ projectDir: dir, args: [PHASE, CONTEXT] });
    } finally {
      process.stderr.write = errWrite;
      process.stdout.write = outWrite;
      if (prevCeiling === undefined) delete process.env.GIT_CEILING_DIRECTORIES;
      else process.env.GIT_CEILING_DIRECTORIES = prevCeiling;
      cleanup(dir);
    }
    assert.deepStrictEqual(writes, []);
    assert.deepStrictEqual(result.payload, EXPECTED_PAYLOAD);
  });

  test('control: the same `git log` with stderr inherited DOES print `not a git repository`, so the assertion can fail', () => {
    const dir = nonGitProject();
    try {
      const child = spawnSync(
        process.execPath,
        ['-e', `require('node:child_process').execFileSync('git', ['log', '-n', '1'], { cwd: ${JSON.stringify(dir)}, encoding: 'utf-8' })`],
        { encoding: 'utf-8', timeout: PROBE_TIMEOUT_MS, env: { ...process.env, GIT_CEILING_DIRECTORIES: path.dirname(fs.realpathSync(dir)) } },
      );
      assert.match(child.stderr, /not a git repository/i);
    } finally {
      cleanup(dir);
    }
  });
});
