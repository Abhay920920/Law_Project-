'use strict';

/**
 * In-process GateResult tests for `check verify-codebase-drift` (#5219, epic #5056, ADR-5057 §4 arm C).
 *
 * `evaluateCodebaseDriftGate({ projectDir, args })` returns a GateResult. The byte-for-byte stdout /
 * exit-status equivalence with the pre-move code is pinned by
 * tests/check-router-cutover-equivalence.test.cjs (E13); this file pins the returned value, every
 * skip-versus-unreadable arm, the threshold boundary at limit-1 / limit / limit+1, the document-size
 * boundary, the non-blocking "a throw is an unreadable verdict" contract, and that the gate never
 * writes to stdout / stderr.
 *
 * Git and I/O failures are injected by monkeypatching the method (`execGit`, `fs.readFileSync`) and
 * restoring it in `finally` (never a mode-bit trick: root bypasses those).
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fc = require('fast-check');
const fs = require('node:fs');
const { createTempProject, createTempGitProject, cleanup } = require('./helpers.cjs');
const { put, git } = require('./helpers/gate-positive-control.cjs');

const gate = require('../gsd-core/bin/lib/gate-codebase-drift.cjs');
const shell = require('../gsd-core/bin/lib/shell-command-projection.cjs');
const { isGateUsageFailure } = require('../gsd-core/bin/lib/gate-verdict.cjs');

const STRUCTURE = '# Structure\n\n- `src/` application sources\n';
const STRUCTURE_PATH = '.planning/codebase/STRUCTURE.md';
const MAX_DOCUMENT_BYTES = 1048576;

function commitAll(dir, message) {
  git(dir, 'add', '-A');
  git(dir, 'commit', '-m', message);
}

function stamped(sha) { return `---\nlast_mapped_commit: ${sha}\n---\n${STRUCTURE}`; }

/**
 * A git project mapped at its baseline commit. `stamp`: 'head' names the baseline commit, 'none' writes
 * no stamp, 'fake' names a commit git cannot resolve, 'tree' names a tree object (resolvable, not a commit).
 */
function mappedProject({ stamp = 'head', raw, extraDirs = [], config } = {}) {
  return (dir) => {
    if (config !== undefined) put(dir, '.planning/config.json', JSON.stringify(config));
    put(dir, STRUCTURE_PATH, STRUCTURE);
    put(dir, 'src/main.js', 'main\n');
    commitAll(dir, 'feat: baseline');
    const baseline = git(dir, 'rev-parse', 'HEAD').trim();
    const tree = git(dir, 'rev-parse', `${baseline}^{tree}`).trim();
    const named = {
      head: baseline,
      fake: '0123456789abcdef0123456789abcdef01234567',
      tree,
      abbrev7: baseline.slice(0, 7),
      upper: baseline.toUpperCase(),
    }[stamp];
    // `raw` is a stamp spelled literally (the boundary cases below).
    const value = raw !== undefined ? raw : named;
    if (value !== undefined) {
      put(dir, STRUCTURE_PATH, stamped(value));
      commitAll(dir, 'docs: stamp the map');
    }
    for (const extra of extraDirs) put(dir, `${extra}/index.js`, `${extra}\n`);
    if (extraDirs.length > 0) commitAll(dir, 'feat: add directories');
  };
}

