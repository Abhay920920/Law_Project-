'use strict';

/**
 * Gate positive-control harness (#5204, epic #5056, ADR-5057 §4 ratchet).
 *
 * A gate that has never been seen to fail cannot be trusted to pass. `gateControl` drives one gate
 * module to its FAILING verdict and to a DIFFERENT (green) verdict, through the gate's real
 * `evaluate*` export, and asserts both. `scripts/lint-gate-positive-control.cjs` fails CI when a gate
 * module has no `gateControl` call, so a new gate cannot land without one.
 *
 * `red` is the failing verdict the gate can reach:
 *   - `'block'`       a gate with a blocking arm: the red verdict carries `block: true` (its own
 *                     blocking decision, whatever its `outcome` label), the green one `block: false`.
 *   - `'unreadable'`  a gate that can never block (advisory / scope resolver): the failing verdict is
 *                     the typed `outcome: 'unreadable'` (`gateUnreadable`), the one non-pass outcome it
 *                     reaches; the green verdict is any other outcome.
 * The lint derives which of the two applies from the gate's own source and rejects a control that
 * declares the other, so a control cannot dodge a blocking verdict.
 *
 * A `{ failure }` usage failure is neither red nor green: a control whose scenario produced one is
 * a broken control, not a failing gate, and throws.
 */

const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { createTempProject, createTempGitProject, cleanup } = require('../helpers.cjs');
const { GIT_TIMEOUT_MS } = require('./timeouts.cjs');

const RED_OUTCOMES = Object.freeze(['block', 'unreadable']);

function isUsageFailure(result) {
  return result !== null && typeof result === 'object' && typeof result.failure === 'object' && result.failure !== null;
}

/**
 * Run one scenario in a fresh temp project and return the gate's verdict.
 * `scenario.git` makes the project a git repository first; `setup(dir)` may return a `restore`
 * function (for a monkeypatched fs method) and it always runs, even when the gate throws.
 */
function runScenario(evaluate, scenario) {
  const dir = scenario.git ? createTempGitProject('gate-control-') : createTempProject('gate-control-');
  let restore;
  try {
    restore = typeof scenario.setup === 'function' ? scenario.setup(dir) : undefined;
    const args = typeof scenario.args === 'function' ? scenario.args(dir) : scenario.args;
    return evaluate({ projectDir: dir, args: args ?? [] });
  } finally {
    if (typeof restore === 'function') restore();
    cleanup(dir);
  }
}

/**
 * Assert the red/green pair for already-computed verdicts. Pure, so the harness is testable without a
 * gate: throws unless `redVerdict` is the declared failing verdict and `greenVerdict` is not.
 */
function assertRedGreen(id, red, redVerdict, greenVerdict, expectRed) {
  assert.ok(RED_OUTCOMES.includes(red), `${id}: red must be one of ${RED_OUTCOMES.join(', ')}; got ${JSON.stringify(red)}`);
  for (const [label, verdict] of [['red', redVerdict], ['green', greenVerdict]]) {
    assert.ok(!isUsageFailure(verdict), `${id}: the ${label} scenario produced a usage failure, not a verdict: ${JSON.stringify(verdict)}`);
    assert.equal(typeof verdict?.outcome, 'string', `${id}: the ${label} scenario did not produce a GateVerdict`);
    assert.equal(typeof verdict.block, 'boolean', `${id}: the ${label} verdict carries no boolean block`);
  }
  if (red === 'block') {
    assert.equal(redVerdict.block, true, `${id}: the red scenario must block; got outcome '${redVerdict.outcome}', block ${redVerdict.block}`);
    assert.equal(greenVerdict.block, false, `${id}: the green scenario blocked (outcome '${greenVerdict.outcome}'); a control that cannot tell the two apart proves nothing`);
  } else {
    assert.equal(redVerdict.outcome, 'unreadable', `${id}: the red scenario must reach outcome 'unreadable'; got '${redVerdict.outcome}'`);
    assert.notEqual(greenVerdict.outcome, 'unreadable', `${id}: the green scenario reached 'unreadable'; a control that cannot tell the two apart proves nothing`);
  }
  if (expectRed !== undefined) assertExpectedArm(id, redVerdict, expectRed);
}

/**
 * Pin the red verdict to the arm the control is about: `outcome` is compared with the verdict's
 * outcome, every other key with the same key of its payload. A red scenario that fails for an unrelated
 * reason (a different arm) therefore does not pass as this gate's control.
 */
