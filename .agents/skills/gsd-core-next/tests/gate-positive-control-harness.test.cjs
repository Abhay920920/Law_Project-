'use strict';

/**
 * #5204 (epic #5056, ADR-5057 §4 ratchet): the gate positive-control harness
 * (tests/helpers/gate-positive-control.cjs) rejects a control that proves nothing.
 *
 * `assertRedGreen` is pure, so each way a control can be vacuous is driven here without a gate:
 * the failing verdict never reached, a green that cannot be told from the red, a scenario that
 * produced a usage failure instead of a verdict, and a failing outcome the harness does not define.
 */

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const {
  assertRedGreen, assertExpectedArm, assertKnownKeys, SPEC_KEYS, SCENARIO_KEYS, runScenario, failRead, RED_OUTCOMES,
} = require('./helpers/gate-positive-control.cjs');

const verdict = (outcome, block) => ({ outcome, block, payload: {} });

describe('assertRedGreen — accepts a discriminating pair', () => {
  test('assertRedGreenAcceptsAnAcceptedPair: block', () => {
    assert.doesNotThrow(() => assertRedGreen('g', 'block', verdict('block', true), verdict('pass', false)));
  });

  test('a blocking verdict need not be labelled `block` (an advisory outcome that blocks is red)', () => {
    assert.doesNotThrow(() => assertRedGreen('g', 'block', verdict('advisory', true), verdict('pass', false)));
  });

  test('assertRedGreenAcceptsAnAcceptedPair: unreadable', () => {
    assert.doesNotThrow(() => assertRedGreen('g', 'unreadable', verdict('unreadable', false), verdict('advisory', false)));
  });
});

describe('assertRedGreen — rejects a control that proves nothing', () => {
  test('assertRedGreenRejectsMissingRed: the red scenario does not block', () => {
    assert.throws(() => assertRedGreen('g', 'block', verdict('pass', false), verdict('pass', false)), /must block/);
  });

  test('assertRedGreenRejectsMissingRed: the red scenario is not unreadable', () => {
    assert.throws(() => assertRedGreen('g', 'unreadable', verdict('advisory', false), verdict('pass', false)), /must reach outcome 'unreadable'/);
  });

  test('assertRedGreenRejectsIndistinguishableGreen: green blocks too', () => {
    assert.throws(() => assertRedGreen('g', 'block', verdict('block', true), verdict('block', true)), /green scenario blocked/);
  });

  test('assertRedGreenRejectsIndistinguishableGreen: green is unreadable too', () => {
    assert.throws(() => assertRedGreen('g', 'unreadable', verdict('unreadable', false), verdict('unreadable', false)), /green scenario reached 'unreadable'/);
  });

  test('assertRedGreenRejectsUsageFailure: either scenario', () => {
    const usage = { failure: { code: 'usage', message: 'bad args' } };
    assert.throws(() => assertRedGreen('g', 'block', usage, verdict('pass', false)), /red scenario produced a usage failure/);
    assert.throws(() => assertRedGreen('g', 'block', verdict('block', true), usage), /green scenario produced a usage failure/);
  });

  test('a scenario that returned no verdict at all is rejected', () => {
    assert.throws(() => assertRedGreen('g', 'block', undefined, verdict('pass', false)), /did not produce a GateVerdict/);
    assert.throws(() => assertRedGreen('g', 'block', { outcome: 'block' }, verdict('pass', false)), /no boolean block/);
  });

  test('assertRedGreenRejectsUnknownRed: only block and unreadable are failing verdicts', () => {
    assert.deepEqual([...RED_OUTCOMES], ['block', 'unreadable']);
    for (const red of ['pass', 'skip', 'advisory', '', undefined]) {
      assert.throws(() => assertRedGreen('g', red, verdict('block', true), verdict('pass', false)), /red must be one of/);
    }
  });
});

describe('expectRed — the red verdict must reach the intended arm', () => {
  const red = { outcome: 'block', block: true, payload: { reason: 'could-not-parse', total: 0 } };
  const green = verdict('pass', false);

  test('a matching outcome and payload keys pass', () => {
    assert.doesNotThrow(() => assertRedGreen('g', 'block', red, green, { outcome: 'block', reason: 'could-not-parse' }));
  });

  test('redForAnUnrelatedReasonIsRejected: right block flag, wrong arm', () => {
    assert.throws(() => assertRedGreen('g', 'block', red, green, { reason: 'unreadable_record' }), /reached a different arm/);
    assert.throws(() => assertRedGreen('g', 'block', red, green, { outcome: 'unreadable' }), /outcome is "block"/);
  });

  test('a payload key the verdict does not carry is rejected (undefined is not a match)', () => {
    assert.throws(() => assertRedGreen('g', 'block', red, green, { missing: undefined, other: 1 }), /payload\.other/);
  });

  test('assertExpectedArm compares values strictly', () => {
    assert.throws(() => assertExpectedArm('g', red, { total: '0' }), /reached a different arm/);
    assert.doesNotThrow(() => assertExpectedArm('g', red, { total: 0 }));
  });
});

describe('assertKnownKeys — a misspelled option does not silently weaken a control', () => {
  test('misspelledSpecKeyIsRejected', () => {
    assert.throws(() => assertKnownKeys('g', 'gateControl', { gate: 'g', redScenarioo: {} }, SPEC_KEYS), /unknown key\(s\) redScenarioo/);
  });

  test('misspelledScenarioKeyIsRejected', () => {
    assert.throws(() => assertKnownKeys('g', 'redScenario', { arg: [] }, SCENARIO_KEYS), /unknown key\(s\) arg/);
  });

  test('knownKeysAndNonObjectsAreChecked', () => {
    assert.doesNotThrow(() => assertKnownKeys('g', 'redScenario', { git: true, setup() {}, args: [] }, SCENARIO_KEYS));
    assert.throws(() => assertKnownKeys('g', 'redScenario', undefined, SCENARIO_KEYS), /must be an object/);
  });
});

describe('runScenario — isolation', () => {
  test('runScenarioAlwaysRestores: the monkeypatch is undone when the gate throws, and the project is removed', () => {
    const original = fs.readFileSync;
    let seenDir;
    const boom = (input) => { seenDir = input.projectDir; throw new Error('gate exploded'); };
    assert.throws(() => runScenario(boom, { setup: () => failRead('anything.md'), args: [] }), /gate exploded/);
    assert.equal(fs.readFileSync, original, 'fs.readFileSync is restored');
    assert.equal(fs.existsSync(seenDir), false, 'the temp project is removed');
  });

  test('failRead throws the requested code only for the matching suffix', () => {
    const restore = failRead('target.md', 'EIO');
    try {
      assert.throws(() => fs.readFileSync('/x/target.md', 'utf8'), (error) => error.code === 'EIO');
      assert.doesNotThrow(() => fs.readFileSync(__filename, 'utf8'));
    } finally {
      restore();
    }
  });

  test('each scenario gets its own project: state does not leak between runs', () => {
    const dirs = [];
    const record = (input) => { dirs.push(input.projectDir); return verdict('pass', false); };
    runScenario(record, { args: [] });
    runScenario(record, { args: [] });
    assert.equal(new Set(dirs).size, 2);
  });
});
