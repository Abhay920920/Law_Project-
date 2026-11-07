'use strict';

/**
 * B1, B2, B3 — the router/gate boundary is enforced by the repo's ESLint config
 * (#5139, epic #5056, ADR-5057 §4 bullet 1, design D6.1).
 *
 * Design D6.1: overrides in eslint.config.mjs (no custom rule)
 *   - scoped to `src/gate-*.cts`, `src/decision-coverage-support.cts` and `src/check-auto-mode.cts`:
 *     no io module may be imported (`./io.cjs` by name, and any `**\/io.cjs` / `**\/io` path), and
 *     no direct console / stdout / stderr write is performed (`no-console`, and
 *     `no-restricted-properties` on `process.stdout` / `process.stderr`) — a gate module returns a
 *     GateVerdict; only the router formats output;
 *   - scoped to `src/check-command-router.cts`: `fs`, `child_process`, `fs/promises` (bare and
 *     `node:` spellings) and the `./shell-command-projection.cjs` exec/git helpers are forbidden
 *     (the router only parses argv and formats).
 *
 * FAILING-FIRST (RED): eslint.config.mjs carried no such override on origin/next, so no setting
 * exists for those paths and the positive controls below fail.
 *
 * Mechanism: the config the repo lints with is read through the ESLint API
 * (`calculateConfigForFile`, the way tests/eslint-no-verification-status-literal.test.cjs does),
 * and ONLY its boundary rule entries (`no-restricted-imports` core or @typescript-eslint variant,
 * `no-console`, `no-restricted-properties`) are replayed in a Linter over a violating snippet. A
 * type-aware parse of a not-yet-existing `src/gate-x.cts` is impossible (the file is not in the TS
 * project), so the snippet is parsed without project information; the rule options are exactly the
 * repo's for that path.
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { ESLint, Linter } = require('eslint');

const ROOT = path.join(__dirname, '..');
const BOUNDARY_RE = /(^|\/)(no-restricted-imports|no-console|no-restricted-properties)$/;

const GATE_MODULES = [
  'gate-decision-coverage-plan',
  'gate-decision-coverage-verify',
  'gate-ui-plan',
  'gate-ui-safety',
  'gate-tdd-review-checkpoint',
  'gate-tdd-red-evidence',
  'gate-verify-command-paths',
  'gate-verify-failure-directions',
  'gate-gap-analysis-plan-post',
  'gate-predicate',
  'gate-api-coverage-verify-pre',
  'gate-schema-drift',
  'gate-codebase-drift',
  'gate-context-drift',
  'gate-prohibition-enforcement',
  'gate-verdict',
  'gate-phase-context',
  'gate-args',
  'gate-config',
  'gate-predicate-evaluator',
  'decision-coverage-support',
  'check-auto-mode',
];
const ROUTER = 'src/check-command-router.cts';

let eslintInstance;
function eslint() {
  if (!eslintInstance) eslintInstance = new ESLint({ cwd: ROOT });
  return eslintInstance;
}

/** The repo's boundary rule entries for a path, or null when the path is ignored. */
async function boundaryConfig(relPath) {
  const calc = await eslint().calculateConfigForFile(path.join(ROOT, relPath));
  if (!calc) return null;
  const entries = Object.entries(calc.rules || {}).filter(([id, setting]) => {
    if (!BOUNDARY_RE.test(id)) return false;
    const severity = Array.isArray(setting) ? setting[0] : setting;
    return severity === 2 || severity === 'error' || severity === 1 || severity === 'warn';
  });
  return { calc, entries };
}

/** Lint `code` as if it lived at `relPath`, running only the repo's boundary rules for that path. */
async function boundaryMessages(relPath, code) {
  const config = await boundaryConfig(relPath);
  assert.notEqual(config, null, `${relPath} must not be ignored by the ESLint config`);
  if (config.entries.length === 0) return [];
  const linter = new Linter({ cwd: ROOT, configType: 'flat' });
  const messages = linter.verify(
    code,
    [
      {
        files: ['**/*.cts'],
        languageOptions: {
          parser: config.calc.languageOptions.parser,
          ecmaVersion: 'latest',
          sourceType: 'module',
        },
        plugins: config.calc.plugins,
        rules: Object.fromEntries(config.entries),
      },
    ],
    { filename: relPath },
  );
  const fatal = messages.filter((m) => m.fatal);
  assert.deepStrictEqual(fatal, [], `snippet must parse: ${JSON.stringify(fatal)}`);
  return messages.filter((m) => m.ruleId && BOUNDARY_RE.test(m.ruleId));
}

const GATE_PATHS = ['src/gate-x.cts', 'src/decision-coverage-support.cts', 'src/check-auto-mode.cts'];

const IO_REQUIRE = "import ioMod = require('./io.cjs');\nexport = { ioMod };\n";
const IO_ESM = "import { output } from './io.cjs';\nexport = { output };\n";

