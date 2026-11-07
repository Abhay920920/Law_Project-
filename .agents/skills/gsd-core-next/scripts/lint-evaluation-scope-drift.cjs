#!/usr/bin/env node
'use strict';

/**
 * #5164 (epic #5056, ADR-5057 §4 second bullet) — evaluation-scope drift guard.
 *
 * Every gate or workflow step that decides "which commits and files am I evaluating" asks the ONE
 * resolver (`src/gate-evaluation-scope.cts`, reached as `check evaluation-scope`). This guard fails
 * CI on the next bespoke derivation. It scans the shapes the Phase 7 census enumerated (S1–S5):
 *
 *   S1  `git log … --all`            any-branch commit lookups (a commit that lives on another branch
 *                                    satisfies the check)
 *   S2  `git diff … ..HEAD`          a range whose tip is the present, not the unit's own last commit
 *                                    (`DIFF_BASE..HEAD`, `HEAD~1..HEAD`, `${X}..HEAD`)
 *   S3  `git diff … HEAD~N`          a relative anchor that names whatever commit happened to land
 *   S4  `PHASE_START=$(git log … --diff-filter=A …)`  a hand-rolled phase-start anchor
 *   S5  `git log … --grep` (shell) / any raw `git log` argv in a gate module — a commit-message
 *       lookup that decides scope; only the resolver may ask git which commits belong to a unit
 *
 * Hosts: every `src/gate-*.cts` module except the resolver itself, `src/decision-coverage-support.cts`,
 * `gsd-core/workflows/**\/*.md` and `agents/*.md`. A site that is legitimately NOT an evaluation
 * scope (a PR diff against the base branch, a session summary) is exempted by an allowlist entry
 * `{ file, rule, command, reason }` pinning the exact normalized site; `reason` must cite `#NNN`.
 * A stale entry is itself a violation, via `scripts/lib/allowlist-ratchet.cjs`.
 *
 * Fail-closed: scanning zero hosts is a violation, and a malformed allowlist is a hard error.
 * The scanner is exported (`scanText`) so the positive control can drive it red on a violating
 * snippet (tests/evaluation-scope-drift-lint.test.cjs).
 */

const fs = require('node:fs');
const path = require('node:path');
const { assertWithinAllowlist } = require('./lib/allowlist-ratchet.cjs');
const { runMain } = require('./lib/cli-exit.cjs');

const REPO_ROOT = path.join(__dirname, '..');
const ALLOWLIST_PATH = path.join(__dirname, 'lint-evaluation-scope-drift.allowlist.json');
const RESOLVER_FILE = 'src/gate-evaluation-scope.cts';
const ISSUE_REF_RE = /#\d+/;

// Shell shapes (workflows, agents): scanned per logical line, backslash continuations joined.
const SHELL_RULES = [
  { rule: 'S1', re: /\bgit\s+log\b[^\n]*--all(?![-\w])/ },
  { rule: 'S2', re: /\bgit\s+diff\b[^\n]*(?:(?<!\.)\.\.HEAD\b|\$\{?[A-Z_]*(?:BASE|TIP)\}?(?<!\.)\.\.(?!\.))/ },
  { rule: 'S3', re: /\bgit\s+(?:diff|log)\b[^\n]*\bHEAD~\d+\b/ },
  { rule: 'S4', re: /\b[A-Z_]*PHASE_START=\$\(\s*git\s+log\b[^\n]*--diff-filter=A\b/ },
  { rule: 'S5', re: /\bgit\s+log\b[^\n]*--grep\b/ },
];

// TypeScript shapes: an argv array handed to a git runner, possibly across lines. S5 (any raw
// `git log` argv) applies only to the gate modules, where the resolver is the one place that may
// ask git which commits belong to a unit; `src/verify.cts` is scanned for S1–S3 only.
const TS_RULES = [
  { rule: 'S1', re: /\[\s*'log'[^\]]*'--all'/g }, // exact element: `--all-match` is a different flag
  { rule: 'S2', re: /\[\s*'diff'[^\]]*(?<!\.)\.\.HEAD\b/g },
  { rule: 'S3', re: /\[\s*'(?:diff|log)'[^\]]*'HEAD~\d+'/g },
  { rule: 'S5', re: /\[\s*'log'/g, gateModulesOnly: true },
];

function toPosix(p) {
  return p.split(path.sep).join('/');
}

/** Join backslash-continued physical lines; `line` is the first physical line of each group. */
function joinContinuations(lines) {
  const out = [];
  let i = 0;
  while (i < lines.length) {
    const startLine = i + 1;
    let text = lines[i];
    while (/\\\s*$/.test(text) && i + 1 < lines.length) {
      text = `${text.replace(/\\\s*$/, '')} ${lines[i + 1].trim()}`;
      i += 1;
    }
    out.push({ text, line: startLine });
    i += 1;
  }
  return out;
}

/** Whitespace-normalize so a re-wrapped but otherwise identical site keys the same. */
function normalizeText(s) {
  return String(s || '').replace(/\s+/g, ' ').trim();
}

/** Scan one host's text. `kind` is `'shell'` (markdown prompts) or `'ts'` (gate modules). */
function scanText(file, text, kind) {
  const violations = [];
  if (kind === 'ts' || kind === 'ts-scope') {
    for (const { rule, re, gateModulesOnly } of TS_RULES) {
      if (gateModulesOnly && kind !== 'ts') continue;
      for (const match of text.matchAll(re)) {
        const line = text.slice(0, match.index).split('\n').length;
        violations.push({ rule, file, line, text: normalizeText(match[0]) });
      }
    }
    return violations;
  }
  for (const { text: logical, line } of joinContinuations(text.split('\n'))) {
    for (const { rule, re } of SHELL_RULES) {
      if (re.test(logical)) violations.push({ rule, file, line, text: normalizeText(logical) });
    }
  }
  return violations;
}

function keyFor(file, rule, commandText) {
  return `${file}::${rule}::${normalizeText(commandText)}`;
}

const idFor = (v) => keyFor(v.file, v.rule, v.text);
const idForEntry = (e) => keyFor(e.file, e.rule, e.command);

/** Drop findings an allowlist entry pins by exact site (file + rule + normalized text). */
function filterAllowedViolations(violations, entries) {
  const allowed = new Set(entries.map(idForEntry));
  return violations.filter((v) => !allowed.has(idFor(v)));
}

/** Every allowlist entry must cite an issue and still match a real finding (no stale entry). */
function validateAllowlist(entries, violations) {
  const problems = [];
  for (const e of entries) {
    if (!ISSUE_REF_RE.test(String(e.reason || ''))) {
      problems.push({ message: `lint-evaluation-scope-drift allowlist entry ${idForEntry(e)} has a reason that does not cite an issue (#NNN): ${JSON.stringify(e.reason)}` });
    }
  }
  assertWithinAllowlist({
    label: 'lint-evaluation-scope-drift allowlist',
    current: violations.map(idFor),
    known: entries.map(idForEntry),
    fail: (message) => problems.push({ message }),
    pruneHint: 'edit scripts/lint-evaluation-scope-drift.allowlist.json',
  });
  return problems;
}

function parseAllowlist(raw) {
  let parsed;
  try {
    parsed = JSON.parse(raw);
  } catch (err) {
    throw new Error(`lint-evaluation-scope-drift: malformed allowlist JSON at ${ALLOWLIST_PATH}: ${err.message}`);
  }
  if (!Array.isArray(parsed)) {
    throw new Error(`lint-evaluation-scope-drift: allowlist at ${ALLOWLIST_PATH} must be a JSON array, got ${typeof parsed}`);
  }
  return parsed;
}

function loadAllowlist() {
  let raw;
  try {
    raw = fs.readFileSync(ALLOWLIST_PATH, 'utf-8');
  } catch (err) {
    if (err && err.code === 'ENOENT') return [];
    throw new Error(`lint-evaluation-scope-drift: cannot read allowlist ${ALLOWLIST_PATH}: ${err.message}`);
  }
  return parseAllowlist(raw);
}

function collectFiles(root, dir, suffix, predicate) {
  const out = [];
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch {
    return out;
  }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...collectFiles(root, full, suffix, predicate));
    else if (entry.isFile() && entry.name.endsWith(suffix)) {
      const rel = toPosix(path.relative(root, full));
      if (!predicate || predicate(rel)) out.push(rel);
    }
  }
  return out;
}

