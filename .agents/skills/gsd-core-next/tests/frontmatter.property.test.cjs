'use strict';

/**
 * Property-based tests for frontmatter.cjs
 *
 * Module: gsd-core/bin/lib/frontmatter.cjs
 * Exported (pure): extractFrontmatter, reconstructFrontmatter, spliceFrontmatter
 *
 * Properties tested:
 *   (a) extractFrontmatter never throws on ANY string input (including binary/unicode)
 *   (b) extractFrontmatter always returns a plain object (not null, not array)
 *   (c)/(d) spliceFrontmatter's own properties live in tests/frontmatter-splice.property.test.cjs
 *       since the writer moved to frontmatter-splice.cts (#5105)
 *   (e) extractFrontmatter returns {} for content without a leading ---...--- block
 *   (f) prohibitions bijection (#644): over a generated must_haves.prohibitions block,
 *       parseMustHavesBlock(spliceFrontmatter(doc, parseFrontmatter(doc)), 'prohibitions')
 *       deepEquals the original parse — the new parse ↔ splice path is identity-preserving.
 */

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const fc = require('./helpers/fast-check-setup.cjs');
const yaml = require('js-yaml');

const {
  extractFrontmatter,
  reconstructFrontmatter,
  spliceFrontmatter,
  parseFrontmatter,
  parseMustHavesBlock,
  frontmatterKeyHasValue,
  frontmatterKeyBlockText,
  frontmatterRegion,
} = require('../gsd-core/bin/lib/frontmatter.cjs');
const { escapeRegex } = require('../gsd-core/bin/lib/pattern.cjs');

// ─── frontmatterKeyHasValue / frontmatterKeyBlockText — property (#5139) ──────────────────────────
// Key and value are matched LITERALLY: no caller-supplied key or value can change the pattern.
describe('frontmatterKeyHasValue / frontmatterKeyBlockText — property (fast-check)', () => {
  // Metacharacters on purpose; no whitespace, no newline, no `-` (so a line can never read as a fence).
  const ALPHABET = 'ab.*+?^${}()|[]\\'.split('');
  const tokenArb = fc.array(fc.constantFrom(...ALPHABET), { minLength: 1, maxLength: 8 }).map((chars) => chars.join(''));
  const wsArb = fc.constantFrom('', ' ', '  ', '\t');
  const modeArb = fc.constantFrom('match', 'longer', 'shorter-key', 'wrong-key-prefix');

  /** Reference: split the region into lines and compare — no regex anywhere. */
  function reference(doc, key, value) {
    const lines = doc.split('\n');
    const close = lines.indexOf('---', 1);
    return lines.slice(1, close).some((line) => line.startsWith(`${key}:`) && line.slice(key.length + 1).trim() === value);
  }

  function build(key, value, ws1, ws2, mode) {
    let line;
    if (mode === 'match') line = `${key}:${ws1}${value}${ws2}`;
    else if (mode === 'longer') line = `${key}:${ws1}${value}zz${ws2}`;
    else if (mode === 'shorter-key') line = `${key.slice(0, -1)}:${ws1}${value}${ws2}`;
    else line = `q${key}:${ws1}${value}${ws2}`;
    return `---\nzz: 1\n${line}\nzz: 2\n---\nbody\n`;
  }

  const property = (hasValue) => fc.property(tokenArb, tokenArb, wsArb, wsArb, modeArb, (key, value, ws1, ws2, mode) => {
    const doc = build(key, value, ws1, ws2, mode);
    return hasValue(doc, key, value) === reference(doc, key, value);
  });

  function assertPropertyFails(prop, what) {
    let failed = false;
    try {
      fc.assert(prop);
    } catch {
      failed = true;
    }
    assert.ok(failed, `positive control: ${what} must be rejected by the property`);
  }

  test('P1: never throws, and agrees with a line-splitting reference for keys/values full of metacharacters', () => {
    fc.assert(property(frontmatterKeyHasValue));
  });

  test('P1 control: a variant that interpolates key and value UNESCAPED is rejected (throws or disagrees)', () => {
    const unescaped = (content, key, value) => {
      const found = frontmatterRegion(content);
      if (!found || !found.terminated) return false;
      return new RegExp(`^${key}:\\s*${value}\\s*$`, 'm').test(found.region);
    };
    assertPropertyFails(property(unescaped), 'the unescaped variant');
  });

  test('P1 control: a variant that escapes only the value is rejected', () => {
    const valueOnly = (content, key, value) => {
      const found = frontmatterRegion(content);
      if (!found || !found.terminated) return false;
      return new RegExp(`^${key}:\\s*${escapeRegex(value)}\\s*$`, 'm').test(found.region);
    };
    assertPropertyFails(property(valueOnly), 'the value-only-escaped variant');
  });

  test('P2: frontmatterKeyBlockText finds a metacharacter key exactly when the literal line is present, and returns its value line', () => {
    fc.assert(fc.property(tokenArb, tokenArb, wsArb, (key, value, ws) => {
      const doc = `---\nzz: 1\n${key}:${ws}${value}\nzz: 2\n---\nbody\n`;
      return frontmatterKeyBlockText(doc, key) === `${ws}${value}`;
    }));
  });

  test('P2: a doc without the literal key line yields the empty string (never throws)', () => {
    fc.assert(fc.property(tokenArb, tokenArb, (key, value) => {
      const doc = `---\nzz: 1\nq${key}x: ${value}\n---\nbody\n`;
      return frontmatterKeyBlockText(doc, key) === '';
    }));
  });
});

