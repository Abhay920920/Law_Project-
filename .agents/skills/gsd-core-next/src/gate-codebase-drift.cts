/**
 * `check verify-codebase-drift` as a gate module (#5219, epic #5056, ADR-5057 §4 closing arm C): it
 * returns a `GateResult`; the command router formats it. Imports no io module and performs no direct
 * console/stdout/stderr write (ESLint-enforced).
 *
 * The `drift` capability's gate at `execute:wave:post`. It diffs the repository's HEAD against the
 * commit `.planning/codebase/STRUCTURE.md` was last mapped at (`last_mapped_commit`, stamped by
 * `stamp-codebase-map`) and reports structural change the map does not describe (`detectDrift`,
 * `drift.cjs`). Non-blocking unless the drift crosses the configured threshold (`block` = the drift
 * library's `actionRequired`).
 *
 * Dispatched gate, payload exit mode (see `gate-schema-drift.cts`): a blocking verdict is exit 0 and
 * "could not look" is exit 69 (#5170).
 *
 * Non-blocking contract: a throw anywhere yields a non-blocking `unreadable` verdict, never a crash.
 *
 * Argv after the verb: none.
 */

import fs from 'node:fs';
import path from 'node:path';
import { gateVerdict, gateUnreadable } from './gate-verdict.cjs';
import type { GateResult } from './gate-verdict.cjs';
import { readWorkflowConfigValue } from './gate-config.cjs';
import { execGit } from './shell-command-projection.cjs';
import { resolveRuntime } from './runtime-slash.cjs';
// eslint-disable-next-line @typescript-eslint/no-require-imports -- planning-workspace.cjs is an export= CommonJS module
import planningWorkspace = require('./planning-workspace.cjs');
// eslint-disable-next-line @typescript-eslint/no-require-imports -- worktree-safety.cjs is an export= CommonJS module
import worktreeSafetyMod = require('./worktree-safety.cjs');
// eslint-disable-next-line @typescript-eslint/no-require-imports -- onboard-projection.cjs is an export= CommonJS module
import onboardProjectionMod = require('./onboard-projection.cjs');
// drift.cjs is a pure library that imports no gate or verify module, so it is loaded at the top.
// eslint-disable-next-line @typescript-eslint/no-require-imports -- drift.cjs is an export= CommonJS module
import driftModule = require('./drift.cjs');
const drift = driftModule as unknown as Record<string, unknown>;

/**
 * A mapped-commit stamp must be a hex object id (abbreviated to at least 7, or a full SHA-1 / SHA-256
 * id) before git sees it. A ref name (`HEAD`), a shorter abbreviation and an option-shaped value
 * (`--batch`) are not baselines: each is routed to `unresolvable-mapped-commit`, so no stamp can reach
 * `git cat-file` / `git diff` as anything but an object id.
 */
const HEX_OBJECT_ID_RE = /^[0-9a-f]{7,64}$/i;

const { planningDir, planningRoot } = planningWorkspace;
// Single owner of git C-quoted-path decoding (see #4081 note at the --name-status parse loop).
const { decodeGitQuotedPath } = worktreeSafetyMod;
const { REQUIRED_CODEBASE_MAP_FILES } = onboardProjectionMod;

/**
 * A generated document is read only when it is a regular file (symlinks followed) no larger than
 * this: a FIFO would block the gate forever and a huge file would exhaust memory.
 */
const MAX_DOCUMENT_BYTES = 1048576;

function readDocument(file: string): string {
  const st = fs.statSync(file);
  if (!st.isFile()) throw new Error('not a regular file');
  if (st.size > MAX_DOCUMENT_BYTES) throw new Error(`larger than ${MAX_DOCUMENT_BYTES} bytes`);
  return fs.readFileSync(file, 'utf-8');
}

/**
 * The drift gates' fixed non-answer payload; `skip` is a documented "nothing to compare", `unreadable`
 * is "could not look" (#5170) and exits UNAVAILABLE. Both keep the non-blocking payload.
 */
function nonAnswer(reason: string, extra: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    // Uniform gate contract: block = action_required (false when skipped).
    block: false,
    skipped: true,
    reason,
    action_required: false,
    directive: 'none',
    elements: [],
    ...extra,
  };
}

function skipped(reason: string, extra: Record<string, unknown> = {}): GateResult {
  return gateVerdict('skip', false, nonAnswer(reason, extra));
}

function unreadable(reason: string, extra: Record<string, unknown> = {}): GateResult {
  return gateUnreadable(false, nonAnswer(reason, extra));
}

