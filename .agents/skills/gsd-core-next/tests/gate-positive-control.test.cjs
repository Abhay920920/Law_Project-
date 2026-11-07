'use strict';

/**
 * #5204 (epic #5056, ADR-5057 §4 ratchet): every gate module has a positive control.
 *
 * Each `gateControl` below drives one gate through its real `evaluate*` export to the verdict that
 * makes it fail and to a different, green one, so a refactor that makes a failing arm unreachable —
 * or a gate that can no longer tell its two answers apart — fails here. `expectRed` pins the red
 * verdict to the arm the control is about, so a scenario that fails for an unrelated reason does not
 * count. The lint `scripts/lint-gate-positive-control.cjs` fails CI on a gate module with no control
 * in a top-level `gateControl` call (or a control declaring the wrong failing verdict), so a new gate
 * cannot land without one.
 *
 * Scenarios are the red and green arms the per-gate unit tests (tests/gate-*.unit.test.cjs) already
 * pin byte-for-byte; here they are asserted as a PAIR through one harness.
 */

const fs = require('node:fs');
const path = require('node:path');
const { cleanup } = require('./helpers.cjs');
const { gateControl, put, git, failRead } = require('./helpers/gate-positive-control.cjs');

const ROADMAP_FRONTEND = ['# Roadmap', '', '### Phase 1: Dashboard frontend', '**Goal**: Build the React dashboard UI for operators', ''].join('\n');
const DECISION_CONTEXT = ['<decisions>', '- **D-01:** Use PostgreSQL for the primary datastore layer', '</decisions>', ''].join('\n');
const REQUIREMENTS = ['# Requirements', '', '- [ ] **REQ-01**: Users can log in', ''].join('\n');
const PHASE_DIR = '.planning/phases/01-x';
const CONTEXT_PATH = `${PHASE_DIR}/01-CONTEXT.md`;
const PLAN_PATH = `${PHASE_DIR}/01-01-PLAN.md`;

/** A plan with one `<task>` per automated verify command (the verify-command-paths gate's input). */
function planWithCommands(commands) {
  return ['# Plan', '']
    .concat(commands.map((cmd, i) => [
      '<task type="auto">',
      `  <name>task-${i}</name>`,
      '  <files></files>',
      '  <action>do the thing</action>',
      `  <verify><automated>${cmd}</automated></verify>`,
      '  <acceptance_criteria>it works</acceptance_criteria>',
      '  <done>committed</done>',
      '</task>',
    ].join('\n')))
    .join('\n');
}

/** A plan with one task whose verify command states (or omits) its failing direction. */
function planWithFailsWhen(parts) {
  return ['# Plan', '', '<task type="auto">', '  <name>task-0</name>', '  <action>do the thing</action>']
    .concat(parts.map((p) => ('automated' in p
      ? `  <verify><automated>${p.automated}</automated></verify>`
      : `  <fails_when>${p.failsWhen}</fails_when>`)))
    .concat(['  <done>committed</done>', '</task>'])
    .join('\n');
}

function commitAll(dir, message) {
  git(dir, 'add', '-A');
  git(dir, 'commit', '-m', message);
}

/** Phase 3's directory plus one commit scoped to it: the repository shape the scope resolver reads. */
function scaffoldScopedPhase(dir) {
  put(dir, '.planning/config.json', '{}');
  put(dir, '.planning/phases/03-scope/03-CONTEXT.md', 'context\n');
  commitAll(dir, 'docs(03): scaffold phase');
  put(dir, 'src/z.js', 'z\n');
  commitAll(dir, 'feat(03-01): z');
}

/** A frontend phase whose HEAD commit touches a UI file; `withSpec` adds the UI-SPEC the gate asks for. */
function scaffoldUiChange(withSpec) {
  return (dir) => {
    put(dir, '.planning/ROADMAP.md', ROADMAP_FRONTEND);
    if (withSpec) put(dir, '.planning/phases/01-dashboard/01-UI-SPEC.md', '# spec\n');
    else put(dir, '.planning/phases/01-dashboard/01-01-PLAN.md', '# p\n');
    put(dir, 'src/components/Button.tsx', 'export const B = 1;\n');
    commitAll(dir, 'feat: add button');
  };
}