/** The hosts under `root`, each `{ file, kind }`. */
function listHosts(root) {
  const hosts = [];
  const gateModule = (r) => /^src\/gate-[^/]+\.cts$/.test(r) || r === 'src/decision-coverage-support.cts';
  for (const rel of collectFiles(root, path.join(root, 'src'), '.cts', gateModule)) {
    if (rel !== RESOLVER_FILE) hosts.push({ file: rel, kind: 'ts' });
  }
  if (fs.existsSync(path.join(root, 'src', 'verify.cts'))) hosts.push({ file: 'src/verify.cts', kind: 'ts-scope' });
  for (const rel of collectFiles(root, path.join(root, 'gsd-core', 'workflows'), '.md')) hosts.push({ file: rel, kind: 'shell' });
  for (const rel of collectFiles(root, path.join(root, 'agents'), '.md', (r) => /^agents\/[^/]+\.md$/.test(r))) hosts.push({ file: rel, kind: 'shell' });
  return hosts;
}

/** Scan the tree. Returns `{ scannedHosts, violations, problems }`. */
function scanRepo(root, entries) {
  const hosts = listHosts(root);
  const raw = [];
  for (const { file, kind } of hosts) {
    let text;
    try {
      text = fs.readFileSync(path.join(root, file), 'utf-8');
    } catch {
      continue;
    }
    raw.push(...scanText(file, text, kind));
  }
  const problems = validateAllowlist(entries, raw);
  const violations = filterAllowedViolations(raw, entries);
  if (hosts.length === 0) problems.push({ message: 'lint-evaluation-scope-drift scanned zero hosts — an inert scan must not report a clean repo' });
  return { scannedHosts: hosts.length, violations, problems };
}

function main() {
  const { scannedHosts, violations, problems } = scanRepo(REPO_ROOT, loadAllowlist());
  if (violations.length === 0 && problems.length === 0) {
    process.stdout.write(`ok evaluation-scope-drift: ${scannedHosts} hosts\n`);
    return 0;
  }
  process.stderr.write('ERROR evaluation-scope-drift: a bespoke commit-range / file-set derivation (ADR-5057 §4, #5164)\n');
  for (const v of violations) process.stderr.write(`  - ${v.file}:${v.line} [${v.rule}] ${v.text}\n`);
  for (const p of problems) process.stderr.write(`  - ${p.message}\n`);
  process.stderr.write('Ask the resolver instead: `gsd_run check evaluation-scope --phase <N> | --plan <P-N> | --quick <id>`.\n');
  return 1;
}

if (require.main === module) runMain(main);

module.exports = {
  scanText, scanRepo, listHosts, filterAllowedViolations, validateAllowlist, parseAllowlist,
  SHELL_RULES, TS_RULES, RESOLVER_FILE,
};
