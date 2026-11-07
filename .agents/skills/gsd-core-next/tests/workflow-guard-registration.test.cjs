// allow-test-rule: structural-regression-guard
// Reads hook .js or bin/install.js source to assert structural invariants
// (search array order, function wiring, path constants) that cannot be
// verified by observing runtime outputs alone. Per CONTRIBUTING.md exception matrix.

/**
 * Regression guard for #1767: gsd-workflow-guard.js must be registered in settings.json
 *
 * The hook file is built, copied, and installed — but was never registered as a
 * PreToolUse hook entry in install.js. This test ensures the registration block
 * exists with the correct structure.
 *
 * Also tests the broader anti-pattern: every hook in gsdHooks that is a JS
 * PreToolUse/PostToolUse hook should have a corresponding registration block.
 * Hooks owned by the runtime-hooks-surface module (Cursor) are validated
 * behaviorally rather than by source-scan of install.js.
 */
const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const { createTempDir, cleanup, captureConsole } = require('./helpers.cjs');

const INSTALL_JS = path.join(__dirname, '..', 'bin', 'install.js');
// ADR-857 phase 5f-1b: settings-json hook registration moved from install.js into
// applySettingsJsonHooks in src/runtime-hooks-surface.cts. Source-scan checks must
// include both files so structural invariants are verified against the correct source.
const HOOKS_SURFACE_SRC = path.join(__dirname, '..', 'src', 'runtime-hooks-surface.cts');

// Hooks whose registration is NOT a literal buildHookCommand(..., '<name>', ...)
// call in install.js/runtime-hooks-surface.cts, so the source-scan below cannot
// find them there. These are excluded from that scan and validated/documented
// elsewhere.
const MODULE_OWNED_HOOKS = new Set([
  // Cursor lifecycle hooks — registered by writeCursorHooksJson via the
  // descriptor-driven CURSOR_EVENT_SCRIPT_MAP indirection
  // (src/host-integration-adapters/imperative-hook-bus.cts), never a literal
  // buildHookCommand(..., '<hook-name>', ...) call this source-scan can match.
  // Validated behaviorally in the describe block below.
  'gsd-cursor-session-start.js',
  'gsd-cursor-post-tool.js',
  'gsd-cursor-pre-tool.js',
  'gsd-cursor-stop.js',
  'gsd-cursor-subagent-start.js',
  'gsd-cursor-subagent-stop.js',
  // Windsurf/Cascade blocking hooks — registered by writeWindsurfHooksJson via the
  // WINDSURF_EVENT_SCRIPT_MAP indirection (src/runtime-hooks-surface.cts), never a
  // literal buildHookCommand(..., '<hook-name>', ...) call this source-scan matches.
  // Validated behaviorally by tests/windsurf-hooks-bridge.test.cjs.
  'gsd-windsurf-pre-write.js',
  'gsd-windsurf-pre-command.js',
  // gsd-check-update-worker.js is an implementation detail of gsd-check-update.js
  // (spawned internally via child_process.spawn), never itself registered as a
  // hook entry point.
  'gsd-check-update-worker.js',
  // gsd-ensure-canonical-path.js (#997) is registered ONLY in the
  // marketplace-plugin manifest (hooks/hooks.json's SessionStart entry), not
  // via install.js's classic-installer settings.json path — the plugin
  // install mode never runs bin/install.js at all.
  'gsd-ensure-canonical-path.js',
]);

// ADR-857 phase 5f-1b: settings-json hook registration moved to runtime-hooks-surface.cts.
// Concatenate both sources so structural assertions find patterns in either file.
function readInstallSources() {
  const installSrc = fs.readFileSync(INSTALL_JS, 'utf-8');
  let hooksSurfaceSrc = '';
  try { hooksSurfaceSrc = fs.readFileSync(HOOKS_SURFACE_SRC, 'utf-8'); } catch { /* ok */ }
  return installSrc + '\n' + hooksSurfaceSrc;
}