/** REQUIREMENTS.md, a decision, and a plan citing both: the gap-analysis gate's readable input. */
function scaffoldGapAnalysis(dir) {
  put(dir, '.planning/REQUIREMENTS.md', REQUIREMENTS);
  put(dir, CONTEXT_PATH, DECISION_CONTEXT);
  put(dir, PLAN_PATH, '<objective>REQ-01 and D-01</objective>\n');
}

// ─── blocking gates: red = a verdict that blocks, green = one that does not ─────────────────────────

gateControl({
  gate: 'api-coverage-verify-pre',
  module: require('../gsd-core/bin/lib/gate-api-coverage-verify-pre.cjs'),
  fn: 'evaluateApiCoverageVerifyPre',
  red: 'block',
  expectRed: { phase_lookup_failed: true },
  // A phases tree exists but the phase cannot be resolved: fail closed.
  redScenario: { args: ['99'] },
  // No phases tree at all: not a GSD layout, the gate is skipped open.
  greenScenario: { setup: (dir) => { cleanup(path.join(dir, '.planning', 'phases')); }, args: ['01'] },
});

gateControl({
  gate: 'decision-coverage-plan',
  module: require('../gsd-core/bin/lib/gate-decision-coverage-plan.cjs'),
  fn: 'evaluateDecisionCoveragePlan',
  red: 'block',
  expectRed: { reason: 'could-not-parse' },
  // A decision-shaped block with no extractable bullet: could-not-parse blocks.
  redScenario: {
    setup: (dir) => put(dir, CONTEXT_PATH, ['<decisions>', '- **DEC-01:** Unsupported id grammar', '</decisions>', ''].join('\n')),
    args: [PHASE_DIR, '--context', CONTEXT_PATH],
  },
  // The decision is cited in a plan objective.
  greenScenario: {
    setup: (dir) => {
      put(dir, CONTEXT_PATH, DECISION_CONTEXT);
      put(dir, PLAN_PATH, ['---', 'phase: 1', '---', '<objective>Honor D-01 in the storage layer</objective>', ''].join('\n'));
    },
    args: [PHASE_DIR, '--context', CONTEXT_PATH],
  },
});

gateControl({
  gate: 'tdd-red-evidence',
  module: require('../gsd-core/bin/lib/gate-tdd-red-evidence.cjs'),
  fn: 'evaluateTddRedEvidence',
  red: 'block',
  expectRed: { verdict: 'INVALID_RED', reason: 'unreadable_record' },
  // No record file: INVALID_RED blocks GREEN.
  redScenario: { args: (dir) => [path.join(dir, 'red.json')] },
  // The target test failed on purpose (exit 1, a failing TAP subtest): RED_EVIDENCE_OK.
  greenScenario: {
    setup: (dir) => put(dir, 'red.json', JSON.stringify({
      command: 'node --test t.test.cjs',
      exitCode: 1,
      output: 'TAP version 13\n# Subtest: target\nnot ok 1 - target\n  ---\n  duration_ms: 0.97\n  type: \'test\'\n  failureType: \'testCodeFailure\'\n  error: \'expected 1 to equal 2\'\n  code: \'ERR_ASSERTION\'\n  ...\n1..1\n# tests 1\n# suites 0\n# pass 0\n# fail 1\n',
      targetTest: 'target',
      targetFile: 't.test.cjs',
      expected: '2',
      actual: '1',
    })),
    args: (dir) => [path.join(dir, 'red.json')],
  },
});