/** Run the gate in a fresh project, asserting it wrote nothing to stdout / stderr. */
function evaluate(options, setup) {
  const dir = options.git === false ? createTempProject('gate-codebase-') : createTempGitProject('gate-codebase-');
  const writes = [];
  const outWrite = process.stdout.write;
  const errWrite = process.stderr.write;
  let restore;
  try {
    restore = setup ? setup(dir) : undefined;
    process.stdout.write = (chunk) => { writes.push(String(chunk)); return true; };
    process.stderr.write = (chunk) => { writes.push(String(chunk)); return true; };
    const result = gate.evaluateCodebaseDriftGate({ projectDir: dir, args: [] });
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

/** Make `execGit` misbehave for the subcommand `sub` (`'throw'` throws, otherwise returns `exitCode`). */
function patchExecGit(sub, behaviour) {
  const real = shell.execGit;
  shell.execGit = function patched(args, ...rest) {
    if (args[0] === sub) {
      if (behaviour === 'throw') throw new Error('simulated git failure');
      return { exitCode: behaviour, stdout: '', stderr: '' };
    }
    return real.call(shell, args, ...rest);
  };
  return function restore() { shell.execGit = real; };
}

describe('evaluateCodebaseDriftGate: skip versus unreadable', () => {
  test('no STRUCTURE.md is a skip (nothing mapped yet)', () => {
    const result = evaluate({}, undefined);
    assert.equal(isGateUsageFailure(result), false);
    assert.equal(result.outcome, 'skip');
    assert.equal(result.block, false);
    assert.deepStrictEqual(result.payload, { block: false, skipped: true, reason: 'no-structure-md', action_required: false, directive: 'none', elements: [] });
    assert.equal(JSON.stringify(result.payload), '{"block":false,"skipped":true,"reason":"no-structure-md","action_required":false,"directive":"none","elements":[]}', 'key order is the wire order');
  });

  test('a STRUCTURE.md that cannot be read is unreadable (never "no map")', () => {
    const realRead = fs.readFileSync;
    const result = evaluate({}, (dir) => {
      put(dir, STRUCTURE_PATH, STRUCTURE);
      fs.readFileSync = function patched(p, ...rest) {
        if (String(p).endsWith('STRUCTURE.md')) {
          const err = new Error('EACCES: simulated read failure');
          err.code = 'EACCES';
          throw err;
        }
        return realRead.call(fs, p, ...rest);
      };
      return () => { fs.readFileSync = realRead; };
    });
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.block, false);
    assert.equal(result.payload.reason, 'cannot-read-structure-md: EACCES: simulated read failure');
    assert.equal(result.payload.skipped, true);
  });

  test('a STRUCTURE.md that is a directory is unreadable (not a regular file)', () => {
    const result = evaluate({}, (dir) => put(dir, `${STRUCTURE_PATH}/keep.txt`, 'x\n'));
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.payload.reason, 'cannot-read-structure-md: not a regular file');
  });

  test('outside a git repository is a skip (not-a-git-repo)', () => {
    const result = evaluate({ git: false }, (dir) => put(dir, STRUCTURE_PATH, stamped('0123456789abcdef0123456789abcdef01234567')));
    assert.equal(result.outcome, 'skip');
    assert.equal(result.payload.reason, 'not-a-git-repo');
  });

  test('a map with no stamped baseline is a skip (no comparison is possible), reporting a null baseline', () => {
    const result = evaluate({}, mappedProject({ stamp: 'none' }));
    assert.equal(result.outcome, 'skip');
    assert.deepStrictEqual(result.payload, { block: false, skipped: true, reason: 'no-mapped-commit', action_required: false, directive: 'none', elements: [], last_mapped_commit: null });
  });

  test('a stamp git cannot resolve is unreadable, naming the stamp', () => {
    const result = evaluate({}, mappedProject({ stamp: 'fake' }));
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.block, false);
    assert.equal(result.payload.reason, 'unresolvable-mapped-commit');
    assert.equal(result.payload.last_mapped_commit, '0123456789abcdef0123456789abcdef01234567');
  });

  test('a stamp naming a resolvable non-commit (a tree) is unreadable too', () => {
    const result = evaluate({}, mappedProject({ stamp: 'tree' }));
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.payload.reason, 'unresolvable-mapped-commit');
  });

  test('a failing git diff is unreadable (git-diff-failed)', () => {
    const result = evaluate({}, (dir) => {
      mappedProject({ stamp: 'head' })(dir);
      return patchExecGit('diff', 128);
    });
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.block, false);
    assert.equal(result.payload.reason, 'git-diff-failed');
  });

  test('an unreadable sibling document makes a non-blocking answer unreadable and is named', () => {
    const result = evaluate({}, (dir) => {
      mappedProject({ stamp: 'head' })(dir);
      put(dir, '.planning/codebase/ARCHITECTURE.md/keep.txt', 'a directory where a document is expected\n');
    });
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.block, false);
    assert.deepStrictEqual(result.payload.documents_unreadable, ['ARCHITECTURE.md']);
    assert.deepStrictEqual(result.payload.documents_read, ['STRUCTURE.md']);
  });

  test('an unreadable sibling document does not lift a blocking verdict', () => {
    const result = evaluate({}, (dir) => {
      mappedProject({ stamp: 'head', extraDirs: ['alpha', 'beta', 'gamma'] })(dir);
      put(dir, '.planning/codebase/ARCHITECTURE.md/keep.txt', 'a directory where a document is expected\n');
    });
    assert.equal(result.outcome, 'block');
    assert.equal(result.block, true);
    assert.deepStrictEqual(result.payload.documents_unreadable, ['ARCHITECTURE.md']);
  });

  test('an absent sibling document is simply not part of the map (not unreadable)', () => {
    const result = evaluate({}, mappedProject({ stamp: 'head' }));
    assert.equal(result.outcome, 'pass');
    assert.deepStrictEqual(result.payload.documents_unreadable, []);
  });
});

