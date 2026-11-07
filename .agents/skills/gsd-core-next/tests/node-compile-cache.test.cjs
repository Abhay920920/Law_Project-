'use strict';

/**
 * #5183: gsd-tools and the hooks that load gsd-core/bin/lib turn on Node's
 * on-disk compile cache (`module.enableCompileCache()`) when run as an entry.
 *
 * Every row spawns the real entry. A shared `--require` probe records, at
 * process exit, what Node itself reports (`module.getCompileCacheDir()`) and
 * whether `NODE_COMPILE_CACHE` leaked into the environment children inherit.
 * `--require` keeps `require.main === module` true for the entry.
 *
 * Child env: a copy of process.env with NODE_COMPILE_CACHE,
 * NODE_DISABLE_COMPILE_CACHE and NODE_V8_COVERAGE DELETED, not blanked:
 * NODE_DISABLE_COMPILE_CACHE is a presence flag ("" and "0" both disable).
 */

const { test, describe, before, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createTempDir, cleanup, homeSandboxEnv, TEST_ENV_BASE, TOOLS_PATH } = require('./helpers.cjs');
const { runNode, OUTCOME } = require('./helpers/process-seam.cjs');

const REPO_ROOT = path.join(__dirname, '..');
const HOOKS_DIR = path.join(REPO_ROOT, 'hooks');
const NODE_CACHE_ENV_KEYS = ['NODE_COMPILE_CACHE', 'NODE_DISABLE_COMPILE_CACHE', 'NODE_V8_COVERAGE'];
const PROBE_OUT_ENV = 'GSD_TEST_COMPILE_CACHE_PROBE_OUT';

// Under V8 coverage (c8, `npm run test:coverage`) Node re-injects
// NODE_V8_COVERAGE into every child even when the spawn env deletes it, so
// each entry correctly skips the cache there and "cache written" cannot hold.
// The coverage-guard row below still runs; the non-coverage lanes run this one.
const UNDER_COVERAGE = Boolean(process.env.NODE_V8_COVERAGE)
  && 'running under V8 coverage: Node forces NODE_V8_COVERAGE into every child, so the entry skips the cache by design';

// Records Node's own view of the compile cache when the entry exits.
const PROBE_SRC = `'use strict';
const fs = require('node:fs');
const m = require('node:module');
process.on('exit', () => {
  fs.writeFileSync(process.env.${PROBE_OUT_ENV}, JSON.stringify({
    dir: m.getCompileCacheDir() ?? null,
    envVar: process.env.NODE_COMPILE_CACHE ?? null,
  }));
});
`;

// gsd-check-update.js launches a detached worker that queries the registry;
// stub spawn so the test stays offline (pattern of check-update-config-dir).
const STUB_SPAWN_SRC = `'use strict';
const cp = require('node:child_process');
cp.spawn = () => ({ unref() {}, on() { return this; } });
`;

// gsd-check-update-worker.js destructures checkLatestVersion at load from the
// shared module cache, so overwriting the export here keeps it offline.
const STUB_LATEST_SRC = `'use strict';
require(${JSON.stringify(path.join(REPO_ROOT, 'gsd-core', 'bin', 'check-latest-version.cjs'))}).checkLatestVersion = () => ({ ok: false });
`;

const ENTRIES = [
  { name: 'gsd-tools.cjs', script: TOOLS_PATH, args: ['generate-slug', 'Compile Cache'] },
  { name: 'gsd-statusline.js', script: path.join(HOOKS_DIR, 'gsd-statusline.js'), input: '{}' },
  { name: 'gsd-agent-isolation-guard.js', script: path.join(HOOKS_DIR, 'gsd-agent-isolation-guard.js'), input: '{}' },
  { name: 'gsd-update-banner.js', script: path.join(HOOKS_DIR, 'gsd-update-banner.js'), input: '{}' },
  { name: 'gsd-cursor-subagent-start.js', script: path.join(HOOKS_DIR, 'gsd-cursor-subagent-start.js'), input: '{}' },
  { name: 'gsd-check-update.js', script: path.join(HOOKS_DIR, 'gsd-check-update.js'), input: '{}', stub: 'spawn' },
  { name: 'gsd-check-update-worker.js', script: path.join(HOOKS_DIR, 'gsd-check-update-worker.js'), stub: 'latest' },
];

let fixtures;