function runCodebaseDriftGate(cwd: string): GateResult {
  const codebaseDir = path.join(planningDir(cwd), 'codebase');
  const structurePath = path.join(codebaseDir, 'STRUCTURE.md');

  let structureMd: string;
  try {
    structureMd = readDocument(structurePath);
  } catch (err) {
    if ((err as NodeJS.ErrnoException)?.code === 'ENOENT') {
      return skipped('no-structure-md');
    }
    return unreadable('cannot-read-structure-md: ' + (err instanceof Error ? err.message : String(err)));
  }

  const lastMapped = (drift['readMappedCommit'] as (p: string) => string | null)(structurePath);

  const revProbe = execGit(['rev-parse', 'HEAD'], { cwd }) as unknown as { exitCode: number; stdout: string };
  if (revProbe.exitCode !== 0) {
    return skipped('not-a-git-repo');
  }

  // #3418: an absent or unresolvable baseline means NO COMPARISON IS POSSIBLE.
  // It is neither zero drift nor total drift, and reporting it as either is a
  // lie the consumer cannot detect. The former fallback diffed HEAD against
  // the empty tree, so every tracked file read as newly added and the gate
  // reported maximum drift identically on every run -- which made a genuinely
  // stale map indistinguishable from a fresh one, and let `spawn_mapper` fire
  // a whole-repo remap while presenting itself as an incremental one.
  if (!lastMapped) {
    return skipped('no-mapped-commit', { last_mapped_commit: null });
  }
  // A stamp that is not a hex object id is not a baseline (a ref name such as `HEAD` would "resolve" to
  // a commit and diff against the wrong object; an option-shaped value must never reach git): same arm
  // as a stamp git cannot resolve, and no git call is made with it.
  if (!HEX_OBJECT_ID_RE.test(lastMapped)) {
    return unreadable('unresolvable-mapped-commit', { last_mapped_commit: lastMapped });
  }
  const baseProbe = execGit(['cat-file', '-t', lastMapped], { cwd }) as unknown as { exitCode: number; stdout: string };
  if (baseProbe.exitCode !== 0 || baseProbe.stdout.trim() !== 'commit') {
    // A stamp git cannot resolve: history rewrite, GC, or a shallow clone.
    // Distinct reason from 'no-mapped-commit' -- the map claims a baseline,
    // this repository just cannot see it, which is an operator-actionable
    // difference (re-map vs. unshallow). A resolvable non-commit (a tree or
    // blob sha) is the same class of bad baseline: git would happily diff
    // against it and report drift against the wrong object.
    // The repository cannot resolve the baseline the map claims: it could not look (#5170).
    return unreadable('unresolvable-mapped-commit', { last_mapped_commit: lastMapped });
  }
  const base = lastMapped;

  const diff = execGit(['diff', '--name-status', base, 'HEAD'], { cwd }) as unknown as { exitCode: number; stdout: string };
  if (diff.exitCode !== 0) {
    return unreadable('git-diff-failed');
  }

  // #3418: GSD's own planning artifacts are not codebase structure. A
  // map-codebase run commits `.planning/codebase/*.md`, so a correctly
  // stamped baseline would be re-poisoned by the very commit that carries
  // the stamp -- the next gate invocation would report the map's own seven
  // documents as seven new directories, back over the default threshold of
  // three. Derived from planningRoot() rather than a hardcoded literal so a
  // repoint of the planning root cannot leave this filter behind.
  //
  // `git diff --name-status` always prints repo-root-relative paths, so a cwd
  // below the root needs the `sub/` prefix or the filter matches nothing.
  // That prefix comes from git (`--show-prefix`: root-relative, forward
  // slashes, trailing slash, empty at the root). The rejected alternative was
  // path.relative(`--show-toplevel`, cwd), which mixes two path producers: on
  // Windows os.tmpdir() hands back the 8.3 short form while git resolves the
  // long one, so relative() between them yields a `../..` chain that matches
  // nothing. The `.planning` half below is safe to compute with relative()
  // because both of its sides are the same cwd string.
  const prefixProbe = execGit(['rev-parse', '--show-prefix'], { cwd }) as unknown as { exitCode: number; stdout: string };
  const repoPrefix = prefixProbe.exitCode === 0 ? prefixProbe.stdout.trim() : '';
  const planningPrefix = repoPrefix + path.relative(cwd, planningRoot(cwd)).split(path.sep).join('/') + '/';
  const isPlanningArtifact = (file: string): boolean => file.split('\\').join('/').startsWith(planningPrefix);

  const added: string[] = [];
  const modified: string[] = [];
  const deleted: string[] = [];
  for (const line of diff.stdout.split(/\r?\n/)) {
    if (!line.trim()) continue;
    const m = line.match(/^([A-Z])\d*\t(.+?)(?:\t(.+))?$/);
    if (!m) continue;
    const status = m[1];
    // execGit sets no core.quotepath config, so git's default `true` applies:
    // any path containing non-ASCII bytes (or `"`, `\`, control bytes) is
    // C-quoted — `"docs/\350\256\276…/overview.md"`. Capturing that verbatim
    // garbles affected_paths/elements and makes isPathMapped compare the
    // quoted prefix (`"docs`) against STRUCTURE.md, misclassifying DOCUMENTED
    // directories as new_dir (#4081). Decode with the single owner of the
    // git C-quote seam (worktree-safety.cjs); a non-quoted value — the plain
    // ASCII common case — passes through untouched. Both capture groups are
    // decoded: R/C lines carry old AND new paths, either may be quoted.
    // A rename is a deletion of the old path plus an addition of the new
    // one; a copy leaves its source in place and adds only the new path.
    const oldPath = decodeGitQuotedPath(m[2]);
    const newPath = m[3] ? decodeGitQuotedPath(m[3]) : oldPath;
    const put = (file: string, into: string[]): void => {
      if (!isPlanningArtifact(file)) into.push(file);
    };
    if (status === 'R') {
      put(oldPath, deleted);
      put(newPath, added);
    } else if (status === 'C') put(newPath, added);
    else if (status === 'A') put(newPath, added);
    else if (status === 'M' || status === 'T') put(newPath, modified);
    else if (status === 'D') put(newPath, deleted);
  }

  // Every generated document is territory the map describes, so all seven are
  // read (the one owner of the names is REQUIRED_CODEBASE_MAP_FILES).
  // STRUCTURE.md was read above; an unreadable other document is omitted and
  // named rather than sinking the whole check, and an absent one is simply
  // not part of this map (a `--fast` map writes four of the seven).
  const documents: Record<string, string> = {};
  const documentsRead: string[] = [];
  const documentsUnreadable: string[] = [];
  for (const name of REQUIRED_CODEBASE_MAP_FILES) {
    if (name === 'STRUCTURE.md') {
      documents[name] = structureMd;
      documentsRead.push(name);
      continue;
    }
    try {
      documents[name] = readDocument(path.join(codebaseDir, name));
      documentsRead.push(name);
    } catch (err) {
      if ((err as NodeJS.ErrnoException)?.code !== 'ENOENT') documentsUnreadable.push(name);
    }
  }

  // loadConfig() returns a flattened object — there is no nested `workflow`
  // key. Read the workflow-scoped keys through the quiet gate-config reader (the dot-path
  // resolver `config-get workflow.*` shares: workstream config first, then the project
  // root's; a missing or malformed config is "key absent", nothing is printed).
  const configuredThreshold = readWorkflowConfigValue(cwd, 'workflow.drift_threshold').value;
  const threshold =
    Number.isInteger(configuredThreshold) && (configuredThreshold as number) >= 1
      ? (configuredThreshold as number)
      : 3;
  const action = readWorkflowConfigValue(cwd, 'workflow.drift_action').value === 'auto-remap' ? 'auto-remap' : 'warn';

  const driftResult = (drift['detectDrift'] as (opts: unknown) => Record<string, unknown>)({
    addedFiles: added,
    modifiedFiles: modified,
    deletedFiles: deleted,
    documents,
    threshold,
    action,
    runtime: resolveRuntime(cwd),
  });

  const actionRequired = !!driftResult['actionRequired'];
  // Paths are attacker-controlled (they come from git); the raw values stay
  // in the library result, the CLI JSON carries display-safe renderings and
  // a bounded withheld list with its true size alongside.
  const display = drift['displaySafePath'] as (p: string) => string;
  const WITHHELD_LIST_CAP = 50;
  const withheldAll = (driftResult['withheldPaths'] as string[] | undefined) || [];
  const elementsRaw = (driftResult['elements'] as { category: string; path: string }[] | undefined) || [];
  const isSkipped = !!driftResult['skipped'];
  const payload = {
    // Uniform gate contract: block = action_required.
    block: actionRequired,
    skipped: isSkipped,
    reason: driftResult['reason'] || null,
    action_required: actionRequired,
    directive: driftResult['directive'],
    spawn_mapper: !!driftResult['spawnMapper'],
    affected_paths: driftResult['affectedPaths'] || [],
    withheld_paths: withheldAll.slice(0, WITHHELD_LIST_CAP).map((p) => display(p)),
    withheld_count: withheldAll.length,
    documents_read: documentsRead,
    documents_unreadable: documentsUnreadable,
    elements: elementsRaw.map((e) => ({ category: e.category, path: display(e.path) })),
    threshold,
    action,
    last_mapped_commit: lastMapped,
    message: driftResult['message'] || '',
  };
  // #5170: the drift was detected over fewer documents than the map has. A BLOCKING verdict stands
  // (more documents could only add territory the map describes); anything else was computed from
  // evidence the gate never saw and is `unreadable` (exit UNAVAILABLE), never a clean pass or skip.
  return actionRequired
    ? gateVerdict('block', true, payload)
    : documentsUnreadable.length > 0
      ? gateUnreadable(false, payload)
      : gateVerdict(isSkipped ? 'skip' : 'pass', false, payload);
}

export function evaluateCodebaseDriftGate(input: {
  projectDir: string;
  args: readonly string[];
  env?: NodeJS.ProcessEnv;
}): GateResult {
  // Non-blocking contract: a throw anywhere yields the non-blocking payload; the exit status says
  // "could not look" (#5170) rather than a clean exit 0.
  try {
    return runCodebaseDriftGate(input.projectDir);
  } catch (err) {
    return unreadable('exception: ' + (err && err instanceof Error ? err.message : String(err)));
  }
}
