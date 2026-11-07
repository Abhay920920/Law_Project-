'use strict';

/**
 * `check api-coverage.verify-pre`: the defence-in-depth arm "resolved phase dir escapes .planning/"
 * (#5139, epic #5056, ADR-5057 Phase 6 review finding).
 *
 * The arm is NOT reachable through the CLI, so it has no cutover golden. Verified by executing the
 * CLI, not assumed: `findPhaseInternal` only ever returns directories under `.planning/phases` or
 * `.planning/milestones`; a phase directory that is a SYMLINK to an outside directory is not
 * resolved at all (`phase_lookup_failed`), and a workstream (`GSD_WORKSTREAM`) does not make it
 * return a root-level phase. The recheck after resolution is the last line of defence against a
 * future resolver that does, so it is driven here through the module's one injected seam: its
 * binding of `findPhaseInternal` (the gate destructures it from `phase-locator.cjs` at load, so a
 * fresh load of the gate over a stubbed locator module injects it).
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createTempProject, cleanup } = require('./helpers.cjs');

const GATE_PATH = require.resolve('../gsd-core/bin/lib/gate-api-coverage-verify-pre.cjs');
const LOCATOR_PATH = require.resolve('../gsd-core/bin/lib/phase-locator.cjs');

/** A fresh copy of the gate whose `findPhaseInternal` is `stub`; every cache entry is restored. */
function loadGateWith(stub) {
  const locatorEntry = require.cache[LOCATOR_PATH] ?? (require(LOCATOR_PATH), require.cache[LOCATOR_PATH]);
  const realExports = locatorEntry.exports;
  const savedGate = require.cache[GATE_PATH];
  try {
    locatorEntry.exports = { ...realExports, findPhaseInternal: stub };
    delete require.cache[GATE_PATH];
    return require(GATE_PATH);
  } finally {
    locatorEntry.exports = realExports;
    if (savedGate) require.cache[GATE_PATH] = savedGate;
    else delete require.cache[GATE_PATH];
  }
}

function withProject(fn) {
  const dir = createTempProject('gate-api-escape-');
  try {
    fs.mkdirSync(path.join(dir, '.planning', 'phases', '01-x'), { recursive: true });
    fs.mkdirSync(path.join(dir, '.planning', 'milestones', 'v1.0-phases', '01-x'), { recursive: true });
    fs.mkdirSync(path.join(dir, 'src'), { recursive: true });
    return fn(dir);
  } finally {
    cleanup(dir);
  }
}

const ESCAPE_PAYLOAD = {
  block: true,
  passed: false,
  coverage_present: false,
  detected: false,
  message: 'api-coverage: resolved phase dir escapes .planning/ — refusing to evaluate',
};

describe('api-coverage.verify-pre refuses a resolved phase dir outside .planning/', () => {
  for (const directory of ['src', '../elsewhere', '.planning', '.planning/phases/../../src']) {
    test(`a resolver answering ${JSON.stringify(directory)} -> the escape arm, nothing evaluated`, () => {
      withProject((dir) => {
        const gate = loadGateWith(() => ({ directory, phase_number: '01' }));
        const result = gate.evaluateApiCoverageVerifyPre({ projectDir: dir, args: ['01-x'] });
        assert.equal(result.outcome, 'block');
        assert.equal(result.block, true);
        assert.deepStrictEqual(result.payload, ESCAPE_PAYLOAD);
        assert.equal(JSON.stringify(result.payload), JSON.stringify(ESCAPE_PAYLOAD), 'payload key order is part of the contract');
      });
    });
  }

  test('control: a resolved dir under .planning/phases is NOT refused by that arm', () => {
    withProject((dir) => {
      const gate = loadGateWith(() => ({ directory: '.planning/phases/01-x', phase_number: '01' }));
      const result = gate.evaluateApiCoverageVerifyPre({ projectDir: dir, args: ['01-x'] });
      assert.notEqual(result.payload.message, ESCAPE_PAYLOAD.message);
      assert.equal(result.payload.scope_unavailable, true, 'an empty phase reaches the scope-empty arm');
    });
  });

  test('control: a resolved dir under .planning/milestones is NOT refused by that arm', () => {
    withProject((dir) => {
      const gate = loadGateWith(() => ({ directory: '.planning/milestones/v1.0-phases/01-x', phase_number: '01' }));
      const result = gate.evaluateApiCoverageVerifyPre({ projectDir: dir, args: ['01-x'] });
      assert.notEqual(result.payload.message, ESCAPE_PAYLOAD.message);
    });
  });

  test('the stub is scoped: the gate everyone else requires still uses the real resolver', () => {
    withProject((dir) => {
      loadGateWith(() => ({ directory: 'src', phase_number: '01' }));
      const real = require(GATE_PATH);
      const result = real.evaluateApiCoverageVerifyPre({ projectDir: dir, args: ['99-missing'] });
      assert.equal(result.payload.phase_lookup_failed, true, 'the real resolver found no such phase');
    });
  });
});