before(() => {
  fixtures = createTempDir('gsd-compile-cache-fixtures-');
  fs.writeFileSync(path.join(fixtures, 'probe.cjs'), PROBE_SRC);
  fs.writeFileSync(path.join(fixtures, 'stub-spawn.cjs'), STUB_SPAWN_SRC);
  fs.writeFileSync(path.join(fixtures, 'stub-latest.cjs'), STUB_LATEST_SRC);
});

after(() => cleanup(fixtures));

/**
 * Run one entry in a fresh sandbox. `tmp` is what the temp triad points at
 * (a fresh directory unless the caller passes one); `extraEnv` is applied
 * last, so it can re-add a Node cache variable.
 */
function runEntry(entry, { tmp, extraEnv = {} } = {}) {
  const box = createTempDir('gsd-compile-cache-');
  const home = path.join(box, 'home');
  const cwd = path.join(box, 'cwd');
  fs.mkdirSync(home);
  fs.mkdirSync(cwd);
  const tempDir = tmp ?? fs.mkdtempSync(path.join(box, 'tmp-'));
  const probeOut = path.join(box, 'probe.json');

  const env = { ...process.env, ...TEST_ENV_BASE, ...homeSandboxEnv(home), GSD_HOME: home };
  for (const key of NODE_CACHE_ENV_KEYS) delete env[key];
  // ADR-1703 require-full-tmpdir-triad: Windows' os.tmpdir() reads TEMP/TMP only.
  Object.assign(env, { TMPDIR: tempDir, TEMP: tempDir, TMP: tempDir });
  env[PROBE_OUT_ENV] = probeOut;
  if (entry.stub === 'latest') {
    // The env gsd-check-update.js hands its worker, pointed into the sandbox.
    Object.assign(env, {
      GSD_CACHE_FILE: path.join(box, 'update-check.json'),
      GSD_PROJECT_VERSION_FILE: path.join(box, 'project-VERSION'),
      GSD_GLOBAL_VERSION_FILE: path.join(box, 'global-VERSION'),
    });
  }
  Object.assign(env, extraEnv);

  const preloads = ['--require', path.join(fixtures, 'probe.cjs')];
  if (entry.stub === 'spawn') preloads.push('--require', path.join(fixtures, 'stub-spawn.cjs'));
  if (entry.stub === 'latest') preloads.push('--require', path.join(fixtures, 'stub-latest.cjs'));

  const result = runNode([...preloads, entry.script, ...(entry.args ?? [])], {
    cwd,
    env,
    input: entry.input,
  });
  assert.equal(result.outcome, OUTCOME.EXITED, `${entry.name}: ${result.outcome} ${result.stderr}`);
  assert.ok(fs.existsSync(probeOut), `${entry.name}: exit probe did not run; stderr=${result.stderr}`);
  const probe = JSON.parse(fs.readFileSync(probeOut, 'utf8'));
  return { result, probe, box, tempDir };
}

function hasRegularFile(dir) {
  if (!fs.existsSync(dir)) return false;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isFile()) return true;
    if (ent.isDirectory() && hasRegularFile(p)) return true;
  }
  return false;
}

