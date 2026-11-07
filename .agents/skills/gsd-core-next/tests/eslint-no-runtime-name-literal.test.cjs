'use strict';

/**
 * Tests for `local/no-runtime-name-literal` (#5169, ADR-5057 Phase 10).
 *
 * The runtime descriptor owns every runtime-specific fact. This rule is the
 * ratchet: install and hook code may not compare a runtime identifier to a
 * registered runtime-id literal. This file locks four things:
 *
 *   1. POSITIVE CONTROL — the rule goes red on each shape it exists to catch
 *      (comparison, reversed comparison, `switch`/`case`, member access) in
 *      every owned surface (src install files, installer migrations, hooks,
 *      bin/install.js). A guard with no proven red is not a guard.
 *   2. NEGATIVE SPACE — it stays green on look-alikes: non-runtime literals,
 *      non-runtime identifiers, object keys, the owner modules, non-install
 *      source, and tests.
 *   3. DERIVATION — the id set comes from capabilities/<id>/capability.json, so
 *      every registered runtime is covered with no edit to the rule.
 *   4. CENSUS ZERO — the real owned tree has zero findings (run through the
 *      same rule, so a new literal fails here and in `npm run lint`).
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { Linter, ESLint } = require('eslint');

const rule = require('../eslint-rules/no-runtime-name-literal.cjs');
const registry = require('../gsd-core/bin/lib/capability-registry.cjs');
const { LEGACY_NON_REGISTRY_RUNTIME_IDS } = require('../gsd-core/bin/lib/runtime-name-policy.cjs');

const ROOT = path.join(__dirname, '..');

function lint(code, filename, options) {
  const linter = new Linter({ configType: 'flat' });
  const messages = linter.verify(
    code,
    [
      {
        // Explicit extensions: flat config does not match `.cts` through a bare `**/*`.
        files: ['**/*.cts', '**/*.js', '**/*.cjs'],
        plugins: { local: { rules: { 'no-runtime-name-literal': rule } } },
        rules: { 'local/no-runtime-name-literal': options ? ['error', options] : 'error' },
        languageOptions: { ecmaVersion: 2022, sourceType: 'commonjs' },
      },
    ],
    { filename },
  );
  return messages.filter((m) => m.ruleId === 'local/no-runtime-name-literal');
}

// Synthetic ids keep these unit cases independent of the real registry.
const OPTS = { runtimeIds: ['claude', 'hermes', 'opencode', 'codex', 'pi'] };
const INSTALL_FILE = 'src/install-engine.cts';

describe('no-runtime-name-literal — positive controls (goes red)', () => {
  const cases = [
    ['strict equality', "function f(runtime) { return runtime === 'hermes'; }"],
    ['strict inequality', "function f(runtime) { return runtime !== 'claude'; }"],
    ['loose equality', "function f(runtime) { return runtime == 'codex'; }"],
    ['loose inequality', "function f(runtime) { return runtime != 'codex'; }"],
    ['reversed operands', "function f(runtime) { return 'hermes' === runtime; }"],
    ['layout.runtime member', "function f(layout) { return layout.runtime === 'opencode'; }"],
    ['ctx.runtime member', "function f(ctx) { return ctx.runtime !== 'pi'; }"],
    ['canonical alias', "function f(canonical) { return canonical === 'claude'; }"],
    ['runtimeId identifier', "function f(runtimeId) { return runtimeId === 'codex'; }"],
    ['switch case', "function f(runtime) { switch (runtime) { case 'codex': return 1; default: return 0; } }"],
    ['optional chain', "function f(ctx) { return ctx?.runtime === 'hermes'; }"],
  ];
  for (const [name, code] of cases) {
    test(`flags ${name}`, () => {
      assert.equal(lint(code, INSTALL_FILE, OPTS).length, 1);
    });
  }

  test('flags every owned surface', () => {
    const code = "function f(runtime) { return runtime === 'hermes'; }";
    for (const filename of [
      'src/install-engine.cts',
      'src/install-scope.cts',
      'src/runtime-artifact-conversion.cts',
      'src/runtime-artifact-install-plan.cts',
      'src/surface.cts',
      'src/shell-command-projection.cts',
      'src/agent-install-check.cts',
      'hooks/gsd-read-guard.js',
      'bin/install.js',
    ]) {
      assert.equal(lint(code, filename, OPTS).length, 1, `${filename} must be governed`);
    }
  });

  test('reports the literal and names the descriptor remedy', () => {
    const [m] = lint("function f(runtime) { return runtime === 'hermes'; }", INSTALL_FILE, OPTS);
    assert.equal(m.messageId, 'runtimeNameLiteral');
    assert.ok(m.message.includes('hermes'));
  });

  test('flags each case of a multi-case switch (one finding per literal)', () => {
    const code = "function f(runtime) { switch (runtime) { case 'codex': return 1; case 'pi': return 2; case 'other': return 3; } }";
    assert.equal(lint(code, INSTALL_FILE, OPTS).length, 2);
  });
});

describe('no-runtime-name-literal — negative space (stays green)', () => {
  const valid = [
    ['non-runtime literal', "function f(transport) { return transport === 'openai-http'; }"],
    ['typeof check', "function f(runtime) { return typeof runtime === 'string'; }"],
    ['unregistered literal on a runtime identifier', "function f(a) { return a.runtime === 'sandboxed-web'; }"],
    ['registered literal on a non-runtime identifier', "function f(mode) { return mode === 'claude'; }"],
    ['object key (data, not a comparison)', "const t = { claude: 1, hermes: 2 }; function f(runtime) { return t[runtime]; }"],
    ['computed lookup', "function f(runtime, table) { return table['claude'] && table[runtime]; }"],
    ['comparison against another identifier', 'function f(runtime, other) { return runtime === other; }'],
    ['descriptor field read', 'function f(runtime, hb) { return hb(runtime).skipCompactAgents === true; }'],
    ['empty-string absence check', "function f(runtime) { return runtime === ''; }"],
    ['array membership (not a comparison)', "function f(runtime) { return ['claude', 'codex'].includes(runtime); }"],
  ];
  for (const [name, code] of valid) {
    test(`allows ${name}`, () => {
      assert.equal(lint(code, INSTALL_FILE, OPTS).length, 0);
    });
  }

  test('does not govern the owner modules', () => {
    const code = "function f(runtime) { return runtime === 'hermes'; }";
    assert.equal(lint(code, 'src/runtime-name-policy.cts', OPTS).length, 0);
    assert.equal(lint(code, 'src/runtime-homes.cts', OPTS).length, 0);
  });

  test('does not govern installer migrations: a shipped migration body is immutable (#670) and names its runtime by design', () => {
    const code = "function f(ctx) { return ctx.runtime !== 'pi'; }";
    assert.equal(lint(code, 'src/installer-migrations/009-pi-retire-reserved-hooks-dir.cts', OPTS).length, 0);
    assert.equal(lint(code, 'src/installer-migrations/000-first-time-baseline.cts', OPTS).length, 0);
  });

  test('does not govern non-install source or tests', () => {
    const code = "function f(runtime) { return runtime === 'hermes'; }";
    assert.equal(lint(code, 'src/commands.cts', OPTS).length, 0);
    assert.equal(lint(code, 'src/init.cts', OPTS).length, 0);
    assert.equal(lint(code, 'tests/foo.test.cjs', OPTS).length, 0);
    assert.equal(lint(code, 'scripts/gen-thing.cjs', OPTS).length, 0);
  });

  test('Windows-style paths are classified the same as POSIX paths', () => {
    const code = "function f(runtime) { return runtime === 'hermes'; }";
    assert.equal(lint(code, 'C:\\repo\\src\\install-engine.cts', OPTS).length, 1);
    assert.equal(lint(code, 'C:\\repo\\src\\runtime-name-policy.cts', OPTS).length, 0);
  });
});

describe('no-runtime-name-literal — id set is derived from the descriptors', () => {
  test('every registered runtime id is covered with no option override', () => {
    const filename = path.join(ROOT, 'src', 'install-engine.cts');
    const ids = Object.keys(registry.runtimes);
    assert.ok(ids.length > 0);
    for (const id of ids) {
      const code = `function f(runtime) { return runtime === '${id}'; }`;
      assert.equal(lint(code, filename).length, 1, `registered runtime '${id}' must be flagged`);
    }
  });

  test('every legacy non-registry id is covered', () => {
    const filename = path.join(ROOT, 'src', 'install-engine.cts');
    for (const id of LEGACY_NON_REGISTRY_RUNTIME_IDS) {
      assert.equal(lint(`function f(runtime) { return runtime === '${id}'; }`, filename).length, 1);
    }
  });

  test('parity: the rule\'s legacy id list equals the owner\'s LEGACY_NON_REGISTRY_RUNTIME_IDS', () => {
    assert.deepEqual([...rule.LEGACY_NON_REGISTRY_RUNTIME_IDS].sort(), [...LEGACY_NON_REGISTRY_RUNTIME_IDS].sort());
  });

  test('a made-up id is not flagged (membership, not a prefix or shape test)', () => {
    const filename = path.join(ROOT, 'src', 'install-engine.cts');
    assert.equal(lint("function f(runtime) { return runtime === 'not-a-runtime-xyz'; }", filename).length, 0);
  });
});

describe('no-runtime-name-literal — config wiring', () => {
  for (const rel of ['src/install-engine.cts', 'src/surface.cts', 'bin/install.js']) {
    test(`eslint.config.mjs applies the rule at error to ${rel}`, async () => {
      const eslint = new ESLint({ cwd: ROOT });
      const config = await eslint.calculateConfigForFile(path.join(ROOT, rel));
      const setting = config && config.rules ? config.rules['local/no-runtime-name-literal'] : undefined;
      assert.ok(setting !== undefined, `the rule must be configured for ${rel}`);
      const severity = Array.isArray(setting) ? setting[0] : setting;
      assert.ok(severity === 2 || severity === 'error', `expected severity error, got ${JSON.stringify(setting)}`);
    });
  }
});

describe('no-runtime-name-literal — census zero over the real owned tree', () => {
  function collect(dir, predicate, out) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) collect(full, predicate, out);
      else if (predicate(full)) out.push(full);
    }
    return out;
  }

  test('no runtime-name literal remains in install or hook code', async () => {
    const { parser } = require('typescript-eslint');
    const files = [
      ...collect(path.join(ROOT, 'src'), (f) => f.endsWith('.cts'), []),
      ...collect(path.join(ROOT, 'hooks'), (f) => f.endsWith('.js'), []),
      path.join(ROOT, 'bin', 'install.js'),
    ];
    const eslint = new ESLint({
      cwd: ROOT,
      overrideConfigFile: true,
      overrideConfig: [
        {
          files: ['**/*.cts'],
          languageOptions: { parser },
          plugins: { local: { rules: { 'no-runtime-name-literal': rule } } },
          rules: { 'local/no-runtime-name-literal': 'error' },
        },
        {
          files: ['**/*.js'],
          languageOptions: { sourceType: 'commonjs', ecmaVersion: 2022 },
          plugins: { local: { rules: { 'no-runtime-name-literal': rule } } },
          rules: { 'local/no-runtime-name-literal': 'error' },
        },
      ],
    });
    const results = await eslint.lintFiles(files);
    const findings = results.flatMap((r) =>
      r.messages
        .filter((m) => m.ruleId === 'local/no-runtime-name-literal')
        .map((m) => `${path.relative(ROOT, r.filePath)}:${m.line} ${m.message.slice(0, 60)}`),
    );
    assert.deepEqual(findings, []);
    assert.ok(files.length > 50, 'the census must actually have scanned the install surface');
  });
});