describe('evaluateCodebaseDriftGate: the threshold boundary (limit-1 / limit / limit+1)', () => {
  const dirsNamed = (n) => Array.from({ length: n }, (_, i) => `newdir${i}`);

  for (const [count, outcome, block] of [[0, 'pass', false], [2, 'pass', false], [3, 'block', true], [4, 'block', true]]) {
    test(`${count} unmapped new directories against the default threshold of 3 -> ${outcome}`, () => {
      const result = evaluate({}, mappedProject({ stamp: 'head', extraDirs: dirsNamed(count) }));
      assert.equal(result.outcome, outcome);
      assert.equal(result.block, block);
      assert.equal(result.payload.block, block);
      assert.equal(result.payload.action_required, block);
      assert.equal(result.payload.threshold, 3);
      assert.equal(result.payload.elements.length, count);
    });
  }

  test('a configured threshold of 2 blocks at 2 and not at 1', () => {
    const run = (count) => evaluate({}, mappedProject({ stamp: 'head', extraDirs: dirsNamed(count), config: { workflow: { drift_threshold: 2 } } }));
    assert.equal(run(1).outcome, 'pass');
    const atLimit = run(2);
    assert.equal(atLimit.outcome, 'block');
    assert.equal(atLimit.payload.threshold, 2);
  });

  test('a threshold that is not a positive integer falls back to 3', () => {
    for (const value of [0, -1, 1.5, '2', null]) {
      const result = evaluate({}, mappedProject({ stamp: 'head', extraDirs: dirsNamed(2), config: { workflow: { drift_threshold: value } } }));
      assert.equal(result.payload.threshold, 3, `drift_threshold ${JSON.stringify(value)}`);
      assert.equal(result.outcome, 'pass', `drift_threshold ${JSON.stringify(value)}`);
    }
  });

  test('auto-remap names the paths to remap and asks for the mapper; any other action is a warning', () => {
    const run = (action) => evaluate({}, mappedProject({ stamp: 'head', extraDirs: dirsNamed(3), config: { workflow: { drift_action: action } } })).payload;
    const remap = run('auto-remap');
    assert.equal(remap.directive, 'auto-remap');
    assert.equal(remap.spawn_mapper, true);
    assert.equal(remap.action, 'auto-remap');
    const warn = run('anything-else');
    assert.equal(warn.directive, 'warn');
    assert.equal(warn.spawn_mapper, false);
    assert.equal(warn.action, 'warn');
  });

  test('the blocking payload carries every documented key in wire order', () => {
    const result = evaluate({}, mappedProject({ stamp: 'head', extraDirs: dirsNamed(3) }));
    assert.deepStrictEqual(Object.keys(result.payload), [
      'block', 'skipped', 'reason', 'action_required', 'directive', 'spawn_mapper', 'affected_paths', 'withheld_paths',
      'withheld_count', 'documents_read', 'documents_unreadable', 'elements', 'threshold', 'action', 'last_mapped_commit', 'message',
    ]);
    assert.equal(typeof result.payload.last_mapped_commit, 'string');
    assert.deepStrictEqual(result.payload.elements.map((e) => e.category), ['new_dir', 'new_dir', 'new_dir']);
  });

  test('the map\'s own planning artifacts are not structure: a re-stamp commit alone is not drift', () => {
    const result = evaluate({}, mappedProject({ stamp: 'head' }));
    assert.equal(result.outcome, 'pass');
    assert.deepStrictEqual(result.payload.elements, []);
  });

  test('property: the verdict blocks exactly when the unmapped directories reach the threshold', () => {
    fc.assert(fc.property(fc.integer({ min: 1, max: 4 }), fc.integer({ min: 0, max: 5 }), (threshold, count) => {
      const result = evaluate({}, mappedProject({ stamp: 'head', extraDirs: dirsNamed(count), config: { workflow: { drift_threshold: threshold } } }));
      return result.block === (count >= threshold) && result.outcome === (count >= threshold ? 'block' : 'pass');
    }), { seed: 5219, numRuns: 20 });
  });
});