gateControl({
  gate: 'tdd-review-checkpoint',
  module: require('../gsd-core/bin/lib/gate-tdd-review-checkpoint.cjs'),
  fn: 'evaluateTddReviewCheckpoint',
  red: 'block',
  expectRed: { violations: 1 },
  // A type:tdd plan with no gate commits: violations, block true.
  redScenario: {
    git: true,
    setup: (dir) => put(dir, PLAN_PATH, ['---', 'phase: 1', 'plan: 1', 'type: tdd', '---', '# Plan', ''].join('\n')),
    args: ['1'],
  },
  // No type:tdd plan in the phase: the review is skipped.
  greenScenario: {
    git: true,
    setup: (dir) => put(dir, PLAN_PATH, ['---', 'phase: 1', 'plan: 1', 'type: execute', '---', '# Plan', ''].join('\n')),
    args: ['1'],
  },
});

gateControl({
  gate: 'ui-plan',
  module: require('../gsd-core/bin/lib/gate-ui-plan.cjs'),
  fn: 'evaluateUiPlanGate',
  red: 'block',
  expectRed: { frontend: true, hasUiSpec: false },
  // A frontend phase with static evidence and no UI-SPEC.
  redScenario: {
    setup: (dir) => {
      put(dir, '.planning/ROADMAP.md', ROADMAP_FRONTEND);
      put(dir, 'package.json', '{"dependencies":{"react":"^18.0.0"}}');
      put(dir, '.planning/phases/01-dashboard/01-01-PLAN.md', '# p\n');
    },
    args: ['1'],
  },
  // No ROADMAP.md: not a frontend phase, nothing blocks.
  greenScenario: { args: ['1'] },
});

gateControl({
  gate: 'ui-safety',
  module: require('../gsd-core/bin/lib/gate-ui-safety.cjs'),
  fn: 'evaluateUiSafetyGate',
  red: 'block',
  expectRed: { frontend: true, hasUiFiles: true, hasUiSpec: false },
  // A frontend phase whose HEAD commit touches a UI file, with no UI-SPEC.
  redScenario: { git: true, setup: scaffoldUiChange(false), args: ['1'] },
  // The same change with a UI-SPEC present.
  greenScenario: { git: true, setup: scaffoldUiChange(true), args: ['1'] },
});

gateControl({
  gate: 'predicate',
  module: require('../gsd-core/bin/lib/gate-predicate.cjs'),
  fn: 'evaluateCheckPredicate',
  red: 'block',
  expectRed: { message: 'command exited 3' },
  redScenario: { args: ['--predicate', '{"kind":"command-exit-zero","command":"exit 3"}'] },
  greenScenario: { args: ['--predicate', '{"kind":"command-exit-zero","command":"exit 0"}'] },
});

gateControl({
  gate: 'verify-command-paths',
  module: require('../gsd-core/bin/lib/gate-verify-command-paths.cjs'),
  fn: 'evaluateVerifyCommandPaths',
  red: 'block',
  expectRed: { status: 'broken' },
  // A verify command whose `cd` target does not exist.
  redScenario: {
    setup: (dir) => put(dir, PLAN_PATH, planWithCommands(['cd nowhere && npm test'])),
    args: ['1'],
  },
  // The same shape with a target that resolves.
  greenScenario: {
    setup: (dir) => {
      put(dir, 'good/package.json', '{"name":"fx","scripts":{"test":"node --version"}}');
      put(dir, PLAN_PATH, planWithCommands(['cd good && npm test']));
    },
    args: ['1'],
  },
});

gateControl({
  gate: 'verify-failure-directions',
  module: require('../gsd-core/bin/lib/gate-verify-failure-directions.cjs'),
  fn: 'evaluateVerifyFailureDirections',
  red: 'block',
  expectRed: { status: 'blocked' },
  // A verify command with no stated failing direction.
  redScenario: {
    setup: (dir) => put(dir, PLAN_PATH, planWithFailsWhen([{ automated: 'npm test' }])),
    args: ['1'],
  },
  // The same command with its failing direction stated.
  greenScenario: {
    setup: (dir) => put(dir, PLAN_PATH, planWithFailsWhen([{ automated: 'npm test' }, { failsWhen: 'non-zero exit' }])),
    args: ['1'],
  },
});