// ─── Arbitraries ─────────────────────────────────────────────────────────────

// Simple YAML key: alphanumeric + underscore, at least 1 char
const yamlKey = fc.stringMatching(/^[a-z][a-z0-9_]{0,19}$/);

// Simple YAML scalar value: printable ASCII without : ' " # newlines
const yamlScalarValue = fc.stringMatching(/^[a-zA-Z0-9 ._/-]{1,40}$/);

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('frontmatter: extractFrontmatter properties', () => {
  // (a) Never throws on any string input
  test('property: extractFrontmatter never throws on arbitrary binary/unicode input', () => {
    fc.assert(
      fc.property(
        fc.oneof(
          fc.string({ unit: 'binary', maxLength: 300 }),
          fc.string({ unit: 'grapheme-composite', maxLength: 300 }),
          fc.constant(''),
          fc.constant('---\n---'),
          fc.constant('---\nkey: value\n---\n# body'),
          fc.string({ maxLength: 300 })
        ),
        (input) => {
          assert.doesNotThrow(
            () => extractFrontmatter(input),
            `extractFrontmatter threw on input: ${JSON.stringify(input.slice(0, 50))}`
          );
        }
      )
    );
  });

  // (b) Always returns a plain object
  test('property: extractFrontmatter always returns a plain object', () => {
    fc.assert(
      fc.property(
        fc.oneof(
          fc.string({ unit: 'binary', maxLength: 200 }),
          fc.string({ unit: 'grapheme-composite', maxLength: 200 }),
          fc.string({ maxLength: 200 })
        ),
        (input) => {
          const result = extractFrontmatter(input);
          assert.ok(
            typeof result === 'object' && result !== null && !Array.isArray(result),
            `extractFrontmatter must return plain object, got ${JSON.stringify(result)}`
          );
        }
      )
    );
  });

  // (e) Returns {} for content without leading --- block
  test('property: content without leading --- block returns empty object', () => {
    fc.assert(
      fc.property(
        fc.oneof(
          fc.string({ minLength: 0, maxLength: 200 }).filter((s) => !s.startsWith('---')),
          fc.constant('# Just a heading'),
          fc.constant('plain text content'),
          fc.constant('')
        ),
        (input) => {
          const result = extractFrontmatter(input);
          assert.deepEqual(
            result,
            {},
            `Expected {} for non-frontmatter input, got ${JSON.stringify(result)}`
          );
        }
      )
    );
  });
});