describe('evaluateCodebaseDriftGate: the mapped-commit stamp is a hex object id before git sees it', () => {
  /** Run with `execGit` recording every call; returns the result and the recorded argument lists. */
  function withGitCalls(setup) {
    const calls = [];
    const real = shell.execGit;
    const result = evaluate({}, (dir) => {
      setup(dir);
      shell.execGit = function recording(args, ...rest) {
        calls.push([...args]);
        return real.call(shell, args, ...rest);
      };
      return () => { shell.execGit = real; };
    });
    return { result, calls };
  }
  // `rev-parse HEAD` is the gate's own repository probe and legitimately names HEAD; the stamp is what
  // the baseline probe and the diff would be given.
  const reachedGit = (calls, stamp) => calls.some((args) => args[0] !== 'rev-parse' && args.includes(stamp));

  // [label, raw stamp, whether it is a hex object id (7..64 hex chars) so that git is asked]
  const NOT_HEX_IDS = [
    ['6 hex chars (limit-1, an abbreviation too short)', 'abcdef'],
    ['65 hex chars (limit+1)', 'a'.repeat(65)],
    ['a ref name', 'HEAD'],
    ['an option-shaped value', '--batch'],
    ['a 40-char id with a non-hex character', `${'a'.repeat(39)}g`],
  ];
  for (const [label, raw] of NOT_HEX_IDS) {
    test(`${label} is routed to unresolvable-mapped-commit and never reaches git`, () => {
      const { result, calls } = withGitCalls(mappedProject({ stamp: 'head', raw }));
      assert.equal(result.outcome, 'unreadable');
      assert.equal(result.block, false);
      assert.equal(result.payload.reason, 'unresolvable-mapped-commit');
      assert.equal(result.payload.last_mapped_commit, raw, 'the stamp is reported as read');
      assert.equal(reachedGit(calls, raw), false, `${JSON.stringify(raw)} must not be an argument of any git call`);
      assert.equal(calls.some((args) => args[0] === 'cat-file' || args[0] === 'diff'), false, 'no baseline probe or diff runs');
    });
  }

  // Hex ids that pass the check ARE handed to git, which then decides (limit, and the sizes between).
  for (const [label, raw] of [['7 hex chars (limit)', 'abcdef0'], ['40 hex chars', 'a'.repeat(40)], ['64 hex chars (limit)', 'a'.repeat(64)]]) {
    test(`${label} that names no object is asked of git and is unresolvable there`, () => {
      const { result, calls } = withGitCalls(mappedProject({ stamp: 'head', raw }));
      assert.equal(result.outcome, 'unreadable');
      assert.equal(result.payload.reason, 'unresolvable-mapped-commit');
      assert.equal(calls.some((args) => args[0] === 'cat-file' && args.includes(raw)), true, 'git was asked');
    });
  }

  test('a 7-hex abbreviation of a real commit is a baseline (limit)', () => {
    const result = evaluate({}, mappedProject({ stamp: 'abbrev7', extraDirs: ['alpha', 'beta', 'gamma'] }));
    assert.equal(result.outcome, 'block');
    assert.equal(result.payload.last_mapped_commit.length, 7);
  });

  test('an uppercase hex id is a baseline (the id check is case-insensitive)', () => {
    const result = evaluate({}, mappedProject({ stamp: 'upper', extraDirs: ['alpha', 'beta', 'gamma'] }));
    assert.equal(result.outcome, 'block');
  });

  test('an absent stamp is still no-mapped-commit, not unresolvable (the check runs on a present stamp only)', () => {
    assert.equal(evaluate({}, mappedProject({ stamp: 'none' })).payload.reason, 'no-mapped-commit');
  });
});