// ─── gates that never block: red = the typed `unreadable` outcome, green = any other ────────────────

gateControl({
  gate: 'decision-coverage-verify',
  module: require('../gsd-core/bin/lib/gate-decision-coverage-verify.cjs'),
  fn: 'evaluateDecisionCoverageVerify',
  red: 'unreadable',
  expectRed: { outcome: 'unreadable', reason: 'unreadable evidence' },
  // CONTEXT.md exists but cannot be read: never "no trackable decisions".
  redScenario: {
    setup: (dir) => {
      put(dir, CONTEXT_PATH, DECISION_CONTEXT);
      return failRead('01-CONTEXT.md');
    },
    args: [PHASE_DIR, CONTEXT_PATH],
  },
  // CONTEXT.md with nothing trackable: a skip.
  greenScenario: {
    setup: (dir) => put(dir, CONTEXT_PATH, '# Phase\n\nNothing decision-shaped here.\n'),
    args: [PHASE_DIR, CONTEXT_PATH],
  },
});

gateControl({
  gate: 'gap-analysis-plan-post',
  module: require('../gsd-core/bin/lib/gate-gap-analysis-plan-post.cjs'),
  fn: 'evaluateGapAnalysisPlanPost',
  red: 'unreadable',
  expectRed: { outcome: 'unreadable', passed: false },
  // REQUIREMENTS.md exists but cannot be read: the table would be computed over evidence never seen.
  redScenario: {
    setup: (dir) => {
      scaffoldGapAnalysis(dir);
      return failRead('REQUIREMENTS.md');
    },
    args: [PHASE_DIR, 'REQ-01'],
  },
  // Everything readable: the advisory table.
  greenScenario: { setup: scaffoldGapAnalysis, args: [PHASE_DIR, 'REQ-01'] },
});

gateControl({
  gate: 'evaluation-scope',
  module: require('../gsd-core/bin/lib/gate-evaluation-scope.cjs'),
  fn: 'evaluateEvaluationScope',
  red: 'unreadable',
  expectRed: { outcome: 'unreadable', status: 'unresolvable' },
  // A phase with no directory in the project: the scope cannot be resolved ("could not look").
  redScenario: { git: true, setup: scaffoldScopedPhase, args: ['--phase', '9'] },
  // A phase with a directory and a scoped commit but no SUMMARY: resolved, advisory.
  greenScenario: { git: true, setup: scaffoldScopedPhase, args: ['--phase', '3'] },
});

// ─── #5219 (ADR-5057 §4 arm C): the drift and prohibition gates ─────────────────────────────────────

/** A plan whose frontmatter declares `files_modified` (the drift gates' input). */
function planDeclaring(files) {
  return `---\nphase: 01\nfiles_modified:\n${files.map((f) => `  - ${f}`).join('\n')}\n---\n\n# Plan\n`;
}

gateControl({
  gate: 'schema-drift',
  module: require('../gsd-core/bin/lib/gate-schema-drift.cjs'),
  fn: 'evaluateSchemaDriftGate',
  red: 'block',
  expectRed: { drift_detected: true, unpushed_orms: ['prisma'] },
  // A plan that modifies a Prisma schema and no push evidenced anywhere in the phase.
  redScenario: {
    git: true,
    setup: (dir) => put(dir, '.planning/phases/01-schema/01-01-PLAN.md', planDeclaring(['prisma/schema.prisma'])),
    args: ['1'],
  },
  // The same phase whose plan touches no schema file.
  greenScenario: {
    git: true,
    setup: (dir) => put(dir, '.planning/phases/01-schema/01-01-PLAN.md', planDeclaring(['src/a.js'])),
    args: ['1'],
  },
});

