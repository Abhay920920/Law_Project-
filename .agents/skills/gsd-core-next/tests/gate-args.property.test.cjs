'use strict';

/**
 * Properties of `partitionPredicateArgs` / `parsePredicateFlags` (src/gate-args.cts), the one
 * `--flag value` / positional splitter the flag-taking `check` verbs share (#5139, epic #5056,
 * ADR-5057 Phase 6). The contract is the one `check predicate` established (#2008), exercised by
 * tests/check-predicate.test.cjs:
 *   - `--flag` followed by a non-`--` token consumes it as the value (last write wins);
 *   - a `--flag` with no value (end of argv, or the next token starts with `--`) stays a bare
 *     token and moves to the positionals; a bare `--` (empty key) is always a positional;
 *   - every other token is a positional, in argv order;
 *   - nothing is dropped: tokens == positionals + two per consumed flag pair.
 *
 * Every property has a POSITIVE CONTROL: a deliberately broken parser the same property must reject.
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fc = require('./helpers/fast-check-setup.cjs');

const { partitionPredicateArgs, parsePredicateFlags } = require('../gsd-core/bin/lib/gate-args.cjs');

const tokenArb = fc.oneof(
  { weight: 5, arbitrary: fc.constantFrom('--a', '--b', '--dir', '--context', '--__proto__', '--constructor', '--', '---x', '--a=1', 'x', 'y z', '', ' ', '-q', '-') },
  { weight: 2, arbitrary: fc.string({ maxLength: 4 }) },
  { weight: 2, arbitrary: fc.string({ maxLength: 4 }).map((s) => `--${s}`) },
);
const argvArb = fc.array(tokenArb, { maxLength: 12 });

/** Independent reference: a cursor walk that also counts the consumed pairs. */
function reference(args) {
  const flags = new Map();
  const positionals = [];
  let pairs = 0;
  let i = 0;
  while (i < args.length) {
    const token = args[i];
    const hasKey = token.startsWith('--') && token.length > 2;
    const hasValue = hasKey && i + 1 < args.length && !args[i + 1].startsWith('--');
    if (hasValue) {
      flags.set(token.slice(2), args[i + 1]);
      pairs++;
      i += 2;
    } else {
      positionals.push(token);
      i += 1;
    }
  }
  return { flags, positionals, pairs };
}

function isSubsequence(sub, full) {
  let at = 0;
  for (const item of full) {
    if (at < sub.length && sub[at] === item) at++;
  }
  return at === sub.length;
}

/** The whole contract as one predicate over (parser, argv). */
function honoursContract(parse, args) {
  const { flags, positionals } = parse(args);
  const expected = reference(args);
  // (1) same split as the independent reference (flags compared key-sorted: an object orders
  // integer-like keys first, the reference's Map keeps insertion order)
  const byKey = (a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0);
  if (JSON.stringify(Object.entries(flags).sort(byKey)) !== JSON.stringify([...expected.flags.entries()].sort(byKey))) return false;
  if (JSON.stringify(positionals) !== JSON.stringify(expected.positionals)) return false;
  // (2) nothing dropped: tokens == positionals + 2 per consumed pair
  if (args.length !== positionals.length + 2 * expected.pairs) return false;
  // (3) positionals are an order-preserving subsequence of argv
  if (!isSubsequence(positionals, args)) return false;
  // (4) every flag is an adjacent `--key value` pair in argv, and a duplicated key keeps its LAST value
  for (const [key, value] of Object.entries(flags)) {
    let last = -1;
    for (let i = 0; i + 1 < args.length; i++) {
      if (args[i] === `--${key}` && !args[i + 1].startsWith('--')) last = i;
    }
    if (last === -1 || args[last + 1] !== value) return false;
  }
  return true;
}

function assertPropertyFails(property, what) {
  let failed = false;
  try {
    fc.assert(property);
  } catch {
    failed = true;
  }
  assert.ok(failed, `positive control: ${what} must be rejected by the property`);
}

