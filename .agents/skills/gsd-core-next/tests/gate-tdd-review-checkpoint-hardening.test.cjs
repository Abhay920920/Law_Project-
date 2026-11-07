'use strict';

/**
 * `check tdd-review-checkpoint` review findings (#5139, epic #5056, ADR-5057 Phase 6):
 *
 *   H1  `type: tdd` detection is byte-equivalent to the old `^type:\s*tdd\s*$` multiline regex in
 *       its three pathological forms (a duplicate `type:` key with tdd second, the value on the
 *       next line, and `type : tdd`, which does NOT match).
 *   H2  the plan id (a plan FILE NAME) reaches `git log --grep` as a LITERAL: a metacharacter in it
 *       can no longer satisfy RED/GREEN with an unrelated commit, while an ordinary id like
 *       `03.1-02` still matches its own commits.
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { GIT_TIMEOUT_MS } = require('./helpers/timeouts.cjs');
const { createTempGitProject, cleanup } = require('./helpers.cjs');

const { evaluateTddReviewCheckpoint } = require('../gsd-core/bin/lib/gate-tdd-review-checkpoint.cjs');

function git(dir, ...args) {
  return execFileSync(
    'git',
    ['-c', 'user.name=gate-test', '-c', 'user.email=gate-test@example.invalid', '-c', 'commit.gpgsign=false', ...args],
    { cwd: dir, encoding: 'utf-8', stdio: 'pipe', timeout: GIT_TIMEOUT_MS, windowsHide: true },
  );
}

function withProject(planFiles, commits, fn) {
  const dir = createTempGitProject('gate-tdd-hardening-');
  try {
    for (const [name, content] of Object.entries(planFiles)) {
      const file = path.join(dir, '.planning', 'phases', '01-x', name);
      fs.mkdirSync(path.dirname(file), { recursive: true });
      fs.writeFileSync(file, content);
    }
    // The gate scopes `git log` to `-- .`, so each commit must touch a file (an empty one is invisible).
    commits.forEach((message, index) => {
      fs.writeFileSync(path.join(dir, `commit-${index}.txt`), `${message}\n`);
      git(dir, 'add', '-A');
      git(dir, 'commit', '-m', message);
    });
    return fn(dir);
  } finally {
    cleanup(dir);
  }
}

const plan = (typeLines) => `---\nphase: 1\nplan: 1\n${typeLines}\n---\n# Plan\n`;

function review(dir) {
  const result = evaluateTddReviewCheckpoint({ projectDir: dir, args: ['1'] });
  assert.equal(result.failure, undefined, 'not a usage failure');
  return result.payload;
}

describe('H1 type: tdd detection equals the old regex in its pathological forms', () => {
  const CASES = [
    ['a duplicate type key with tdd second', 'type: execute\ntype: tdd', 1],
    ['the value on the next line', 'type:\ntdd', 1],
    ['a space before the colon (`type : tdd`)', 'type : tdd', 0],
    ['a quoted value', 'type: "tdd"', 0],
    ['a trailing comment', 'type: tdd # note', 0],
    ['plain', 'type: tdd', 1],
  ];
  for (const [label, typeLines, tddPlans] of CASES) {
    test(`${label} -> ${tddPlans} tdd plan(s)`, () => {
      withProject({ '01-01-PLAN.md': plan(typeLines) }, [], (dir) => {
        assert.equal(review(dir).tddPlans, tddPlans);
      });
    });
  }
});

describe('H2 the plan id is matched literally in the git --grep pattern', () => {
  // `*` and `?` are not valid in a Windows file name, so the POSIX-only ids are skipped there.
  const posixOnly = { skip: process.platform === 'win32' };

  test('an ordinary id with a dot (`03.1-02`) still matches its own RED and GREEN commits', () => {
    withProject({ '03.1-02-PLAN.md': plan('type: tdd') }, ['test(03.1-02): red', 'feat(03.1-02): green'], (dir) => {
      const out = review(dir);
      assert.equal(out.rows[0].planId, '03.1-02');
      assert.equal(out.rows[0].red, true);
      assert.equal(out.rows[0].green, true);
      assert.equal(out.violations, 0);
    });
  });

  test('`.` is a literal dot: an unrelated id (`03x1-02`) does not satisfy plan `03.1-02`', () => {
    withProject({ '03.1-02-PLAN.md': plan('type: tdd') }, ['test(03x1-02): red', 'feat(03x1-02): green'], (dir) => {
      const row = review(dir).rows[0];
      assert.equal(row.red, false);
      assert.equal(row.green, false);
      assert.deepStrictEqual(row.missing, ['RED', 'GREEN']);
    });
  });

  test('a bracket id (`a[b]`) matches its own commits and not `ab`', () => {
    withProject({ 'a[b]-PLAN.md': plan('type: tdd') }, ['test(ab): r', 'feat(ab): g'], (dir) => {
      assert.equal(review(dir).rows[0].red, false);
    });
    withProject({ 'a[b]-PLAN.md': plan('type: tdd') }, ['test(a[b]): r', 'feat(a[b]): g', 'refactor(a[b]): t'], (dir) => {
      const row = review(dir).rows[0];
      assert.equal(row.red, true);
      assert.equal(row.green, true);
      assert.equal(row.refactor, true);
    });
  });

  test('parentheses, plus, caret, dollar and braces in an id are literal', () => {
    for (const id of ['a(b', 'a)b', 'a+b', 'a^b', 'a$b', 'a{1}']) {
      withProject({ [`${id}-PLAN.md`]: plan('type: tdd') }, [`test(${id}): r`, `feat(${id}): g`], (dir) => {
        const row = review(dir).rows[0];
        assert.equal(row.planId, id);
        assert.equal(row.red, true, `${id}: its own RED commit matches`);
        assert.equal(row.green, true, `${id}: its own GREEN commit matches`);
      });
    }
    // `a+b` would (as an extended regex) match `aab`; `a{1}` would match `a`.
    withProject({ 'a+b-PLAN.md': plan('type: tdd') }, ['test(aab): r', 'feat(aab): g'], (dir) => {
      assert.equal(review(dir).rows[0].red, false);
    });
    withProject({ 'a{1}-PLAN.md': plan('type: tdd') }, ['test(a): r', 'feat(a): g'], (dir) => {
      assert.equal(review(dir).rows[0].red, false);
    });
  });

  test('a plan named `x.*` (wildcard) is not satisfied by unrelated commits', posixOnly, () => {
    withProject({ 'x.*-PLAN.md': plan('type: tdd') }, ['test(xyz): red', 'feat(xyz): green', 'test(x): red'], (dir) => {
      const row = review(dir).rows[0];
      assert.equal(row.planId, 'x.*');
      assert.equal(row.red, false);
      assert.equal(row.green, false);
      assert.equal(review(dir).violations, 1);
    });
    withProject({ 'x.*-PLAN.md': plan('type: tdd') }, ['test(x.*): red', 'feat(x.*): green'], (dir) => {
      const row = review(dir).rows[0];
      assert.equal(row.red, true);
      assert.equal(row.green, true);
    });
  });
});
