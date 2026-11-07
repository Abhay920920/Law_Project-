'use strict';

/**
 * In-process GateResult tests for `check prohibition-enforcement` (#5219, epic #5056, ADR-5057 §4 arm C).
 *
 * `evaluateProhibitionEnforcementGate({ projectDir, args })` returns a GateResult: an `advisory`
 * verdict (the producer's disposition is a delivered answer, never `block`) or a GateUsageFailure for a
 * request that is absent or does not parse. The byte-for-byte stdout / exit-status equivalence with the
 * pre-move code is pinned by tests/check-router-cutover-equivalence.test.cjs (E15); this file pins the
 * returned value against `runProhibitionEnforcement` itself, the request grammar (file path, `--json`),
 * hostile and malformed requests, the no-throw contract (a throw is an unreadable, fail-closed verdict,
 * never a silent green) and that the gate never writes to stdout / stderr.
 *
 * Only descriptors the producer rejects as not locatable are used: a locatable check would spawn the
 * wired test or lint rule, which is the producer's own suite's concern (tests/prohibition-enforcement.test.cjs).
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fc = require('fast-check');
const path = require('node:path');
const { createTempProject, cleanup } = require('./helpers.cjs');
const { put } = require('./helpers/gate-positive-control.cjs');

const gate = require('../gsd-core/bin/lib/gate-prohibition-enforcement.cjs');
const producer = require('../gsd-core/bin/lib/prohibition-enforcement.cjs');
const probeCore = require('../gsd-core/bin/lib/probe-core.cjs');
const { isGateUsageFailure } = require('../gsd-core/bin/lib/gate-verdict.cjs');

const USAGE = {
  failure: {
    code: 'sdk_missing_arg',
    message: 'prohibition-enforcement requires a JSON request: check prohibition-enforcement <request.json> | --json \'{"prohibition":{...},"check":{...}}\'',
  },
};

const PROHIBITION = { verification: 'test', text: 'never log secrets' };
const REQUEST = { prohibition: PROHIBITION, check: null };

/** Run the gate in a fresh project, asserting it wrote nothing to stdout / stderr. */
function evaluate(args, setup) {
  const dir = createTempProject('gate-prohibition-');
  const writes = [];
  const outWrite = process.stdout.write;
  const errWrite = process.stderr.write;
  let restore;
  try {
    restore = setup ? setup(dir) : undefined;
    process.stdout.write = (chunk) => { writes.push(String(chunk)); return true; };
    process.stderr.write = (chunk) => { writes.push(String(chunk)); return true; };
    const result = gate.evaluateProhibitionEnforcementGate({ projectDir: dir, args: typeof args === 'function' ? args(dir) : args });
    process.stdout.write = outWrite;
    process.stderr.write = errWrite;
    assert.deepStrictEqual(writes, [], 'a gate module must not write to stdout/stderr');
    return result;
  } finally {
    process.stdout.write = outWrite;
    process.stderr.write = errWrite;
    if (typeof restore === 'function') restore();
    cleanup(dir);
  }
}

/** The gate's result for an inline `--json` request. */
const inline = (request) => evaluate(['--json', typeof request === 'string' ? request : JSON.stringify(request)]);

describe('evaluateProhibitionEnforcementGate: usage', () => {
  test('no argument is a usage failure carrying the old error() text and code', () => {
    const result = evaluate([]);
    assert.equal(isGateUsageFailure(result), true);
    assert.deepStrictEqual(result, USAGE);
  });

  test('an empty path argument is the same usage failure', () => {
    assert.deepStrictEqual(evaluate(['']), USAGE);
  });

  test('a request file that does not exist is the usage failure', () => {
    assert.deepStrictEqual(evaluate((dir) => [path.join(dir, 'absent.json')]), USAGE);
  });

  test('a request file that is a directory is the usage failure', () => {
    assert.deepStrictEqual(evaluate((dir) => [dir]), USAGE);
  });

  test('a request file that is not JSON is the usage failure', () => {
    assert.deepStrictEqual(evaluate((dir) => [path.join(dir, 'bad.json')], (dir) => put(dir, 'bad.json', '{not json\n')), USAGE);
  });

  test('--json with invalid inline JSON is the usage failure', () => {
    assert.deepStrictEqual(inline('{not json'), USAGE);
  });

  test('--json with no value falls back to the path form and is the usage failure', () => {
    assert.deepStrictEqual(evaluate(['--json']), USAGE);
  });

  // A document with nothing to read a request from is a usage failure, never a throw.
  for (const [label, body] of [['null', 'null'], ['an empty document', '']]) {
    test(`a request document that is ${label} is a usage failure`, () => {
      assert.deepStrictEqual(inline(body), USAGE);
    });
  }

  // A scalar parses; its `check` and `prohibition` are simply absent (fail-closed disposition).
  for (const [label, body] of [['a number', '7'], ['a string', '"x"']]) {
    test(`a request document that is ${label} parses and is delivered as the fail-closed advisory verdict`, () => {
      const result = inline(body);
      assert.equal(isGateUsageFailure(result), false);
      assert.equal(result.outcome, 'advisory');
      assert.equal(result.payload.flagged, true);
      assert.equal(result.payload.located, false);
    });
  }
});

