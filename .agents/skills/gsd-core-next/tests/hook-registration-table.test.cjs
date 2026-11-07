'use strict';

/**
 * #5207 / ADR-5057 Phase 11 — hook registration is a table behind the
 * `settings-json` hooksSurface adapter.
 *
 * `golden.json` was generated from the pre-migration per-hook-branch
 * implementation; the table-driven loop must reproduce it byte-for-byte for
 * every runtime whose hooksSurface is `settings-json`.
 */

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');

const surface = require('../gsd-core/bin/lib/runtime-hooks-surface.cjs');
const scenarios = require('./fixtures/hook-registration/run-scenarios.cjs');
const golden = require('./fixtures/hook-registration/golden.json');

const { SETTINGS_JSON_HOOK_ROWS, SETTINGS_JSON_EXTENDED_ROWS, applySettingsJsonHookTables } = surface;

// A non-blocking budget distinct from every row's, for the timeout mutation.
const MUTATED_BUDGET_S = 7;

describe('settings-json hook registration parity (golden)', () => {
  const runtimes = scenarios.settingsJsonRuntimes();

  test('the golden covers every settings-json runtime the registry declares', () => {
    assert.deepEqual(runtimes.map((r) => r.id).sort(), Object.keys(golden).sort());
    assert.ok(runtimes.length >= 6);
  });

  for (const rt of runtimes) {
    for (const [name, spec] of Object.entries(scenarios.scenarioSpecs(rt))) {
      test(`${rt.id} ${name} registers identically to the pre-migration golden`, () => {
        const actual = scenarios.runOne(rt, spec);
        assert.deepEqual(actual, golden[rt.id][name]);
      });
    }
  }

  // Every field a row carries is observable in the golden; mutating any one
  // of them must turn the comparison red, or the pin is not binding it.
  const MUTATIONS = [
    ['matcher', (r) => { r.matcher = 'Write|Edit'; }],
    ['timeout', (r) => { r.timeout = r.timeout === 'blocking' ? MUTATED_BUDGET_S : 'blocking'; }],
    ['event', (r) => { r.event = r.event === 'pre' ? 'post' : 'pre'; }],
    ['configuredMessage', (r) => { r.configuredMessage += ' (mutated)'; }],
    ['skipLabel', (r) => { r.skipLabel += ' (mutated)'; }],
    ['command source', (r) => { r.command = 'opts' in r.command ? { build: 'js' } : { opts: 'promptGuardCommand' }; }],
  ];
  for (const [field, mutate] of MUTATIONS) {
    test(`the golden comparison goes red when a row's ${field} is mutated (positive control)`, () => {
      const rt = runtimes.find((r) => r.id === 'claude');
      const rows = structuredClone(SETTINGS_JSON_HOOK_ROWS);
      mutate(rows.find((r) => r.file === 'gsd-write-guard.js'));
      const scenario = field === 'skipLabel' ? 'local/none-present' : 'local/all-present';
      const actual = scenarios.runOne(
        rt,
        scenarios.scenarioSpecs(rt)[scenario],
        (settings, opts) => applySettingsJsonHookTables(settings, opts, { rows, extendedRows: SETTINGS_JSON_EXTENDED_ROWS }),
      );
      assert.notDeepEqual(actual, golden.claude[scenario]);
    });
  }

  test('the #3329 reconcile path executes in the golden (a dropped .sh command cannot pass unseen)', () => {
    const reconciled = Object.values(golden.claude).filter((s) => s.stdout.includes('Reconciled managed .sh hook commands'));
    assert.ok(reconciled.length > 0);
  });

  test('the scenarios install every hook file the tables register', () => {
    const files = new Set(scenarios.ALL_HOOKS);
    for (const row of [...SETTINGS_JSON_HOOK_ROWS, ...SETTINGS_JSON_EXTENDED_ROWS]) {
      assert.ok(files.has(row.file), `${row.file} has a row but no scenario installs it`);
    }
  });
});