function assertExpectedArm(id, verdict, expectRed) {
  for (const [key, expected] of Object.entries(expectRed)) {
    const actual = key === 'outcome' ? verdict.outcome : verdict.payload?.[key];
    assert.deepStrictEqual(actual, expected, `${id}: the red verdict's ${key === 'outcome' ? 'outcome' : `payload.${key}`} is ${JSON.stringify(actual)}, expected ${JSON.stringify(expected)}; the red scenario reached a different arm`);
  }
}

const SPEC_KEYS = Object.freeze(['gate', 'module', 'fn', 'red', 'expectRed', 'redScenario', 'greenScenario']);
const SCENARIO_KEYS = Object.freeze(['git', 'setup', 'args']);

/** A misspelled option reads as `undefined` and would silently weaken a control, so it is rejected. */
function assertKnownKeys(id, what, value, known) {
  assert.ok(value !== null && typeof value === 'object', `${id}: ${what} must be an object`);
  const unknown = Object.keys(value).filter((key) => !known.includes(key));
  assert.deepEqual(unknown, [], `${id}: ${what} has unknown key(s) ${unknown.join(', ')}; known: ${known.join(', ')}`);
}

/**
 * Register the positive control for one gate module.
 *
 * @param {{
 *   gate: string,                      gate id: the `gate-<id>` module basename
 *   module: object,                    the required gate module (`require('../gsd-core/bin/lib/gate-<id>.cjs')`)
 *   fn: string,                        the exported `evaluate*` function the control drives
 *   red: 'block' | 'unreadable',
 *   expectRed: object,                 `outcome` and/or payload keys the red verdict must carry (the intended arm)
 *   redScenario: { git?: boolean, setup?: Function, args: string[] | Function },
 *   greenScenario: { git?: boolean, setup?: Function, args: string[] | Function },
 * }} spec
 */
function gateControl(spec) {
  assertKnownKeys(spec.gate, 'gateControl', spec, SPEC_KEYS);
  assertKnownKeys(spec.gate, 'redScenario', spec.redScenario, SCENARIO_KEYS);
  assertKnownKeys(spec.gate, 'greenScenario', spec.greenScenario, SCENARIO_KEYS);
  const { gate, module: gateModule, fn, red, expectRed, redScenario, greenScenario } = spec;
  assert.ok(expectRed !== null && typeof expectRed === 'object' && Object.keys(expectRed).length > 0, `${gate}: expectRed must name the arm the red scenario reaches (outcome and/or payload keys)`);
  test(`positive control: ${gate} reaches its failing verdict (${red}) and a different one`, () => {
    assert.equal(typeof gateModule?.[fn], 'function', `${gate}: the module does not export ${fn}`);
    const evaluate = gateModule[fn];
    assertRedGreen(gate, red, runScenario(evaluate, redScenario), runScenario(evaluate, greenScenario), expectRed);
  });
}

/** Write a file (creating parents) under `dir`. */
function put(dir, rel, content) {
  const p = path.join(dir, rel);
  fs.mkdirSync(path.dirname(p), { recursive: true });
  fs.writeFileSync(p, content);
}

/** Run git in `dir` with a fixed identity, no signing, and a bounded timeout. */
function git(dir, ...args) {
  return execFileSync(
    'git',
    ['-c', 'user.name=gate-control', '-c', 'user.email=gate-control@example.invalid', '-c', 'commit.gpgsign=false', ...args],
    { cwd: dir, encoding: 'utf-8', stdio: 'pipe', timeout: GIT_TIMEOUT_MS, windowsHide: true },
  );
}

/**
 * Make `fs.readFileSync` throw `code` for any path ending in `suffix`; returns the `restore` function.
 * Injected by monkeypatching the method (never a chmod: root bypasses mode bits).
 */
function failRead(suffix, code = 'EACCES') {
  const real = fs.readFileSync;
  fs.readFileSync = function patched(p, ...rest) {
    if (String(p).endsWith(suffix)) {
      const err = new Error(`${code}: simulated read failure`);
      err.code = code;
      throw err;
    }
    return real.call(fs, p, ...rest);
  };
  return function restore() { fs.readFileSync = real; };
}

module.exports = { gateControl, assertExpectedArm, assertKnownKeys, SPEC_KEYS, SCENARIO_KEYS, assertRedGreen, runScenario, put, git, failRead, RED_OUTCOMES };