describe('evaluateProhibitionEnforcementGate: the producer\'s disposition is delivered as an advisory verdict', () => {
  test('a request with no locatable check equals runProhibitionEnforcement and never blocks', () => {
    const result = inline(REQUEST);
    assert.equal(isGateUsageFailure(result), false);
    assert.equal(result.outcome, 'advisory');
    assert.equal(result.block, false);
    assert.deepStrictEqual(result.payload, producer.runProhibitionEnforcement(PROHIBITION, null, {}));
    assert.equal(result.payload.status, 'unverified');
    assert.equal(result.payload.flagged, true);
    assert.equal(result.payload.located, false);
    assert.deepStrictEqual(Object.keys(result.payload), Object.keys(producer.runProhibitionEnforcement(PROHIBITION, null, {})), 'key order is the wire order');
  });

  test('the request file form and the --json form deliver the same verdict', () => {
    const fromFile = evaluate((dir) => [path.join(dir, 'req.json')], (dir) => put(dir, 'req.json', JSON.stringify(REQUEST)));
    assert.deepStrictEqual(fromFile.payload, inline(REQUEST).payload);
    assert.equal(fromFile.outcome, 'advisory');
  });

  test('a judgment-tier prohibition is never a silent green', () => {
    const prohibition = { verification: 'judgment', text: 'prefer clarity' };
    const result = inline({ prohibition, check: { kind: 'unknown-kind', target: 't' } });
    assert.equal(result.payload.status, 'unverified');
    assert.equal(result.payload.tier, 'judgment');
    assert.deepStrictEqual(result.payload, producer.runProhibitionEnforcement(prohibition, { kind: 'unknown-kind', target: 't' }, {}));
  });

  test('a string mode is carried into the payload; an empty or non-string mode is not', () => {
    assert.equal(inline({ ...REQUEST, mode: 'autonomous' }).payload.mode, 'autonomous');
    assert.equal('mode' in inline({ ...REQUEST, mode: '' }).payload, false);
    assert.equal('mode' in inline({ ...REQUEST, mode: 7 }).payload, false);
    assert.equal('mode' in inline(REQUEST).payload, false);
  });

  test('a check that is not an object is read as no check', () => {
    for (const check of ['node-test', 7, null, true]) {
      const result = inline({ prohibition: PROHIBITION, check });
      assert.equal(result.payload.located, false, JSON.stringify(check));
    }
  });

  test('an under-specified descriptor is not locatable (no check is run)', () => {
    for (const check of [{ kind: 'node-test' }, { kind: 'node-test', target: '   ' }, { kind: 'lint-rule', target: 'src' }, { kind: 'lint-rule', target: 'src', rule: ' ' }, { kind: 'other', target: 't' }]) {
      const result = inline({ prohibition: PROHIBITION, check });
      assert.equal(result.outcome, 'advisory', JSON.stringify(check));
      assert.equal(result.payload.located, false, JSON.stringify(check));
      assert.deepStrictEqual(result.payload.evidence, [], JSON.stringify(check));
    }
  });

  test('a missing prohibition is read as an empty one (fail-closed, flagged)', () => {
    const result = inline({ check: null });
    assert.equal(result.outcome, 'advisory');
    assert.equal(result.payload.flagged, true);
    assert.equal(result.payload.tier, null);
  });

  test('--json takes precedence over a path argument', () => {
    const result = evaluate(['ignored.json', '--json', JSON.stringify({ prohibition: { verification: 'judgment' }, check: null })]);
    assert.equal(result.payload.tier, 'judgment');
  });

  // The `unreadable` arm (a throw) is unreachable from request input: the producer catches its own
  // prover / runner throws and every request field is JSON data, so no document can make it throw. That
  // is why the gate's positive control (tests/gate-positive-control.test.cjs) reaches that arm by
  // patching the disposition function instead. The property pins the other half: no request is ever
  // `unreadable`, and none is anything but a usage failure or an advisory, non-blocking verdict.
  test('property: any inline argument is a usage failure or an advisory non-blocking verdict (a request can never reach the throw arm)', () => {
    fc.assert(fc.property(fc.oneof(fc.string(), fc.json()), (arg) => {
      const result = gate.evaluateProhibitionEnforcementGate({ projectDir: process.cwd(), args: ['--json', arg] });
      if (isGateUsageFailure(result)) return JSON.stringify(result) === JSON.stringify(USAGE);
      return result.block === false && result.outcome === 'advisory';
    }), { seed: 5219, numRuns: 100 });
  });
});

describe('evaluateProhibitionEnforcementGate: the no-throw contract', () => {
  test('a throw in the producer is a non-blocking unreadable verdict that fails closed (never green, never a crash)', () => {
    const real = probeCore.dispositionForProhibition;
    let result;
    try {
      probeCore.dispositionForProhibition = function patched() { throw new Error('simulated disposition failure'); };
      result = inline(REQUEST);
    } finally {
      probeCore.dispositionForProhibition = real;
    }
    assert.equal(isGateUsageFailure(result), false);
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.block, false);
    assert.deepStrictEqual(result.payload, {
      status: 'unverified',
      flagged: true,
      tier: null,
      reason: 'exception: simulated disposition failure',
      located: false,
      kind: null,
      evidence: [],
    });
  });

  test('the fail-closed payload keeps the request mode', () => {
    const real = probeCore.dispositionForProhibition;
    let result;
    try {
      probeCore.dispositionForProhibition = function patched() { throw new Error('boom'); };
      result = inline({ ...REQUEST, mode: 'autonomous' });
    } finally {
      probeCore.dispositionForProhibition = real;
    }
    assert.equal(result.payload.mode, 'autonomous');
    assert.equal(result.outcome, 'unreadable');
  });

  test('a non-Error throw is reported by its string form', () => {
    const real = probeCore.dispositionForProhibition;
    let result;
    try {
      probeCore.dispositionForProhibition = function patched() { throw 'plain string'; };
      result = inline(REQUEST);
    } finally {
      probeCore.dispositionForProhibition = real;
    }
    assert.equal(result.payload.reason, 'exception: plain string');
  });

  test('every verdict the gate returns is frozen', () => {
    assert.ok(Object.isFrozen(inline(REQUEST).payload));
  });
});
