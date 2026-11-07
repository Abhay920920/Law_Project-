/**
 * #4928: `gsd-doc-writer`'s README / getting-started / per-package templates
 * required a literal runnable command with visible output, which cannot exist
 * for secrets-only backend monorepos or event-triggered handlers (a Lambda
 * behind SNS/Kinesis has no CLI or HTTP surface). The agent is prose, so this
 * pins the instruction text: each affected template carries the fallback
 * ordering, the Content Discovery rule checks that a cited script's inputs
 * exist, and the canonical and compact variants say the same thing.
 */
'use strict';

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const AGENTS = path.join(__dirname, '..', 'agents');
const VARIANTS = {
  canonical: fs.readFileSync(path.join(AGENTS, 'gsd-doc-writer.md'), 'utf8'),
  compact: fs.readFileSync(path.join(AGENTS, 'gsd-doc-writer.compact.md'), 'utf8'),
};

function section(text, tag) {
  const m = text.match(new RegExp(`<${tag}>([\\s\\S]*?)</${tag}>`));
  assert.ok(m, `missing <${tag}> section`);
  return m[1];
}

const NO_RUN_MODE = /no\s+standalone\s+run\s+mode/i; // prose wraps at 100 columns
const FALLBACK_ORDER = /build[\s\S]{0,80}test|test[\s\S]{0,80}build/i;

for (const [variant, text] of Object.entries(VARIANTS)) {
  describe(`#4928 gsd-doc-writer (${variant}): targets with no runnable entry point`, () => {
    test('getting-started "First run" falls back to build/test, then an explicit "no standalone run mode"', () => {
      const s = section(text, 'template_getting_started');
      assert.match(s, NO_RUN_MODE);
      assert.match(s, FALLBACK_ORDER);
    });

    test('README "Quick start" and "Usage examples" do not require a literal run command', () => {
      const s = section(text, 'template_readme');
      assert.match(s, NO_RUN_MODE);
    });

    test('per-package README "Usage" states there is no standalone run mode for event-triggered handlers', () => {
      const s = section(text, 'template_readme_per_package');
      assert.match(s, NO_RUN_MODE);
      assert.match(s, /event|handler|Lambda/i);
    });

    test('a cited script or Makefile target is only presented as working when its inputs exist', () => {
      const rules = section(text, 'critical_rules');
      assert.match(rules, /fixture/i);
      assert.match(rules, /inputs/i);
      assert.match(rules, /Makefile|package\.json script/i);
      assert.match(rules, /VERIFY/);
    });

    test('the normal path is unchanged: start/dev scripts and CLI bins are still the first choice', () => {
      assert.match(section(text, 'template_getting_started'), /scripts\.start/);
      assert.match(section(text, 'template_readme'), /scripts\.start/);
    });
  });
}