describe('workflow-guard hook registration (#1767)', () => {
  // Behavioral (#5207): hook registration is a table consumed by one loop in
  // applySettingsJsonHooks; drive the real function against a temp target.
  const WORKFLOW_GUARD = 'gsd-workflow-guard.js';
  const WORKFLOW_GUARD_MATCHER = 'Bash|Edit|Write|MultiEdit';
  const { applySettingsJsonHooks } = require('../gsd-core/bin/lib/runtime-hooks-surface.cjs');

  function registerWorkflowGuard(t, seed) {
    const targetDir = createTempDir('workflow-guard-reg-');
    t.after(() => cleanup(targetDir));
    fs.mkdirSync(path.join(targetDir, 'hooks'), { recursive: true });
    fs.writeFileSync(path.join(targetDir, 'hooks', WORKFLOW_GUARD), '// stub\n');
    const settings = JSON.parse(JSON.stringify(seed || {}));
    const localCmd = (f) => `node ${path.join(targetDir, 'hooks', f)}`;
    const apply = () => captureConsole(() => applySettingsJsonHooks(settings, {
      runtime: 'claude',
      isGlobal: false,
      targetDir,
      postToolEvent: 'PostToolUse',
      hookEvents: 'claude',
      extendedHookEvents: [],
      hooksSurface: 'settings-json',
      updateCheckCommand: null,
      contextMonitorCommand: null,
      promptGuardCommand: null,
      readGuardCommand: null,
      readInjectionScannerCommand: null,
      configReloadCommand: null,
      hookOpts: { portableHooks: false, runtime: 'claude' },
      localCmd,
      localShellCmd: localCmd,
    }));
    return { settings, apply };
  }

  const workflowGuardEntries = (settings) =>
    (settings.hooks.PreToolUse || []).filter(e => e.hooks.some(h => h.command.includes('gsd-workflow-guard')));

  test('registers gsd-workflow-guard.js with a command constructed for its path', (t) => {
    const { settings, apply } = registerWorkflowGuard(t);
    apply();
    const entries = workflowGuardEntries(settings);
    assert.equal(entries.length, 1);
    assert.ok(entries[0].hooks[0].command.endsWith(path.join('hooks', WORKFLOW_GUARD)));
  });

  test('registers it once: a second install pass does not duplicate the entry', (t) => {
    const { settings, apply } = registerWorkflowGuard(t);
    apply();
    apply();
    assert.equal(workflowGuardEntries(settings).length, 1);
  });

  test('registers the Bash-aware matcher and upgrades an old edit-only entry on reinstall', (t) => {
    const fresh = registerWorkflowGuard(t);
    fresh.apply();
    assert.equal(workflowGuardEntries(fresh.settings)[0].matcher, WORKFLOW_GUARD_MATCHER,
      'workflow guard must be registered for Bash so worktree-agent git safety checks can run');

    const old = registerWorkflowGuard(t, {
      hooks: { PreToolUse: [{ matcher: 'Write|Edit', hooks: [{ type: 'command', command: `node /x/hooks/${WORKFLOW_GUARD}` }] }] },
    });
    old.apply();
    const entries = workflowGuardEntries(old.settings);
    assert.equal(entries.length, 1);
    assert.equal(entries[0].matcher, WORKFLOW_GUARD_MATCHER,
      'installer must upgrade existing workflow guard hook entries to the Bash-aware matcher');
  });
});

describe('hook registration completeness anti-pattern guard', () => {
  test('every JS hook in gsdHooks (except module-owned) has a command construction in install.js', () => {
    const content = readInstallSources();
    // Use the typed export instead of source-grep regex (branch #455: retire source-grep)
    const { GSD_UNINSTALL_HOOKS } = require('../bin/install.js');
    assert.ok(Array.isArray(GSD_UNINSTALL_HOOKS), 'GSD_UNINSTALL_HOOKS must be exported from install.js');

    // Cursor hooks (gsd-cursor-*.js) are registered by writeCursorHooksJson in
    // runtime-hooks-surface module, not by direct buildHookCommand calls in install.js.
    // They are validated behaviorally in the describe block below.
    // Hooks registered by a row of the settings-json tables (#5207) are
    // validated behaviorally by the registration tests; only a hook outside the
    // tables still needs a literal command construction in the sources.
    const surface = require('../gsd-core/bin/lib/runtime-hooks-surface.cjs');
    const tableOwned = new Set([
      ...surface.SETTINGS_JSON_HOOK_ROWS.map(r => r.file),
      ...surface.SETTINGS_JSON_EXTENDED_ROWS.map(r => r.file),
    ]);
    const jsHooks = GSD_UNINSTALL_HOOKS.filter(h => h.endsWith('.js') && !MODULE_OWNED_HOOKS.has(h) && !tableOwned.has(h));

    const missing = [];
    for (const hook of jsHooks) {
      // Each JS hook should have a buildHookCommand or 'node ' command construction
      // that references the hook filename (not just the gsdHooks array or uninstall filter)
      const lines = content.split('\n').filter(line =>
        line.includes(hook) &&
        (line.includes('buildHookCommand') || line.includes("'node '"))
      );
      if (lines.length === 0) {
        missing.push(hook);
      }
    }

    assert.strictEqual(
      missing.length, 0,
      [
        'Every JS hook in gsdHooks (excluding module-owned cursor hooks) must have a command',
        'construction in install.js. Missing registration for:',
        ...missing.map(h => `  - ${h}`),
      ].join('\n')
    );
  });
});