describe('frontmatter: reconstructFrontmatter properties', () => {
  test('property: reconstructFrontmatter never throws on plain objects with string values', () => {
    fc.assert(
      fc.property(
        fc.dictionary(yamlKey, yamlScalarValue, { maxKeys: 10 }),
        (obj) => {
          assert.doesNotThrow(
            () => reconstructFrontmatter(obj),
            `reconstructFrontmatter threw on ${JSON.stringify(obj)}`
          );
        }
      )
    );
  });

  test('property: reconstructFrontmatter output is a string', () => {
    fc.assert(
      fc.property(
        fc.dictionary(yamlKey, yamlScalarValue, { maxKeys: 8 }),
        (obj) => {
          const result = reconstructFrontmatter(obj);
          assert.ok(typeof result === 'string', `Expected string got ${typeof result}`);
        }
      )
    );
  });

  test('property: reconstructFrontmatter on {} returns empty string', () => {
    assert.equal(reconstructFrontmatter({}), '');
  });
});

// ─── (f) prohibitions bijection (#644) ────────────────────────────────────────
// Locks the new parseMustHavesBlock(…, 'prohibitions') ↔ spliceFrontmatter path that
// the prohibition probe adds. The example-based version lives in
// tests/prohibition-probe.schema.test.cjs; this generalizes it over generated blocks.

// YAML-safe scalar: starts with a letter, no colon/quote/hash/newline (so it parses as a
// plain string and is never coerced to a number by the parser's /^\d+$/ check).
const safeScalar = fc.stringMatching(/^[A-Za-z][A-Za-z0-9 ._-]{0,50}$/);

// One prohibition item with structurally realistic key shape per ADR-550 D7a:
//   resolved  → carries a verification tier (test|judgment)
//   dismissed → carries a non-empty reason (+ a tier)
//   unresolved→ neither
const prohibitionItem = fc.oneof(
  fc.record({ statement: safeScalar, status: fc.constant('resolved'),
    verification: fc.constantFrom('test', 'judgment') }),
  fc.record({ statement: safeScalar, status: fc.constant('dismissed'),
    verification: fc.constantFrom('test', 'judgment'), reason: safeScalar }),
  fc.record({ statement: safeScalar, status: fc.constant('unresolved') })
);

// Emit a frontmatter doc with a must_haves.prohibitions sibling block (keys in a fixed
// order: statement, status, verification?, reason?). Quoted strings carry the values.
function buildDoc(items) {
  const lines = ['---', 'phase: 01-x', 'plan: 01', 'must_haves:',
    '  truths:', '    - "User sees a daily reminder"', '  prohibitions:'];
  for (const it of items) {
    lines.push(`    - statement: "${it.statement}"`);
    lines.push(`      status: ${it.status}`);
    if (it.verification !== undefined) lines.push(`      verification: ${it.verification}`);
    if (it.reason !== undefined) lines.push(`      reason: "${it.reason}"`);
  }
  lines.push('---', '', 'Body text unchanged.', '');
  return lines.join('\n');
}