describe('B1 gate modules may not import an io module', () => {
  for (const relPath of GATE_PATHS) {
    test(`B1: ${relPath} is reported for a require-style ./io.cjs import`, async () => {
      const messages = await boundaryMessages(relPath, IO_REQUIRE);
      assert.ok(messages.length >= 1, JSON.stringify(messages));
      assert.ok(messages.every((m) => m.severity === 2));
    });

    test(`B1: ${relPath} is reported for an ES-style ./io.cjs import`, async () => {
      const messages = await boundaryMessages(relPath, IO_ESM);
      assert.ok(messages.length >= 1, JSON.stringify(messages));
      assert.ok(messages.every((m) => m.severity === 2));
    });
  }

  // Other spellings of an io module: a parent / nested path, and the extensionless name.
  const IO_SPELLINGS = [
    ['../io.cjs', "import ioMod = require('../io.cjs');\nexport = { ioMod };\n"],
    ['./lib/io.cjs', "import ioMod = require('./lib/io.cjs');\nexport = { ioMod };\n"],
    ['./io (extensionless, require)', "import ioMod = require('./io');\nexport = { ioMod };\n"],
    ['../io (extensionless, ES)', "import { output } from '../io';\nexport = { output };\n"],
    ['../../gsd-core/bin/lib/io.cjs', "import { error } from '../../gsd-core/bin/lib/io.cjs';\nexport = { error };\n"],
  ];
  for (const [label, code] of IO_SPELLINGS) {
    test(`B1: an io import spelled ${label} is reported in a gate module`, async () => {
      for (const relPath of GATE_PATHS) {
        const messages = await boundaryMessages(relPath, code);
        assert.ok(messages.length >= 1, `${relPath}: ${JSON.stringify(messages)}`);
      }
    });
  }

  test('B1: the same ./io.cjs import in an unrelated src file is not reported', async () => {
    assert.deepStrictEqual(await boundaryMessages('src/other.cts', IO_REQUIRE), []);
    assert.deepStrictEqual(await boundaryMessages('src/other.cts', IO_ESM), []);
  });

  test('B1: an unrelated import in a gate module is not reported (the restriction is specific)', async () => {
    const code = "import pathMod = require('node:path');\nimport { audio } from './radio-audio.cjs';\nexport = { pathMod, audio };\n";
    assert.deepStrictEqual(await boundaryMessages('src/gate-x.cts', code), []);
  });
});

describe('B1b gate modules perform no direct console / stdout / stderr write', () => {
  const WRITES = [
    ['console.log', "export = { run() { console.log('x'); } };\n"],
    ['console.error', "export = { run() { console.error('x'); } };\n"],
    ['console.warn', "export = { run() { console.warn('x'); } };\n"],
    ['process.stdout.write', "export = { run() { process.stdout.write('x'); } };\n"],
    ['process.stderr.write', "export = { run() { process.stderr.write('x'); } };\n"],
    ["process['stderr'].write (computed)", "export = { run() { process['stderr'].write('x'); } };\n"],
  ];
  for (const [label, code] of WRITES) {
    test(`B1b: ${label} is reported in every gate path`, async () => {
      for (const relPath of GATE_PATHS) {
        const messages = await boundaryMessages(relPath, code);
        assert.equal(messages.length, 1, `${relPath}: ${JSON.stringify(messages)}`);
        assert.equal(messages[0].severity, 2);
      }
    });

    test(`B1b: ${label} in an unrelated src file is not reported`, async () => {
      assert.deepStrictEqual(await boundaryMessages('src/other.cts', code), []);
    });

    test(`B1b: ${label} in the router is not reported by THIS boundary (the router formats through io)`, async () => {
      assert.deepStrictEqual(await boundaryMessages(ROUTER, code), []);
    });
  }

  test('B1b: reading process.env / process.argv / process.cwd is not reported (the restriction is specific)', async () => {
    const code = 'export = { env: process.env, argv: process.argv, cwd: process.cwd() };\n';
    for (const relPath of GATE_PATHS) {
      assert.deepStrictEqual(await boundaryMessages(relPath, code), [], relPath);
    }
  });
});

