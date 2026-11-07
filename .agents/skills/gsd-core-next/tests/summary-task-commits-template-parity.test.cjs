'use strict';

/**
 * SUMMARY template ↔ resolver parity (#5164, epic #5056 Phase 7; generalizes the guard on the
 * closed PR #4127 / #3926).
 *
 * A phase's own commits are the `## Task Commits` rows of its SUMMARY files, read by
 * `extractTaskCommitRefs` (src/gate-evaluation-scope.cts). The coupling to the template's line
 * shape is load-bearing and otherwise implicit: a template whose task rows drift (a bare hash, a
 * different label, no heading) makes every phase silently degrade to the wider range. This suite
 * pins the shape of EVERY `summary*.md` template — the directory's set, not a list of today's
 * files — and holds the parser to it: fill each canonical row's hash slot with a real hex sha and
 * the parser must read back exactly those shas, in order.
 */

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const { extractTaskCommitRefs } = require('../gsd-core/bin/lib/gate-evaluation-scope.cjs');

const TEMPLATE_DIR = path.join(__dirname, '..', 'gsd-core', 'templates');
// `[-.]` and not `-` alone: `summary.compact.md` ships a real `## Task Commits` section.
const TEMPLATE_RE = /^summary([-.][^/]*)?\.md$/;
const HEADING = /^## Task Commits[ \t\r]*$/;
const NEXT_HEADING = /^## /;
// Canonical row: numbered item, `**Task N: …**`, one separator, then the hash slot in backticks.
const CANONICAL_ROW = /^[ \t]*\d+\.[ \t]+\*\*Task[ \t]+\d+:(?:(?!\*\*)[\s\S])*\*\*[ \t]*[-–—:][ \t]*`[0-9A-Za-z]+`/;
const HASH_SLOT = /`[0-9A-Za-z]+`/;

function templates() {
  const found = fs.readdirSync(TEMPLATE_DIR).filter((name) => TEMPLATE_RE.test(name)).sort();
  assert.ok(found.length > 0, 'no SUMMARY templates matched — the guard would check nothing');
  return found;
}

/** Every `## Task Commits` section of `text` as `{ body, terminated }` (the parser reopens on a repeat heading). */
function sections(text) {
  const out = [];
  let current = null;
  for (const line of text.split('\n')) {
    if (HEADING.test(line)) { if (current) current.terminated = true; current = { body: [], terminated: false }; out.push(current); continue; }
    if (NEXT_HEADING.test(line)) { if (current) current.terminated = true; current = null; continue; }
    if (current) current.body.push(line);
  }
  return out;
}

/** Replace the `i`-th canonical row's hash slot with a unique real sha; returns the filled text and the shas in order. */
function fillHashSlots(text) {
  const shas = [];
  const lines = text.split('\n');
  let inside = false;
  const filled = lines.map((line) => {
    if (HEADING.test(line)) { inside = true; return line; }
    if (NEXT_HEADING.test(line)) { inside = false; return line; }
    if (inside && CANONICAL_ROW.test(line)) {
      const sha = (shas.length + 1).toString(16).padStart(7, '0').replace(/^0/, 'a');
      shas.push(sha);
      return line.replace(HASH_SLOT, `\`${sha}\``);
    }
    return line;
  });
  return { text: filled.join('\n'), shas };
}

describe('SUMMARY templates carry the task-commit shape the resolver reads', () => {
  for (const name of templates()) {
    test(`${name}: a terminated \`## Task Commits\` section with canonical rows the parser reads back`, () => {
      const text = fs.readFileSync(path.join(TEMPLATE_DIR, name), 'utf8');
      const found = sections(text);
      assert.ok(found.length > 0, `${name}: no '## Task Commits' heading — the parser's section anchor is gone`);
      for (const section of found) {
        assert.ok(section.terminated, `${name}: '## Task Commits' is the last '## ' section — the parser would run to EOF`);
      }
      const { text: filled, shas } = fillHashSlots(text);
      assert.ok(shas.length > 0, `${name}: no canonical \`N. **Task N: …** - \`hash\`\` row — the parser reads nothing from this template`);
      assert.deepEqual(extractTaskCommitRefs(filled), shas, `${name}: the parser must read back exactly the filled shas`);
    });
  }
});

describe('positive controls — the shape guard can go red', () => {
  test('[control] a placeholder that is not hex (`def456g`) is not read — the executor must write real shas', () => {
    assert.deepEqual(extractTaskCommitRefs('## Task Commits\n1. **Task 1: x** - `def456g`\n## End\n'), []);
    assert.deepEqual(extractTaskCommitRefs('## Task Commits\n1. **Task 1: x** - `hij789k`\n## End\n'), []);
  });

  test('[control] a hash outside backticks, or a row without the Task label, is not read', () => {
    assert.deepEqual(extractTaskCommitRefs('## Task Commits\n1. **Task 1: x** - abc1234\n## End\n'), []);
    assert.deepEqual(extractTaskCommitRefs('## Task Commits\n1. Task 1: x - `abc1234`\n## End\n'), []);
  });

  test('[control] a template with a drifted row fails the canonical-row check', () => {
    assert.ok(!CANONICAL_ROW.test('1. **Task 1: x** abc1234'), 'a bare hash is not canonical');
    assert.ok(!CANONICAL_ROW.test('- Task 1: x - `abc1234`'), 'a bullet without the bold label is not canonical');
    assert.ok(CANONICAL_ROW.test('1. **Task 1: x** - `abc123f` (feat)'), 'the shipped shape is canonical');
  });
});