/** One mapped-and-stamped repository; `extraDirs` are new top-level directories added after the stamp. */
function scaffoldMappedCodebase(extraDirs) {
  return (dir) => {
    put(dir, '.planning/codebase/STRUCTURE.md', '# Structure\n\n- `src/` application sources\n');
    put(dir, 'src/main.js', 'main\n');
    commitAll(dir, 'feat: baseline');
    const baseline = git(dir, 'rev-parse', 'HEAD').trim();
    put(dir, '.planning/codebase/STRUCTURE.md', `---\nlast_mapped_commit: ${baseline}\n---\n# Structure\n\n- \`src/\` application sources\n`);
    commitAll(dir, 'docs: stamp the map');
    for (const extra of extraDirs) put(dir, `${extra}/index.js`, `${extra}\n`);
    if (extraDirs.length > 0) commitAll(dir, 'feat: add directories');
  };
}

gateControl({
  gate: 'codebase-drift',
  module: require('../gsd-core/bin/lib/gate-codebase-drift.cjs'),
  fn: 'evaluateCodebaseDriftGate',
  red: 'block',
  expectRed: { action_required: true, threshold: 3 },
  // Three directories the map does not describe appeared since its stamped commit: the default threshold.
  redScenario: { git: true, setup: scaffoldMappedCodebase(['alpha', 'beta', 'gamma']), args: [] },
  // Nothing changed since the stamp.
  greenScenario: { git: true, setup: scaffoldMappedCodebase([]), args: [] },
});

/** A phase whose CONTEXT.md is newer (by mtime, set explicitly) or older than its RESEARCH.md; `action` is the configured policy. */
function scaffoldContext(researchEpochSeconds) {
  return (dir) => {
    put(dir, '.planning/config.json', JSON.stringify({ workflow: { context_drift_action: 'block' } }));
    put(dir, '.planning/phases/01-x/01-CONTEXT.md', '# context\n');
    put(dir, '.planning/phases/01-x/01-RESEARCH.md', '# research\n');
    fs.utimesSync(path.join(dir, '.planning/phases/01-x/01-CONTEXT.md'), 2000, 2000);
    fs.utimesSync(path.join(dir, '.planning/phases/01-x/01-RESEARCH.md'), researchEpochSeconds, researchEpochSeconds);
  };
}

gateControl({
  gate: 'context-drift',
  module: require('../gsd-core/bin/lib/gate-context-drift.cjs'),
  fn: 'evaluateContextDriftGate',
  red: 'block',
  expectRed: { stale_artifacts: ['01-RESEARCH.md'], action: 'block' },
  // The research was last changed BEFORE the context decisions, under the `block` policy.
  redScenario: { setup: scaffoldContext(1000), args: ['1'] },
  // The research is newer than the context: in sync.
  greenScenario: { setup: scaffoldContext(3000), args: ['1'] },
});

/** Make the producer's disposition function throw (the one throw the producer's own no-throw handling does not catch). */
function failDisposition() {
  const probeCore = require('../gsd-core/bin/lib/probe-core.cjs');
  const real = probeCore.dispositionForProhibition;
  probeCore.dispositionForProhibition = function patched() {
    throw new Error('simulated disposition failure');
  };
  return function restore() { probeCore.dispositionForProhibition = real; };
}

const PROHIBITION_REQUEST = JSON.stringify({ prohibition: { verification: 'test', text: 'never log secrets' }, check: null });

gateControl({
  gate: 'prohibition-enforcement',
  module: require('../gsd-core/bin/lib/gate-prohibition-enforcement.cjs'),
  fn: 'evaluateProhibitionEnforcementGate',
  red: 'unreadable',
  expectRed: { outcome: 'unreadable', status: 'unverified', flagged: true, located: false },
  // The producer throws: never a crash and never a silent green.
  redScenario: {
    setup: (dir) => {
      put(dir, 'req.json', PROHIBITION_REQUEST);
      return failDisposition();
    },
    args: (dir) => [path.join(dir, 'req.json')],
  },
  // The same request, the producer unpatched: its advisory disposition.
  greenScenario: {
    setup: (dir) => put(dir, 'req.json', PROHIBITION_REQUEST),
    args: (dir) => [path.join(dir, 'req.json')],
  },
});
