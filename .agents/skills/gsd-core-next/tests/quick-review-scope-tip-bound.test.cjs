'use strict';

/**
 * Regression coverage for #4466 (carried through #5164, epic #5056 Phase 7):
 * quick.md's post-execute review scoping step must not fold commits that
 * landed on the shared tree (a worktree merge-back, another session) into
 * the quick task's own review scope. The step used to diff a range
 * (`DIFF_BASE..HEAD`, then `DIFF_BASE..QUICK_TIP`); #5164 replaces the range
 * with the evaluation-scope resolver's UNION of the commits naming the task,
 * which also excludes an unrelated commit interleaved INSIDE the task's window
 * — something no bounded range can do.
 *
 * Mirrors the issue's own verified reproduction methodology: extract the
 * fence VERBATIM from quick.md (never reimplemented), run it against a real
 * constructed git fixture matching the issue's own exact scenario (an
 * unrelated commit landing after the quick task's own work, before review).
 */

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { gitOrThrow } = require('./helpers/git-fixture.cjs');
const { GIT_FIXTURE_TIMEOUT_MS } = require('./helpers/timeouts.cjs');
const { createTempDir, cleanup } = require('./helpers.cjs');

const WORKFLOW_PATH = path.join(__dirname, '..', 'gsd-core', 'workflows', 'quick.md');
const TOOLS_PATH = path.join(__dirname, '..', 'gsd-core', 'bin', 'gsd-tools.cjs');

function extractFirstBashBlockAfter(content, startAnchor, stopAnchor) {
  const start = content.indexOf(startAnchor);
  assert.ok(start !== -1, `quick.md must contain the anchor "${startAnchor}"`);
  const stop = stopAnchor ? content.indexOf(stopAnchor, start + startAnchor.length) : content.length;
  assert.ok(!stopAnchor || stop !== -1, `quick.md must contain the anchor "${stopAnchor}" after "${startAnchor}"`);
  const region = content.slice(start, stop);

  const fenceStart = region.indexOf('```bash');
  assert.ok(fenceStart !== -1, `no \`\`\`bash fence found between "${startAnchor}" and its stop anchor`);
  const fenceEnd = region.indexOf('```', fenceStart + '```bash'.length);
  assert.ok(fenceEnd !== -1, `unterminated \`\`\`bash fence after "${startAnchor}"`);
  return region.slice(fenceStart + '```bash'.length, fenceEnd);
}

function seedFixtureRepo(dir) {
  gitOrThrow(['init', '-q'], { cwd: dir, timeoutMs: GIT_FIXTURE_TIMEOUT_MS });
  gitOrThrow(['config', 'user.email', 't@example.com'], { cwd: dir, timeoutMs: GIT_FIXTURE_TIMEOUT_MS });
  gitOrThrow(['config', 'user.name', 'T'], { cwd: dir, timeoutMs: GIT_FIXTURE_TIMEOUT_MS });
  gitOrThrow(['config', 'commit.gpgsign', 'false'], { cwd: dir, timeoutMs: GIT_FIXTURE_TIMEOUT_MS });
}

function writeAndCommit(dir, relPath, content, message) {
  const abs = path.join(dir, relPath);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  fs.writeFileSync(abs, content);
  gitOrThrow(['add', '-A'], { cwd: dir, timeoutMs: GIT_FIXTURE_TIMEOUT_MS });
  gitOrThrow(['commit', '-q', '-m', message], { cwd: dir, timeoutMs: GIT_FIXTURE_TIMEOUT_MS });
}

// Matches the issue's own fixture exactly: init, then the quick task's own
// commit (referencing quick_id in its message), then an unrelated later
// commit landing on the same tree before the review step runs.
const QUICK_ID = '260906-abc';

function buildFixture(tmpDir) {
  seedFixtureRepo(tmpDir);
  writeAndCommit(tmpDir, 'README.md', '# init\n', 'chore: init');
  writeAndCommit(tmpDir, 'src/quick-a.js', 'quick-a\n', `feat(quick-${QUICK_ID}): the quick task's own work`);
  writeAndCommit(tmpDir, 'src/interleaved.js', 'interleaved\n', 'fix: an unrelated commit interleaved inside the task window');
  writeAndCommit(tmpDir, 'src/quick-b.js', 'quick-b\n', `feat(quick-${QUICK_ID}): the quick task's second commit`);
  writeAndCommit(tmpDir, 'src/unrelated.js', 'unrelated\n', 'fix: an unrelated commit from another session on the shared tree');
}

function runScopingFence(tmpDir) {
  const content = fs.readFileSync(WORKFLOW_PATH, 'utf-8');
  const fence = extractFirstBashBlockAfter(content, "**Scope files from executor's commits:**", '**Invoke review:**');

  const script = [
    '#!/usr/bin/env bash',
    'set -uo pipefail',
    `quick_id="${QUICK_ID}"`,
    // The workflow's own launcher defines gsd_run; the fixture points it at this checkout's CLI.
    `gsd_run() { node "${TOOLS_PATH}" "$@"; }`,
    '{',
    fence,
    '} 1>&2',
    'printf \'%s\\n\' "$CHANGED_FILES"',
  ].join('\n');

  const scriptPath = path.join(tmpDir, '.scope-script.sh');
  fs.writeFileSync(scriptPath, script);

  const result = spawnSync('bash', [scriptPath], {
    cwd: tmpDir,
    encoding: 'utf8',
    timeout: GIT_FIXTURE_TIMEOUT_MS,
  });
  if (result.error) {
    throw new Error(`bash spawn failed: ${result.error.message}\ndiagnostics:\n${result.stderr || '(none)'}`);
  }
  if (result.status !== 0) {
    throw new Error(`bash exited ${result.status} (signal ${result.signal})\ndiagnostics:\n${result.stderr || '(none)'}`);
  }
  const files = result.stdout.trim().split(/\s+/).filter(Boolean).sort();
  return { files, diagnostics: result.stderr };
}

describe('#4466 / #5164: quick.md review scoping is the union of the quick task\'s own commits', () => {
  const workflowContent = fs.readFileSync(WORKFLOW_PATH, 'utf-8');
  const fence = extractFirstBashBlockAfter(workflowContent, "**Scope files from executor's commits:**", '**Invoke review:**');

  test('the fence asks the evaluation-scope resolver and derives no commit range of its own', () => {
    assert.ok(/gsd_run check evaluation-scope --quick "\$\{quick_id\}"/.test(fence), 'the scoping fence must call the resolver for the quick task');
    assert.ok(!/\.\.\s*(?:HEAD|\$\{)/.test(fence), 'the scoping fence must not diff a base..tip range');
    assert.ok(!/git (?:diff|log)/.test(fence), 'the scoping fence must not run its own git diff / git log');
  });

  test('real execution: unrelated commits — interleaved inside the window or landed later — are excluded from scope (issue #4466 repro)', () => {
    const tmpDir = fs.realpathSync.native(createTempDir('gsd-4466-'));
    try {
      buildFixture(tmpDir);
      const { files, diagnostics } = runScopingFence(tmpDir);
      assert.deepEqual(
        files,
        ['src/quick-a.js', 'src/quick-b.js'],
        `quick task's review scope must be exactly its own commits' files, got: ${JSON.stringify(files)}\ndiagnostics:\n${diagnostics || '(none)'}`,
      );
    } finally {
      cleanup(tmpDir);
    }
  });
});