// Deliberately broken parsers (the positive controls).
function dropsValuelessFlags(args) {
  const flags = {};
  const positionals = [];
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (!a.startsWith('--')) { positionals.push(a); continue; }
    const key = a.slice(2);
    if (key.length > 0 && i + 1 < args.length && !args[i + 1].startsWith('--')) {
      flags[key] = args[i + 1];
      i++;
    } // else: the valueless flag vanishes — the bug
  }
  return { flags, positionals };
}

function firstWriteWins(args) {
  const out = partitionPredicateArgs(args);
  const flags = {};
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a.startsWith('--') && a.length > 2 && i + 1 < args.length && !args[i + 1].startsWith('--')) {
      const key = a.slice(2);
      if (!(key in flags)) flags[key] = args[i + 1];
      i++;
    }
  }
  return { flags, positionals: out.positionals };
}

function reversesPositionals(args) {
  const out = partitionPredicateArgs(args);
  return { flags: out.flags, positionals: [...out.positionals].reverse() };
}

describe('partitionPredicateArgs — properties', () => {
  test('P1: the split equals an independent reference, drops nothing and keeps argv order', () => {
    fc.assert(fc.property(argvArb, (args) => honoursContract(partitionPredicateArgs, args)));
  });

  test('P1 control: a parser that drops valueless flags is rejected', () => {
    assertPropertyFails(fc.property(argvArb, (args) => honoursContract(dropsValuelessFlags, args)), 'dropsValuelessFlags');
  });

  test('P1 control: a first-write-wins parser is rejected', () => {
    assertPropertyFails(fc.property(argvArb, (args) => honoursContract(firstWriteWins, args)), 'firstWriteWins');
  });

  test('P1 control: a parser that reverses the positionals is rejected', () => {
    assertPropertyFails(fc.property(argvArb, (args) => honoursContract(reversesPositionals, args)), 'reversesPositionals');
  });

  test('P2: argv with no `--` token is all positionals, in order, with no flags', () => {
    const plain = fc.array(fc.string({ maxLength: 6 }).filter((s) => !s.startsWith('--')), { maxLength: 10 });
    fc.assert(fc.property(plain, (args) => {
      const out = partitionPredicateArgs(args);
      return Object.keys(out.flags).length === 0 && JSON.stringify(out.positionals) === JSON.stringify(args);
    }));
  });

  test('P3: a valid `--key value` pair anywhere is a flag and leaves its neighbours positional', () => {
    const key = fc.string({ minLength: 1, maxLength: 6 }).filter((s) => !s.startsWith('-'));
    const value = fc.string({ maxLength: 6 }).filter((s) => !s.startsWith('--'));
    const positional = fc.array(fc.string({ maxLength: 4 }).filter((s) => !s.startsWith('--')), { maxLength: 4 });
    fc.assert(fc.property(positional, key, value, positional, (before, k, v, after) => {
      const out = partitionPredicateArgs([...before, `--${k}`, v, ...after]);
      return out.flags[k] === v
        && Object.keys(out.flags).length === 1
        && JSON.stringify(out.positionals) === JSON.stringify([...before, ...after]);
    }));
  });

  test('P4: parsePredicateFlags is exactly the flags half of the same pass', () => {
    fc.assert(fc.property(argvArb, (args) => {
      assert.deepStrictEqual(parsePredicateFlags(args), partitionPredicateArgs(args).flags);
      return true;
    }));
  });

  test('P5: `--__proto__ value` is an own flag; it neither vanishes nor touches the prototype', () => {
    const out = partitionPredicateArgs(['--__proto__', 'polluted', 'x']);
    assert.equal(Object.prototype.hasOwnProperty.call(out.flags, '__proto__'), true);
    assert.equal(Object.getOwnPropertyDescriptor(out.flags, '__proto__').value, 'polluted');
    assert.equal(Object.getPrototypeOf(out.flags), Object.prototype);
    assert.equal(({}).polluted, undefined);
    assert.deepStrictEqual(out.positionals, ['x']);
  });

  test('P6: non-string argv entries are skipped and a `--flag` before one stays positional', () => {
    const out = partitionPredicateArgs(['a', 5, '--k', null, 'b']);
    assert.deepStrictEqual(out.positionals, ['a', '--k', 'b']);
    assert.deepStrictEqual(Object.keys(out.flags), []);
  });
});
