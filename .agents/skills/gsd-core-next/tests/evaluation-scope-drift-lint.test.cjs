'use strict';

/**
 * Evaluation-scope drift guard (#5164, epic #5056, ADR-5057 §4) — positive controls.
 *
 * A guard nothing has seen fire is not evidence. Each rule (S1–S5) is driven RED by a violating
 * snippet, the exact-site allowlist pinning is shown to be per-site, and the real tree is asserted
 * clean so the next bespoke derivation turns CI red.
 */

const { describe, test, afterEach } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const { cleanup } = require('./helpers.cjs');
const guard = require('../scripts/lint-evaluation-scope-drift.cjs');

const ROOT = path.join(__dirname, '..');
const dirs = [];
afterEach(() => { while (dirs.length > 0) cleanup(dirs.pop()); });

const rulesOf = (found) => found.map((v) => v.rule).sort();

describe('lint-evaluation-scope-drift — each rule fails on its shape', () => {
  const shell = (text) => guard.scanText('gsd-core/workflows/x.md', text, 'shell');
  const ts = (text) => guard.scanText('src/gate-x.cts', text, 'ts');

  test('[S1] an any-branch commit lookup is flagged, in shell and in a TS argv', () => {
    assert.deepEqual(rulesOf(shell('git log --oneline --all --grep="03-01"')), ['S1', 'S5']);
    assert.deepEqual(rulesOf(ts("run(['log', '--oneline', '--all'])")), ['S1', 'S5']);
  });

  test('[S2] a range whose tip is the present is flagged; a three-dot PR diff and a bounded tip are not', () => {
    assert.deepEqual(rulesOf(shell('git diff --name-only "${DIFF_BASE}..HEAD" -- .')), ['S2']);
    assert.deepEqual(rulesOf(shell('git diff --name-only ${DIFF_BASE}..${TIP}')), ['S2']);
    assert.deepEqual(rulesOf(shell('git diff main...HEAD')), []);
    assert.deepEqual(rulesOf(ts("run(['diff', '--name-only', 'abc..HEAD'])")), ['S2']);
  });

  test('[S1/S2 precision] `--all-match` is not `--all`; a `${BASE}..${TIP}` range is still a range', () => {
    assert.deepEqual(rulesOf(shell('git log --oneline --all-match --grep=x')), ['S5']);
    assert.deepEqual(rulesOf(shell('git diff --name-only "${SCOPE_BASE}..${QUICK_TIP}"')), ['S2']);
    assert.deepEqual(rulesOf(shell('git diff --name-only "${BASE}...${TIP}"')), []);
  });

  test('[S3] a relative HEAD~N anchor is flagged', () => {
    assert.deepEqual(rulesOf(shell('git diff --name-only HEAD~1 HEAD')), ['S3']);
    assert.deepEqual(rulesOf(ts("run(['diff', '--name-only', 'HEAD~1', 'HEAD'])")), ['S3']);
  });

  test('[S4] a hand-rolled phase-start anchor is flagged, prefixed names included', () => {
    assert.deepEqual(rulesOf(shell('PHASE_START=$(git log --format=%H --diff-filter=A -- "$D" | tail -1)')), ['S4']);
    assert.deepEqual(rulesOf(shell('FALLOW_PHASE_START=$(git log --format=%H --diff-filter=A -- "$D" | tail -1)')), ['S4']);
  });

  test('[S5] a commit-message lookup in shell, and any raw `git log` argv in a gate module, is flagged', () => {
    assert.deepEqual(rulesOf(shell('git log --oneline --grep="feat("')), ['S5']);
    assert.deepEqual(rulesOf(ts("run(['log', '-n', '200'])")), ['S5']);
    assert.deepEqual(rulesOf(guard.scanText('src/verify.cts', "run(['log', '-n', '200'])", 'ts-scope')), []);
  });

  test('[independence] a backslash-continued invocation cannot dodge the scan', () => {
    const wrapped = 'git diff --name-only \\\n  "${DIFF_BASE}..HEAD"';
    assert.deepEqual(rulesOf(shell(wrapped)), ['S2']);
  });

  test('[negative] the resolver call and unrelated git use are clean', () => {
    assert.deepEqual(shell('gsd_run check evaluation-scope --phase 3 --raw'), []);
    assert.deepEqual(shell('git merge-base --is-ancestor "$SHA" HEAD'), []);
    assert.deepEqual(ts("run(['rev-parse', '--verify', 'HEAD'])"), []);
  });
});

