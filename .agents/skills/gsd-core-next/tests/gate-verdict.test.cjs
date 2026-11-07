'use strict';

/**
 * V1, V2 — `GateVerdict` constructors (#5139, epic #5056, ADR-5057 §4 bullet 1, design D1).
 *
 * FAILING-FIRST (RED): `gsd-core/bin/lib/gate-verdict.cjs` (src/gate-verdict.cts) does not exist
 * on origin/next, so this file fails at require time.
 *
 * Contract (design D1):
 *   gateVerdict(outcome, block, payload) -> { outcome, block, payload }
 *     outcome in 'pass' | 'block' | 'skip' | 'advisory'; `block` is the gate's own decision,
 *     set explicitly; `payload` is the exact ordered object the router serializes, frozen.
 *   gateUsageFailure(code, message) -> { failure: { code, message } }
 *   isGateUsageFailure(result) narrows a GateResult: true only for usage failures.
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fc = require('./helpers/fast-check-setup.cjs');

const gateVerdictModule = require('../gsd-core/bin/lib/gate-verdict.cjs');

const { gateVerdict, gateUsageFailure, isGateUsageFailure } = gateVerdictModule;

const OUTCOMES = ['pass', 'block', 'skip', 'advisory'];

describe('V1 gate-verdict constructors', () => {
  test('V1a: the module exports the three functions', () => {
    assert.equal(typeof gateVerdict, 'function');
    assert.equal(typeof gateUsageFailure, 'function');
    assert.equal(typeof isGateUsageFailure, 'function');
  });

  test('V1b: gateVerdict sets outcome, block and payload verbatim, and nothing else', () => {
    const payload = { zebra: 1, alpha: 'x', nested: { b: 2, a: 1 }, list: [3, 1, 2] };
    const verdict = gateVerdict('block', true, payload);
    assert.deepStrictEqual(Object.keys(verdict), ['outcome', 'block', 'payload']);
    assert.equal(verdict.outcome, 'block');
    assert.equal(verdict.block, true);
    assert.deepStrictEqual(verdict.payload, { zebra: 1, alpha: 'x', nested: { b: 2, a: 1 }, list: [3, 1, 2] });
  });

  test('V1c: payload key order is preserved (insertion order, not sorted)', () => {
    const verdict = gateVerdict('pass', false, { passed: true, skipped: false, total: 0, covered: 0, uncovered: [], message: 'm' });
    assert.deepStrictEqual(Object.keys(verdict.payload), ['passed', 'skipped', 'total', 'covered', 'uncovered', 'message']);
    assert.equal(JSON.stringify(verdict.payload), '{"passed":true,"skipped":false,"total":0,"covered":0,"uncovered":[],"message":"m"}');
  });

  test('V1d: the payload object is frozen', () => {
    const verdict = gateVerdict('skip', false, { reason: 'r' });
    assert.equal(Object.isFrozen(verdict.payload), true);
    assert.throws(() => {
      verdict.payload.reason = 'changed';
    }, TypeError);
    assert.throws(() => {
      verdict.payload.added = 1;
    }, TypeError);
    assert.equal(verdict.payload.reason, 'r');
    assert.equal('added' in verdict.payload, false);
  });

  test('V1e: every outcome and both block values round-trip', () => {
    for (const outcome of OUTCOMES) {
      for (const block of [true, false]) {
        const verdict = gateVerdict(outcome, block, { k: outcome });
        assert.equal(verdict.outcome, outcome);
        assert.equal(verdict.block, block);
        assert.deepStrictEqual(verdict.payload, { k: outcome });
      }
    }
  });

  test('V1f: gateUsageFailure returns exactly { failure: { code, message } }', () => {
    const failure = gateUsageFailure('sdk_missing_arg', 'ui-plan-gate requires a phase argument: check ui-plan-gate <phase>');
    assert.deepStrictEqual(failure, {
      failure: { code: 'sdk_missing_arg', message: 'ui-plan-gate requires a phase argument: check ui-plan-gate <phase>' },
    });
    assert.deepStrictEqual(Object.keys(failure), ['failure']);
    assert.deepStrictEqual(Object.keys(failure.failure), ['code', 'message']);
  });

  test('V1g: isGateUsageFailure is true for usage failures and false for verdicts', () => {
    assert.equal(isGateUsageFailure(gateUsageFailure('usage', 'm')), true);
    for (const outcome of OUTCOMES) {
      assert.equal(isGateUsageFailure(gateVerdict(outcome, false, {})), false);
    }
  });

  test('V1h: isGateUsageFailure is false for non-result values without throwing', () => {
    for (const value of [null, undefined, 0, 1, '', 'failure', true, false, [], {}]) {
      assert.equal(isGateUsageFailure(value), false, `value ${JSON.stringify(value)}`);
    }
  });

  test('V1i: a verdict carries no failure key and a failure carries no verdict keys', () => {
    const verdict = gateVerdict('pass', false, {});
    const failure = gateUsageFailure('usage', 'm');
    assert.equal('failure' in verdict, false);
    assert.equal('outcome' in failure, false);
    assert.equal('block' in failure, false);
    assert.equal('payload' in failure, false);
  });
});

describe('V2 gate-verdict properties (fast-check)', () => {
  // Keys are prefixed so none is an integer-like key (those are reordered by the engine
  // regardless of insertion order, which is not what the contract is about).
  const keyArb = fc.string({ minLength: 1, maxLength: 8 }).map((s) => `k${s}`);
  const entriesArb = fc.uniqueArray(fc.tuple(keyArb, fc.oneof(fc.integer(), fc.string(), fc.boolean(), fc.constant(null))), {
    selector: (entry) => entry[0],
    maxLength: 12,
  });

  test('V2a: Object.keys(payload) equals the input key order for any key order', () => {
    fc.assert(
      fc.property(entriesArb, fc.constantFrom(...OUTCOMES), fc.boolean(), (entries, outcome, block) => {
        const input = Object.fromEntries(entries);
        const verdict = gateVerdict(outcome, block, input);
        assert.deepStrictEqual(Object.keys(verdict.payload), entries.map((entry) => entry[0]));
        for (const [key, value] of entries) {
          assert.equal(verdict.payload[key], value);
        }
        assert.equal(Object.isFrozen(verdict.payload), true);
        assert.equal(verdict.outcome, outcome);
        assert.equal(verdict.block, block);
      }),
    );
  });

  test('V2b: isGateUsageFailure is true only for failures', () => {
    fc.assert(
      fc.property(
        fc.string(),
        fc.string(),
        fc.constantFrom(...OUTCOMES),
        fc.boolean(),
        entriesArb,
        (code, message, outcome, block, entries) => {
          const failure = gateUsageFailure(code, message);
          assert.equal(isGateUsageFailure(failure), true);
          assert.deepStrictEqual(failure, { failure: { code, message } });
          assert.equal(isGateUsageFailure(gateVerdict(outcome, block, Object.fromEntries(entries))), false);
        },
      ),
    );
  });
});
