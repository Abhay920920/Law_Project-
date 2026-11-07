/**
 * #4929: the component-inventory gate in `gsd-ui-researcher` trusted the first
 * enumeration command that answered and assumed `node_modules` exists:
 *
 *   - `npx shadcn info` can report "No components installed" on a project with
 *     real components on disk (hand-authored or pre-CLI-tracking primitives), and
 *     nothing cross-checked it, so an empty inventory capped every screen.
 *   - `node -p "require('<pkg>/package.json').version"` with stderr suppressed
 *     prints nothing when `node_modules` is absent (exit 1), which reads like "no
 *     output" instead of a failure; a lockfile lookup for a missing package prints
 *     `undefined` with exit 0.
 *
 * The gate is agent prose, so this pins the instruction text in both the full and
 * the compact researcher, and pins that the provenance-line grammar the checker's
 * Dimension 7 parses did not move.
 */
'use strict';

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const AGENTS = path.join(__dirname, '..', 'agents');
const read = (f) => fs.readFileSync(path.join(AGENTS, f), 'utf8');
const VARIANTS = {
  full: read('gsd-ui-researcher.md'),
  compact: read('gsd-ui-researcher.compact.md'),
};
const CHECKER = read('gsd-ui-checker.md');

function gate(text) {
  const m = text.match(/<component_inventory_gate>([\s\S]*?)<\/component_inventory_gate>/);
  assert.ok(m, 'missing <component_inventory_gate>');
  return m[1];
}

const PROVENANCE_LINE = 'Enumerated by `<command>` — <N> components — <package>@<version> — <YYYY-MM-DD>.';

for (const [variant, text] of Object.entries(VARIANTS)) {
  describe(`#4929 gsd-ui-researcher (${variant}): the inventory gate checks itself`, () => {
    const g = gate(text);

    test('the first tool answer is always cross-checked against a filesystem count', () => {
      assert.match(g, /cross-check/i);
      assert.match(g, /\bls\b|filesystem/i);
      assert.match(g, /always|every|mandatory|never skip/i);
    });

    test('a zero or mismatched first-tier count is recorded, not silently resolved', () => {
      assert.match(g, /mismatch|disagree/i);
      assert.match(g, /No components installed|zero|none/i);
    });

    test('version resolution falls back to the lockfile when node_modules is absent', () => {
      assert.match(g, /package-lock\.json/);
      assert.match(g, /node_modules\/<pkg>|packages\[/);
      assert.match(g, /pnpm-lock\.yaml|yarn\.lock/);
    });

    test('empty output, a non-zero exit and "undefined" are failures, never a pass-through', () => {
      assert.match(g, /empty output|no output/i);
      assert.match(g, /non-zero|exit/i);
      assert.match(g, /undefined/);
    });

    test('a caret range from the dependent package.json is still not a resolved version', () => {
      assert.match(g, /caret range/i);
    });

    test('the provenance line the checker parses is unchanged', () => {
      assert.ok(g.includes(PROVENANCE_LINE), 'the recorded-line grammar moved');
      assert.ok(CHECKER.includes(PROVENANCE_LINE), 'Dimension 7 no longer quotes the same grammar');
      assert.match(g, /Could not enumerate: <reason>\./);
    });
  });
}