describe('frontmatter: prohibitions parse ↔ splice bijection (#644)', () => {
  test('property: generated prohibitions parse back with their statement and status', () => {
    fc.assert(
      fc.property(fc.array(prohibitionItem, { minLength: 1, maxLength: 5 }), (items) => {
        const doc = buildDoc(items);
        const parsed = parseMustHavesBlock(doc, 'prohibitions');
        assert.equal(parsed.length, items.length, 'every prohibition item must parse out');
        for (let i = 0; i < items.length; i++) {
          assert.equal(parsed[i].statement, items[i].statement, `statement[${i}] mismatch`);
          assert.equal(parsed[i].status, items[i].status, `status[${i}] mismatch`);
        }
      })
    );
  });

  test('property: parse -> splice -> re-parse is identity-preserving for prohibitions', () => {
    fc.assert(
      fc.property(fc.array(prohibitionItem, { minLength: 1, maxLength: 5 }), (items) => {
        const doc = buildDoc(items);
        const before = parseMustHavesBlock(doc, 'prohibitions');
        const parsed = parseFrontmatter(doc);
        const spliced = spliceFrontmatter(doc, parsed.frontmatter ?? parsed);
        const after = parseMustHavesBlock(spliced, 'prohibitions');
        assert.deepEqual(after, before,
          'prohibitions must survive a splice/re-parse round-trip unchanged');
      })
    );
  });
});

// #1779 — reconstructFrontmatter must emit YAML that a STRICT parser accepts and
// that preserves string values. The bijective contract is
//   ∀ s: yaml.load(reconstructFrontmatter({ k: s })).k === s
// over the documented safe-input subset. Two classes are out of scope and
// excluded here, not silently passed:
//   - lone UTF-16 surrogates (lossy through UTF-8 encoding) — filtered via
//     fc.pre(s.isWellFormed());
//   - numeric/boolean/null-looking BARE strings (e.g. "42", "true", "-5") that a
//     YAML loader resolves to a non-string type — a separate pre-existing bug
//     class (valid YAML, wrong type), so we assert equality only when the value
//     loads back AS a string. An escaping defect (invalid YAML) still fails
//     loudly because yaml.load() throws.
describe('frontmatter: reconstructFrontmatter strict-YAML property (#1779)', () => {
  test('property: every string value serializes to valid YAML and string-round-trips', () => {
    fc.assert(
      fc.property(fc.string({ maxLength: 200 }), (s) => {
        fc.pre(s.isWellFormed());
        // Throws → reconstructFrontmatter emitted invalid YAML → property fails
        // (fast-check shrinks + prints the replay seed automatically).
        const loaded = yaml.load(reconstructFrontmatter({ k: s }));
        if (typeof loaded.k === 'string') {
          assert.equal(loaded.k, s,
            `value did not round-trip through strict YAML: ${JSON.stringify(s)}`);
        }
      })
    );
  });
});

// (g)(h) #1882 added an optional `sourcePath` argument to extractFrontmatter, used only to
//     name and deduplicate a diagnostic. These two properties are what protect the ~50 call
//     sites: whatever the argument does, it must never reach the parsed result, and the
//     LF/CRLF equivalence the parser already promised must survive the new branch.
describe('frontmatter: extractFrontmatter sourcePath is parse-inert (#1882)', () => {
  test('property: the optional path argument never changes the parsed result', (t) => {
    const original = process.stderr.write;
    t.after(() => { process.stderr.write = original; });
    process.stderr.write = () => true;
      fc.assert(
        fc.property(
          fc.oneof(
            fc.string({ maxLength: 300 }),
            fc.string({ unit: 'binary', maxLength: 300 }),
          ),
          fc.stringMatching(/^\/[a-z0-9/_-]{1,40}\.md$/),
          (content, somePath) => {
            assert.deepEqual(
              extractFrontmatter(content, somePath),
              extractFrontmatter(content),
              'sourcePath must be inert with respect to the parsed value',
            );
          }
        )
      );
  });

  test('property: a document and its CRLF twin parse identically', (t) => {
    const original = process.stderr.write;
    t.after(() => { process.stderr.write = original; });
    process.stderr.write = () => true;
      fc.assert(
        fc.property(fc.string({ maxLength: 300 }), (content) => {
          const lf = content.replace(/\r\n/g, '\n');
          const crlf = lf.replace(/\n/g, '\r\n');
          assert.deepEqual(
            extractFrontmatter(crlf),
            extractFrontmatter(lf),
            'CRLF and LF spellings of one document must parse the same',
          );
        })
      );
  });
});