describe('settings-json hook registration table', () => {
  test('rows are well-formed with a closed event vocabulary', () => {
    const events = new Set(['SessionStart', 'post', 'pre']);
    const seen = new Set();
    for (const row of SETTINGS_JSON_HOOK_ROWS) {
      assert.match(row.file, /^gsd-[a-z-]+\.(js|sh)$/);
      assert.ok(events.has(row.event), `${row.file}: unknown event ${row.event}`);
      assert.ok(!seen.has(row.file), `${row.file}: duplicate row`);
      seen.add(row.file);
      assert.equal(typeof row.configuredMessage, 'string');
      assert.equal(typeof row.skipLabel, 'string');
      assert.ok(row.command && (row.command.opts || row.command.build), `${row.file}: no command source`);
      if (row.matcher !== undefined) assert.equal(typeof row.matcher, 'string');
      if (row.timeout !== undefined) assert.ok(row.timeout === 'blocking' || Number.isInteger(row.timeout));
    }
  });

  test('every hook with the blocking budget is a BLOCKING_GUARD_NAMES member, and vice versa', () => {
    const blocking = SETTINGS_JSON_HOOK_ROWS.filter((r) => r.timeout === 'blocking').map((r) => r.file.replace(/\.(?:js|sh)$/, ''));
    assert.deepEqual([...blocking].sort(), [...surface.BLOCKING_GUARD_NAMES].sort());
  });

  test('the table and its rows are frozen', () => {
    assert.ok(Object.isFrozen(SETTINGS_JSON_HOOK_ROWS));
    assert.ok(Object.isFrozen(SETTINGS_JSON_EXTENDED_ROWS));
    for (const row of [...SETTINGS_JSON_HOOK_ROWS, ...SETTINGS_JSON_EXTENDED_ROWS]) {
      assert.ok(Object.isFrozen(row), `${row.file} row is mutable`);
      assert.ok(Object.isFrozen(row.command), `${row.file} command is mutable`);
    }
  });

  test('every registered hook comes from a table row', () => {
    const registered = new Set();
    const g = golden.claude['local/all-present'].settings.hooks;
    for (const entries of Object.values(g)) {
      for (const entry of entries) {
        for (const h of entry.hooks) registered.add(/(gsd-[a-z-]+\.(?:js|sh))$/.exec(h.command)[1]);
      }
    }
    const fromTables = new Set([
      ...SETTINGS_JSON_HOOK_ROWS.map((r) => r.file),
      ...SETTINGS_JSON_EXTENDED_ROWS.map((r) => r.file),
    ]);
    assert.deepEqual([...registered].sort(), [...fromTables].sort());
  });
});

describe('settings-json hook registration edge shapes', () => {
  const rt = scenarios.settingsJsonRuntimes().find((r) => r.id === 'claude');

  for (const surfaceName of ['none', 'kimi-hooks-toml']) {
    test(`hooksSurface '${surfaceName}' leaves settings untouched and silent`, () => {
      const out = scenarios.runOne(rt, { isGlobal: false, present: scenarios.ALL_HOOKS, hooksSurface: surfaceName });
      assert.deepEqual(out.settings, {});
      assert.equal(out.stdout, '');
      assert.equal(out.stderr, '');
    });
  }

  const EXTENDED_EVENTS = SETTINGS_JSON_EXTENDED_ROWS.map((r) => r.event);
  for (const bad of ['oops', 42, { not: 'array' }, true]) {
    for (const event of EXTENDED_EVENTS) {
      test(`a malformed ${event} key (${JSON.stringify(bad)}) is repaired, not thrown on`, () => {
        const spec = {
          isGlobal: false,
          present: scenarios.ALL_HOOKS,
          seed: { hooks: { [event]: bad } },
        };
        const out = scenarios.runOne({ ...rt, extendedHookEvents: [event] }, spec);
        assert.ok(Array.isArray(out.settings.hooks[event]));
        assert.equal(out.settings.hooks[event].length, 1);
      });
    }
  }
});