describe('atomic write temp-tracking — shared Set parity guard', () => {
  // FIX 1 verification: atomic writes from the runtime-hooks-surface module
  // must register their temp paths into the SAME __atomicWrittenTmps Set that
  // install.js's _cleanTmpFiles() uses. If the module had its own separate Set,
  // temp files from Cursor/Codex writes would never be cleaned up.
  test('writeCursorHooksJson atomic write registers temp path in shared __atomicWrittenTmps', (t) => {
    const targetDir = createTempDir('atomic-track-target-');
    const srcDir = createTempDir('atomic-track-src-');
    t.after(() => {
      cleanup(targetDir);
      cleanup(srcDir);
    });

    const srcHooksDir = path.join(srcDir, 'hooks');
    fs.mkdirSync(srcHooksDir, { recursive: true });
    fs.writeFileSync(path.join(srcHooksDir, 'gsd-cursor-session-start.js'), '// stub\n');
    fs.writeFileSync(path.join(srcHooksDir, 'gsd-cursor-post-tool.js'), '// stub\n');

    // Capture the Set BEFORE the call so we can diff it after.
    const hooksSurface = require('../gsd-core/bin/lib/runtime-hooks-surface.cjs');
    const tmpsSet = hooksSurface.__atomicWrittenTmps;
    assert.ok(tmpsSet instanceof Set, '__atomicWrittenTmps must be a Set');
    const sizeBefore = tmpsSet.size;

    const { writeCursorHooksJson } = require('../gsd-core/bin/lib/runtime-hooks-surface.cjs');
    writeCursorHooksJson(targetDir, srcDir, {});

    // After the write, the Set must have grown: the hooks.json temp path
    // was recorded so _cleanTmpFiles can clean it if it lingers.
    assert.ok(
      tmpsSet.size > sizeBefore,
      `__atomicWrittenTmps must grow after writeCursorHooksJson (was ${sizeBefore}, now ${tmpsSet.size})`
    );

    // Every new entry must be an absolute path matching the tmp pattern.
    for (const tmp of tmpsSet) {
      if (tmp.startsWith(targetDir)) {
        // This is the entry we added; it must look like hooks.json.tmp-<pid>-<n>
        assert.ok(
          /hooks\.json\.tmp-\d+-\d+$/.test(tmp),
          `Registered temp path must match hooks.json.tmp-<pid>-<n> pattern, got: ${tmp}`
        );
      }
    }
  });
});

describe('cursor hook registration — behavioral guard (module-owned hooks)', () => {
  // The cursor hook scripts are registered by writeCursorHooksJson inside
  // runtime-hooks-surface. This test calls the function directly and asserts
  // that both managed entries appear in hooks.json. Deleting either cursor
  // hook registration from the module will cause this test to fail.
  test('writeCursorHooksJson writes both gsd-cursor-session-start and gsd-cursor-post-tool to hooks.json', (t) => {
    // Create a temp target dir (simulated ~/.cursor) and a src dir with stub scripts.
    const targetDir = createTempDir('cursor-guard-target-');
    const srcDir = createTempDir('cursor-guard-src-');
    t.after(() => {
      cleanup(targetDir);
      cleanup(srcDir);
    });

    // Stub the hook scripts in srcDir/hooks/ so writeCursorHooksJson can copy them.
    const srcHooksDir = path.join(srcDir, 'hooks');
    fs.mkdirSync(srcHooksDir, { recursive: true });
    fs.writeFileSync(path.join(srcHooksDir, 'gsd-cursor-session-start.js'), '// stub\n');
    fs.writeFileSync(path.join(srcHooksDir, 'gsd-cursor-post-tool.js'), '// stub\n');

    const { GSD_CURSOR_HOOK_MARKER } = require('../bin/install.js');
    const { writeCursorHooksJson } = require('../gsd-core/bin/lib/runtime-hooks-surface.cjs');
    const result = writeCursorHooksJson(targetDir, srcDir, {});

    assert.ok(result, 'writeCursorHooksJson must return a result');

    const hooksJsonPath = path.join(targetDir, 'hooks.json');
    assert.ok(fs.existsSync(hooksJsonPath), 'hooks.json must be created in targetDir');

    const parsed = JSON.parse(fs.readFileSync(hooksJsonPath, 'utf8'));
    const hookTable = (parsed.hooks && typeof parsed.hooks === 'object') ? parsed.hooks : parsed;

    // Both cursor hook entries must be present.
    assert.ok(
      Array.isArray(hookTable.sessionStart) && hookTable.sessionStart.length > 0,
      'hooks.json must contain a sessionStart entry (gsd-cursor-session-start.js)'
    );
    assert.ok(
      Array.isArray(hookTable.postToolUse) && hookTable.postToolUse.length > 0,
      'hooks.json must contain a postToolUse entry (gsd-cursor-post-tool.js)'
    );

    // Both entries must be GSD-managed (not user-authored stubs).
    assert.equal(
      hookTable.sessionStart[0][GSD_CURSOR_HOOK_MARKER], true,
      'sessionStart entry must carry the GSD managed marker'
    );
    assert.equal(
      hookTable.postToolUse[0][GSD_CURSOR_HOOK_MARKER], true,
      'postToolUse entry must carry the GSD managed marker'
    );

    // Both commands must reference the correct script filenames.
    const sessionCmd = hookTable.sessionStart[0].command || '';
    const postToolCmd = hookTable.postToolUse[0].command || '';
    assert.ok(
      sessionCmd.includes('gsd-cursor-session-start.js'),
      `sessionStart command must reference gsd-cursor-session-start.js, got: ${sessionCmd}`
    );
    assert.ok(
      postToolCmd.includes('gsd-cursor-post-tool.js'),
      `postToolUse command must reference gsd-cursor-post-tool.js, got: ${postToolCmd}`
    );
  });
});