describe('lint-evaluation-scope-drift — allowlist', () => {
  const violation = { rule: 'S3', file: 'gsd-core/workflows/a.md', line: 1, text: 'git diff --stat HEAD~10 HEAD' };
  const entry = { file: violation.file, rule: 'S3', command: violation.text, reason: 'report only (#5164)' };

  test('[happy] an entry pins one exact site (file + rule + normalized text)', () => {
    assert.deepEqual(guard.filterAllowedViolations([violation], [entry]), []);
    assert.equal(guard.filterAllowedViolations([{ ...violation, text: 'git diff --stat HEAD~9 HEAD' }], [entry]).length, 1);
    assert.equal(guard.filterAllowedViolations([{ ...violation, file: 'gsd-core/workflows/b.md' }], [entry]).length, 1);
    assert.deepEqual(guard.filterAllowedViolations([{ ...violation, text: '  git   diff --stat  HEAD~10 HEAD ' }], [entry]), []);
  });

  test('[negative] a stale entry and an entry without an issue reference are problems', () => {
    const stale = guard.validateAllowlist([entry], []);
    assert.ok(stale.some((p) => /no longer offend/.test(p.message)));
    const noIssue = guard.validateAllowlist([{ ...entry, reason: 'because' }], [violation]);
    assert.ok(noIssue.some((p) => /does not cite an issue/.test(p.message)));
  });

  test('[hostile] a malformed allowlist is a hard error, never a silent empty list', () => {
    assert.throws(() => guard.parseAllowlist('{not json'), /malformed allowlist JSON/);
    assert.throws(() => guard.parseAllowlist('{"a":1}'), /must be a JSON array/);
    assert.deepEqual(guard.parseAllowlist('[]'), []);
  });
});

describe('lint-evaluation-scope-drift — the tree', () => {
  test('[happy] the real tree has zero bespoke derivations beyond the pinned allowlist', () => {
    const entries = JSON.parse(fs.readFileSync(path.join(ROOT, 'scripts', 'lint-evaluation-scope-drift.allowlist.json'), 'utf8'));
    const { scannedHosts, violations, problems } = guard.scanRepo(ROOT, entries);
    assert.ok(scannedHosts > 50, `expected the workflow/agent/gate hosts to be scanned, got ${scannedHosts}`);
    assert.deepEqual(violations, []);
    assert.deepEqual(problems, []);
  });

  test('[positive control] a fixture tree with a bespoke range in a workflow goes red; the resolver module is exempt', () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'gsd-scope-drift-'));
    dirs.push(root);
    fs.mkdirSync(path.join(root, 'gsd-core', 'workflows'), { recursive: true });
    fs.mkdirSync(path.join(root, 'src'), { recursive: true });
    fs.mkdirSync(path.join(root, 'agents'), { recursive: true });
    fs.writeFileSync(path.join(root, 'gsd-core', 'workflows', 'bad.md'), '```bash\ngit diff --name-only "${DIFF_BASE}..HEAD"\n```\n');
    fs.writeFileSync(path.join(root, 'src', 'gate-evaluation-scope.cts'), "const a = ['log', '--all'];\n");
    const { violations } = guard.scanRepo(root, []);
    assert.deepEqual(violations.map((v) => `${v.file}:${v.rule}`), ['gsd-core/workflows/bad.md:S2']);
  });

  test('[negative] a tree with no hosts is a problem, not a clean bill of health', () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'gsd-scope-drift-empty-'));
    dirs.push(root);
    const { scannedHosts, problems } = guard.scanRepo(root, []);
    assert.equal(scannedHosts, 0);
    assert.ok(problems.some((p) => /scanned zero hosts/.test(p.message)));
  });
});
