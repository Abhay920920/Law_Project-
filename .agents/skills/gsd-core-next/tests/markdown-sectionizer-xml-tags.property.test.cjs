'use strict';

/**
 * Properties of `extractXmlTagBodies` (src/markdown-sectionizer.cts), the XML decision-tag body
 * extractor the decision-coverage gate reads plan surfaces through (#5139, epic #5056, ADR-5057
 * Phase 6; moved there from the router, where it was `buildXmlDecisionTagRegex`).
 *
 * The contract (pinned, pre-move, by tests/refactor-1390-t3-characterization.test.cjs):
 *   - for each name in XML_DECISION_TAG_NAMES, in that order, the body of every `<name ...>` element
 *     in text order; an empty body is dropped; the bodies are joined by `\n`;
 *   - each tag's body ends only at its OWN closing tag, so an inner scanned tag is absorbed into
 *     the outer body AND extracted on its own pass (a single wide alternation would lose the
 *     outer tag's prefix prose);
 *   - tag names and attributes: case-insensitive name, optional attributes after whitespace;
 *   - tags inside fenced code / HTML comments are NOT scanned where the gate's caller strips them
 *     first (`extractPlanDesignatedSections`).
 *
 * Every property has a POSITIVE CONTROL: a deliberately broken variant that the same property
 * must reject, so the property is proven able to fail.
 */

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const fc = require('./helpers/fast-check-setup.cjs');

const { extractXmlTagBodies, XML_DECISION_TAG_NAMES } = require('../gsd-core/bin/lib/markdown-sectionizer.cjs');
const { extractPlanDesignatedSections } = require('../gsd-core/bin/lib/check-command-router.cjs');

// Body text never holds `<`/`>` (so it cannot forge a tag) and never the word SECRET.
const BODY_ALPHABET = 'abcXYZ019 D-05.:,\n_';
const bodyArb = fc
  .array(fc.constantFrom(...BODY_ALPHABET.split('')), { minLength: 1, maxLength: 12 })
  .map((chars) => chars.join(''));
const tagArb = fc.constantFrom(...XML_DECISION_TAG_NAMES);
const attrsArb = fc.constantFrom('', ' id="1"', ' type="auto" n="2"', '\tx=y');
const sepArb = fc.constantFrom('', ' ', '\n', ' prose D-01 ');

const indexOfTag = (name) => XML_DECISION_TAG_NAMES.indexOf(name.toLowerCase());

/** Reference: bodies grouped by tag in XML_DECISION_TAG_NAMES order, text order inside a tag. */
function expectedFlat(pieces) {
  const parts = [];
  for (const name of XML_DECISION_TAG_NAMES) {
    for (const piece of pieces) {
      if (piece.tag === name) parts.push(piece.body);
    }
  }
  return parts.join('\n');
}

function render(piece) {
  const name = piece.upper ? piece.tag.toUpperCase() : piece.tag;
  return `<${name}${piece.attrs}>${piece.body}</${name}>`;
}

/** The fault a property must catch: the wide-alternation extractor the per-tag design replaced. */
function wideAlternationExtract(text) {
  const alternation = XML_DECISION_TAG_NAMES.join('|');
  const re = new RegExp(`<(${alternation})(?:\\s[^>]{0,1000})?>((?:(?!<(?:${alternation})[\\s>])[\\s\\S])*?)<\\/\\1>`, 'gi');
  const parts = [];
  for (const match of text.matchAll(re)) {
    if (match[2]) parts.push(match[2]);
  }
  return parts.join('\n');
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

const flatProperty = (extract) => fc.property(
  fc.array(fc.record({ tag: tagArb, body: bodyArb, attrs: attrsArb, upper: fc.boolean() }), { maxLength: 8 }),
  sepArb,
  (pieces, sep) => {
    const text = pieces.map(render).join(sep);
    return extract(text) === expectedFlat(pieces);
  },
);

// Outer element holds an inner scanned element after some prefix prose.
const nestedProperty = (extract) => fc.property(
  tagArb, tagArb, bodyArb, bodyArb,
  (outer, inner, prefix, innerBody) => {
    fc.pre(outer !== inner);
    const outerBody = `${prefix}<${inner}>${innerBody}</${inner}>`;
    const text = `<${outer}>${outerBody}</${outer}>`;
    const expected = [{ name: outer, body: outerBody }, { name: inner, body: innerBody }]
      .sort((a, b) => indexOfTag(a.name) - indexOfTag(b.name))
      .map((entry) => entry.body)
      .join('\n');
    return extract(text) === expected;
  },
);

describe('extractXmlTagBodies — properties', () => {
  test('P1: a flat run of scanned elements yields exactly their bodies, grouped by tag name order', () => {
    fc.assert(flatProperty(extractXmlTagBodies));
  });

  test('P1 control: a reference that ignores the tag-name grouping is rejected', () => {
    // Text order instead of XML_DECISION_TAG_NAMES order — wrong whenever two tags are swapped.
    const textOrder = (text) => {
      const re = new RegExp(`<(${XML_DECISION_TAG_NAMES.join('|')})(?:\\s[^>]{0,1000})?>([^<]*)<\\/\\1>`, 'gi');
      return [...text.matchAll(re)].map((m) => m[2]).filter(Boolean).join('\n');
    };
    assertPropertyFails(flatProperty(textOrder), 'a text-order extractor');
  });

  test('P2: an inner scanned tag is absorbed into the outer body AND extracted on its own pass', () => {
    fc.assert(nestedProperty(extractXmlTagBodies));
  });

  test('P2 control: the single wide alternation (prefix prose of the outer tag lost) is rejected', () => {
    assertPropertyFails(nestedProperty(wideAlternationExtract), 'the wide-alternation extractor');
  });

  test('P3: text with no scanned element yields the empty string', () => {
    fc.assert(fc.property(bodyArb, bodyArb, (a, b) => extractXmlTagBodies(`${a}<other>${b}</other>`) === ''));
  });

  test('P3 control: an extractor that returns its input is rejected', () => {
    assertPropertyFails(
      fc.property(bodyArb, bodyArb, (a, b) => ((text) => text)(`${a}<other>${b}</other>`) === ''),
      'an identity extractor',
    );
  });
});

describe('extractPlanDesignatedSections — tags inside fenced code or comments are not scanned', () => {
  const fenceArb = fc.constantFrom('```', '~~~', '````');
  const property = (extract) => fc.property(tagArb, bodyArb, tagArb, fenceArb, (realTag, realBody, hiddenTag, fence) => {
    const plan = [
      `<${realTag}>${realBody}</${realTag}>`,
      '',
      fence,
      `<${hiddenTag}>FENCED-SECRET</${hiddenTag}>`,
      fence,
      '',
      `<!-- <${hiddenTag}>COMMENTED-SECRET</${hiddenTag}> -->`,
      '',
    ].join('\n');
    const out = extract(plan);
    return out.includes(realBody) && !out.includes('FENCED-SECRET') && !out.includes('COMMENTED-SECRET');
  });

  test('P4: the real element is scanned; the fenced and the commented one are not', () => {
    fc.assert(property(extractPlanDesignatedSections));
  });

  test('P4 control: scanning the raw text (no fence/comment stripping) is rejected', () => {
    assertPropertyFails(property(extractXmlTagBodies), 'an extractor run over unstripped text');
  });
});