describe('evaluateCodebaseDriftGate: the document size boundary (limit-1 / limit / limit+1), for every document readDocument reads', () => {
  const sizedBody = (bytes, lead) => {
    const body = `${lead}${'x'.repeat(bytes - Buffer.byteLength(lead))}`;
    assert.equal(Buffer.byteLength(body), bytes);
    return body;
  };

  for (const bytes of [MAX_DOCUMENT_BYTES - 1, MAX_DOCUMENT_BYTES]) {
    test(`a ${bytes}-byte STRUCTURE.md is read (the next step is the git probe)`, () => {
      const result = evaluate({ git: false }, (dir) => put(dir, STRUCTURE_PATH, sizedBody(bytes, STRUCTURE)));
      assert.equal(result.outcome, 'skip');
      assert.equal(result.payload.reason, 'not-a-git-repo');
    });
  }

  test(`a ${MAX_DOCUMENT_BYTES + 1}-byte STRUCTURE.md is refused as unreadable`, () => {
    const result = evaluate({ git: false }, (dir) => put(dir, STRUCTURE_PATH, sizedBody(MAX_DOCUMENT_BYTES + 1, STRUCTURE)));
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.payload.reason, `cannot-read-structure-md: larger than ${MAX_DOCUMENT_BYTES} bytes`);
  });

  // The sibling map documents share readDocument: at the limit they are read and part of the map, one byte over
  // they are named unreadable (and, with no drift, make the answer unreadable).
  for (const name of ['STACK.md', 'ARCHITECTURE.md', 'CONVENTIONS.md', 'TESTING.md', 'INTEGRATIONS.md', 'CONCERNS.md']) {
    for (const [bytes, read] of [[MAX_DOCUMENT_BYTES - 1, true], [MAX_DOCUMENT_BYTES, true], [MAX_DOCUMENT_BYTES + 1, false]]) {
      test(`${name} of ${bytes} bytes (${bytes === MAX_DOCUMENT_BYTES ? 'limit' : bytes < MAX_DOCUMENT_BYTES ? 'limit-1' : 'limit+1'}) is ${read ? 'read' : 'named unreadable'}`, () => {
        const result = evaluate({}, (dir) => {
          mappedProject({ stamp: 'head' })(dir);
          put(dir, `.planning/codebase/${name}`, sizedBody(bytes, '# Doc\n'));
        });
        if (read) {
          assert.equal(result.outcome, 'pass');
          assert.deepStrictEqual(result.payload.documents_unreadable, []);
          assert.ok(result.payload.documents_read.includes(name));
        } else {
          assert.equal(result.outcome, 'unreadable');
          assert.deepStrictEqual(result.payload.documents_unreadable, [name]);
          assert.ok(!result.payload.documents_read.includes(name));
        }
      });
    }
  }
});

describe('evaluateCodebaseDriftGate: the non-blocking contract', () => {
  test('a throw is a non-blocking unreadable verdict carrying the message, never a crash', () => {
    const result = evaluate({}, (dir) => {
      mappedProject({ stamp: 'head' })(dir);
      return patchExecGit('diff', 'throw');
    });
    assert.equal(isGateUsageFailure(result), false);
    assert.equal(result.outcome, 'unreadable');
    assert.equal(result.block, false);
    assert.equal(result.payload.block, false);
    assert.equal(result.payload.reason, 'exception: simulated git failure');
    assert.equal(result.payload.skipped, true);
  });

  test('an invalid GSD_WORKSTREAM throws inside the gate and is the same unreadable verdict', () => {
    const previous = process.env.GSD_WORKSTREAM;
    process.env.GSD_WORKSTREAM = '../escape';
    let result;
    try {
      result = evaluate({}, undefined);
    } finally {
      if (previous === undefined) delete process.env.GSD_WORKSTREAM;
      else process.env.GSD_WORKSTREAM = previous;
    }
    assert.equal(result.outcome, 'unreadable');
    assert.match(result.payload.reason, /^exception: /);
  });

  test('a path git reports with a C-quoted non-ASCII name is decoded before it is classified', () => {
    const result = evaluate({}, (dir) => {
      mappedProject({ stamp: 'head' })(dir);
      for (const name of ['设计', '设计2', '设计3']) put(dir, `${name}/index.js`, 'x\n');
      commitAll(dir, 'feat: add non-ascii directories');
    });
    assert.equal(result.outcome, 'block');
    assert.deepStrictEqual(result.payload.elements.map((e) => e.path).sort(), ['设计/index.js', '设计2/index.js', '设计3/index.js']);
  });
});