describe('B2 the router may not import fs, child_process or the exec helpers', () => {
  const cases = [
    ['node:fs (require style)', "import fs = require('node:fs');\nexport = { fs };\n"],
    ['node:fs (ES style)', "import { readFileSync } from 'node:fs';\nexport = { readFileSync };\n"],
    ['bare fs (require style)', "import fs = require('fs');\nexport = { fs };\n"],
    ['bare fs (ES style)', "import { readFileSync } from 'fs';\nexport = { readFileSync };\n"],
    ['node:fs/promises (ES style)', "import { readFile } from 'node:fs/promises';\nexport = { readFile };\n"],
    ['bare fs/promises (require style)', "import fsp = require('fs/promises');\nexport = { fsp };\n"],
    ['node:child_process (require style)', "import cp = require('node:child_process');\nexport = { cp };\n"],
    ['node:child_process (ES style)', "import { execFileSync } from 'node:child_process';\nexport = { execFileSync };\n"],
    ['bare child_process (require style)', "import cp = require('child_process');\nexport = { cp };\n"],
    ['bare child_process (ES style)', "import { spawnSync } from 'child_process';\nexport = { spawnSync };\n"],
    ['shell-command-projection execTool', "import { execTool } from './shell-command-projection.cjs';\nexport = { execTool };\n"],
  ];
  for (const [label, code] of cases) {
    test(`B2: ${label} is reported in ${ROUTER}`, async () => {
      const messages = await boundaryMessages(ROUTER, code);
      assert.equal(messages.length, 1, JSON.stringify(messages));
      assert.equal(messages[0].severity, 2);
    });
  }

  test('B2: the same imports in an unrelated src file are not reported', async () => {
    for (const [, code] of cases) {
      assert.deepStrictEqual(await boundaryMessages('src/other.cts', code), []);
    }
  });

  test('B2: an unrelated import in the router is not reported (the restriction is specific)', async () => {
    const code = "import pathMod = require('node:path');\nexport = { pathMod };\n";
    assert.deepStrictEqual(await boundaryMessages(ROUTER, code), []);
  });
});

describe('B2b the router imports a verb implementation only from a gate module (ADR-5057 §4, #5219)', () => {
  // The four verbs that Phase 6 left in verify.cts / prohibition-enforcement.cts printed their own
  // payload. Wiring a verb's logic into the router from any non-gate module is the same defect again.
  const outside = [
    ['verify.cjs (require style)', "import verifyMod = require('./verify.cjs');\nexport = { verifyMod };\n"],
    ['verify.cjs (ES style)', "import { cmdVerifyArtifacts } from './verify.cjs';\nexport = { cmdVerifyArtifacts };\n"],
    ['prohibition-enforcement.cjs', "import { runProhibitionEnforcement } from './prohibition-enforcement.cjs';\nexport = { runProhibitionEnforcement };\n"],
    ['drift.cjs', "import drift = require('./drift.cjs');\nexport = { drift };\n"],
    ['a parent-relative module', "import x = require('../verify.cjs');\nexport = { x };\n"],
  ];
  for (const [label, code] of outside) {
    test(`B2b: ${label} is reported in ${ROUTER}`, async () => {
      const messages = await boundaryMessages(ROUTER, code);
      assert.equal(messages.length, 1, JSON.stringify(messages));
      assert.equal(messages[0].severity, 2);
    });
  }

  const allowed = [
    'gate-schema-drift', 'gate-codebase-drift', 'gate-context-drift', 'gate-prohibition-enforcement',
    'gate-verdict', 'gate-exit', 'gate-args', 'io', 'check-auto-mode', 'decision-coverage-support',
  ];
  for (const mod of allowed) {
    test(`B2b: ./${mod}.cjs is not reported in ${ROUTER}`, async () => {
      const code = `import m = require('./${mod}.cjs');\nexport = { m };\n`;
      assert.deepStrictEqual(await boundaryMessages(ROUTER, code), []);
    });
  }

  test('B2b: the same non-gate imports in an unrelated src file are not reported', async () => {
    for (const [, code] of outside) {
      assert.deepStrictEqual(await boundaryMessages('src/other.cts', code), []);
    }
  });
});

describe('B3the real files carry the boundary and honour it', () => {
  const realFiles = [...GATE_MODULES.map((m) => `src/${m}.cts`), ROUTER];

  test('B3: every gate module, the shared support modules and the router exist', () => {
    const missing = realFiles.filter((f) => !fs.existsSync(path.join(ROOT, f)));
    assert.deepStrictEqual(missing, []);
  });

  test('B3: every real file has its boundary settings (the boundary is not vacuous)', async () => {
    const unguarded = [];
    for (const f of realFiles) {
      const config = await boundaryConfig(f);
      const ids = new Set((config?.entries ?? []).map(([id]) => id.replace(/^.*\//, '')));
      const wanted = f === ROUTER
        ? ['no-restricted-imports']
        : ['no-restricted-imports', 'no-console', 'no-restricted-properties'];
      for (const rule of wanted) {
        if (!ids.has(rule)) unguarded.push(`${f}: ${rule}`);
      }
    }
    assert.deepStrictEqual(unguarded, []);
  });

  test('B3: linting the real files reports zero boundary violations', async () => {
    const existing = realFiles.filter((f) => fs.existsSync(path.join(ROOT, f)));
    const results = await eslint().lintFiles(existing.map((f) => path.join(ROOT, f)));
    const violations = results.flatMap((r) =>
      r.messages
        .filter((m) => m.ruleId && BOUNDARY_RE.test(m.ruleId))
        .map((m) => `${path.relative(ROOT, r.filePath)}:${m.line} ${m.ruleId} ${m.message}`),
    );
    assert.deepStrictEqual(violations, []);
  });
});