describe('#5183 compile cache at the gsd-tools entry and the bin/lib-loading hooks', () => {
  for (const entry of ENTRIES) {
    describe(entry.name, () => {
      test('run as an entry: caches under <os temp>/node-compile-cache and does not export NODE_COMPILE_CACHE', { skip: UNDER_COVERAGE }, () => {
        const { result, probe, box, tempDir } = runEntry(entry);
        try {
          assert.equal(result.exitCode, 0, result.stderr);
          assert.ok(probe.dir, `${entry.name}: compile cache not enabled`);
          const expected = fs.realpathSync(path.join(tempDir, 'node-compile-cache'));
          assert.ok(fs.realpathSync(probe.dir).startsWith(expected), `${probe.dir} not under ${expected}`);
          assert.ok(hasRegularFile(expected), `${entry.name}: no cache file under ${expected}`);
          assert.equal(probe.envVar, null, 'NODE_COMPILE_CACHE must not be exported to children');
        } finally {
          cleanup(box);
        }
      });

      for (const value of ['1', '']) {
        test(`NODE_DISABLE_COMPILE_CACHE=${JSON.stringify(value)} turns it off`, () => {
          const { result, probe, box, tempDir } = runEntry(entry, { extraEnv: { NODE_DISABLE_COMPILE_CACHE: value } });
          try {
            assert.equal(result.exitCode, 0, result.stderr);
            assert.equal(probe.dir, null);
            assert.equal(fs.existsSync(path.join(tempDir, 'node-compile-cache')), false);
          } finally {
            cleanup(box);
          }
        });
      }

      test('skipped under V8 coverage (NODE_V8_COVERAGE set)', () => {
        const coverageDir = createTempDir('gsd-compile-cache-cov-');
        try {
          const { result, probe, box, tempDir } = runEntry(entry, { extraEnv: { NODE_V8_COVERAGE: coverageDir } });
          try {
            assert.equal(result.exitCode, 0, result.stderr);
            assert.equal(probe.dir, null);
            assert.equal(fs.existsSync(path.join(tempDir, 'node-compile-cache')), false);
          } finally {
            cleanup(box);
          }
        } finally {
          cleanup(coverageDir);
        }
      });

      test('a user-set NODE_COMPILE_CACHE directory is honored', () => {
        const userCache = createTempDir('gsd-compile-cache-user-');
        try {
          const { result, probe, box, tempDir } = runEntry(entry, { extraEnv: { NODE_COMPILE_CACHE: userCache } });
          try {
            assert.equal(result.exitCode, 0, result.stderr);
            assert.ok(probe.dir && fs.realpathSync(probe.dir).startsWith(fs.realpathSync(userCache)));
            assert.equal(fs.existsSync(path.join(tempDir, 'node-compile-cache')), false);
          } finally {
            cleanup(box);
          }
        } finally {
          cleanup(userCache);
        }
      });

      test('an unusable temp dir changes nothing: same exit, stdout and stderr as with the cache off', () => {
        // A regular FILE as the temp dir: Node cannot create its cache dir
        // under it on any OS and for any uid (chmod is bypassed as root).
        const holder = createTempDir('gsd-compile-cache-notdir-');
        const notDir = path.join(holder, 'not-a-dir');
        fs.writeFileSync(notDir, '');
        try {
          const off = runEntry(entry, { tmp: notDir, extraEnv: { NODE_DISABLE_COMPILE_CACHE: '1' } });
          const on = runEntry(entry, { tmp: notDir });
          try {
            assert.equal(on.result.exitCode, 0, on.result.stderr);
            assert.equal(on.result.exitCode, off.result.exitCode);
            assert.equal(on.result.stdout, off.result.stdout);
            const pidless = (text) => text.replace(/\(node:\d+\)/g, '(node:PID)');
            assert.equal(pidless(on.result.stderr), pidless(off.result.stderr));
          } finally {
            cleanup(off.box);
            cleanup(on.box);
          }
        } finally {
          cleanup(holder);
        }
      });
    });
  }

  test('require() of an entry (not run as main) leaves the cache off', () => {
    const box = createTempDir('gsd-compile-cache-require-');
    try {
      const env = { ...process.env, ...TEST_ENV_BASE, ...homeSandboxEnv(box), GSD_HOME: box };
      for (const key of NODE_CACHE_ENV_KEYS) delete env[key];
      Object.assign(env, { TMPDIR: box, TEMP: box, TMP: box });
      for (const entry of ENTRIES) {
        // Hooks without a main guard run (and may exit) on require; the exit
        // probe still records Node's view at the end.
        const probeOut = path.join(box, `${entry.name}.probe.json`);
        const preloads = ['--require', path.join(fixtures, 'probe.cjs')];
        if (entry.stub === 'spawn') preloads.push('--require', path.join(fixtures, 'stub-spawn.cjs'));
        if (entry.stub === 'latest') preloads.push('--require', path.join(fixtures, 'stub-latest.cjs'));
        const program = `try { require(${JSON.stringify(entry.script)}); } catch {}`;
        const result = runNode([...preloads, '-e', program], {
          cwd: box,
          env: { ...env, [PROBE_OUT_ENV]: probeOut, GSD_CACHE_FILE: path.join(box, 'update-check.json') },
          input: entry.input ?? '',
        });
        assert.equal(result.outcome, OUTCOME.EXITED, `${entry.name}: ${result.stderr}`);
        const probe = JSON.parse(fs.readFileSync(probeOut, 'utf8'));
        assert.equal(probe.dir, null, `${entry.name}: cache enabled by a plain require()`);
      }
    } finally {
      cleanup(box);
    }
  });
});
