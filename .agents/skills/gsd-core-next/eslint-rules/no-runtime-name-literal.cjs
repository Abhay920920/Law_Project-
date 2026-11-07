'use strict';

/**
 * no-runtime-name-literal (#5169, ADR-5057 Phase 10)
 *
 * The runtime descriptor (capabilities/<runtime>/capability.json) owns every
 * runtime-specific fact. Install and hook code that compares a runtime
 * identifier against a registered runtime-id literal is re-deriving a fact the
 * descriptor should declare — the shape behind `runtime === 'hermes'` cleanup
 * branches, `runtime === 'claude'` skip rules and a 14-case rewrite switch.
 * Read a descriptor field (`hostBehaviorsFor(runtime).<field>`) instead.
 *
 * Flags, in `src/**` `.cts`, `hooks/**` `.js` and `bin/install.js`, except the
 * owner `src/runtime-name-policy.cts`:
 *   - `===` / `!==` / `==` / `!=` where one side is a string literal equal to a
 *     registered runtime id and the other side is a runtime-identifier
 *     expression;
 *   - a `switch` over a runtime-identifier expression with a `case` whose test
 *     is such a literal.
 *
 * A runtime-identifier expression is an identifier or member property named
 * `runtime`, `runtimeId`, `runtimeName`, `activeRuntime`, `targetRuntime`,
 * `hostRuntime`, `canonical`, `canonicalRuntime`, `rt` (`layout.runtime`,
 * `ctx.runtime`, `opts.runtime`).
 *
 * Not flagged: a literal that is not a registered id (`transport ===
 * 'openai-http'`, `typeof runtime === 'string'`); a comparison whose other side
 * is not named like a runtime identifier; registry keys and object-literal
 * property keys (data, not comparisons); files outside the owned set.
 *
 * The registered-id set is derived at lint time from
 * `capabilities/<id>/capability.json` (`role: "runtime"`) plus the legacy
 * non-registry ids, so adding a runtime extends the guard with no edit here.
 */

const fs = require('node:fs');
const path = require('node:path');

/** Legacy ids with a dedicated branch but no descriptor (parity: runtime-name-policy.cts). */
const LEGACY_NON_REGISTRY_RUNTIME_IDS = Object.freeze(['grok']);

const RUNTIME_IDENTIFIER_RE = /^(?:runtime|runtimeId|runtimeName|activeRuntime|targetRuntime|hostRuntime|canonical|canonicalRuntime|rt)$/;

let cachedIds = null;

function loadRuntimeIds(repoRoot) {
  if (cachedIds && cachedIds.root === repoRoot) return cachedIds.ids;
  const ids = new Set(LEGACY_NON_REGISTRY_RUNTIME_IDS);
  const capDir = path.join(repoRoot, 'capabilities');
  let entries = [];
  try {
    entries = fs.readdirSync(capDir, { withFileTypes: true });
  } catch {
    entries = [];
  }
  for (const entry of entries) {
    if (!entry.isDirectory()) continue;
    try {
      const cap = JSON.parse(fs.readFileSync(path.join(capDir, entry.name, 'capability.json'), 'utf8'));
      if (cap && cap.role === 'runtime' && typeof cap.id === 'string') ids.add(cap.id);
    } catch {
      // Not a capability directory — skip.
    }
  }
  cachedIds = { root: repoRoot, ids };
  return ids;
}

function findRepoRoot(filename) {
  let dir = path.dirname(path.resolve(filename));
  for (let depth = 0; depth < 12; depth += 1) {
    if (fs.existsSync(path.join(dir, 'capabilities')) && fs.existsSync(path.join(dir, 'package.json'))) return dir;
    const parent = path.dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  return null;
}

function propertyName(member) {
  if (!member || member.type !== 'MemberExpression') return null;
  if (!member.computed && member.property && member.property.type === 'Identifier') return member.property.name;
  if (member.computed && member.property && member.property.type === 'Literal' && typeof member.property.value === 'string') {
    return member.property.value;
  }
  return null;
}

function nameOf(node) {
  if (!node) return null;
  if (node.type === 'Identifier') return node.name;
  if (node.type === 'MemberExpression') return propertyName(node);
  return null;
}

function isRuntimeIdentifierExpression(node) {
  if (!node) return false;
  if (node.type === 'ChainExpression') return isRuntimeIdentifierExpression(node.expression);
  if (node.type === 'TSNonNullExpression' || node.type === 'TSAsExpression') {
    return isRuntimeIdentifierExpression(node.expression);
  }
  const name = nameOf(node);
  return Boolean(name && RUNTIME_IDENTIFIER_RE.test(name));
}

/**
 * The install and hook surface the rule governs (#5169 scope: "install and hook
 * code"). Modules that implement the runtime→home/identity mapping itself —
 * `runtime-name-policy.cts` and `runtime-homes.cts` — are the owners and are
 * exempt: their id-keyed tables ARE the descriptor-accessor seam.
 *
 * `src/installer-migrations/**` is deliberately NOT governed: a shipped
 * migration body is immutable (editing it changes its checksum and surfaces
 * drift to every user who already applied it — #670, the committed baseline in
 * tests/installer-migrations.test.cjs), and a migration retiring one runtime's
 * artifacts names that runtime by design. It is historical record, not live
 * install logic that should read a descriptor.
 */
const INSTALL_SURFACE_SRC_RE =
  /(^|\/)src\/(install-[^/]*|runtime-artifact-[^/]*|surface|shell-command-projection|agent-install-check|runtime-hooks-surface|retired-artifact-cleanup)\.cts$/;

function isOwnedFile(filename) {
  const posix = String(filename || '').replace(/\\/g, '/');
  if (/(^|\/)src\/runtime-(name-policy|homes)\.cts$/.test(posix)) return false;
  if (INSTALL_SURFACE_SRC_RE.test(posix)) return true;
  if (/(^|\/)hooks\/.*\.js$/.test(posix)) return true;
  return /(^|\/)bin\/install\.js$/.test(posix);
}

/** @type {import('eslint').Rule.RuleModule} */
const rule = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Disallow comparing a runtime identifier to a registered runtime-id literal in install/hook code — declare a descriptor field and read it via hostBehaviorsFor() instead.',
      category: 'Best Practices',
    },
    schema: [{ type: 'object', properties: { runtimeIds: { type: 'array', items: { type: 'string' } } }, additionalProperties: false }],
    messages: {
      runtimeNameLiteral:
        "Runtime id '{{value}}' compared as a literal. The runtime descriptor owns runtime-specific facts: declare a hostBehaviors field in capabilities/{{value}}/capability.json and read it with hostBehaviorsFor(runtime) (#5169, ADR-5057 Phase 10).",
    },
  },

  create(context) {
    const filename = context.getFilename ? context.getFilename() : context.filename;
    if (!isOwnedFile(filename)) return {};

    const option = context.options && context.options[0];
    let ids;
    if (option && Array.isArray(option.runtimeIds)) {
      ids = new Set(option.runtimeIds);
    } else {
      const root = findRepoRoot(filename);
      ids = root ? loadRuntimeIds(root) : new Set(LEGACY_NON_REGISTRY_RUNTIME_IDS);
    }

    const isRuntimeIdLiteral = (node) =>
      Boolean(node && node.type === 'Literal' && typeof node.value === 'string' && ids.has(node.value));

    function report(literal) {
      context.report({ node: literal, messageId: 'runtimeNameLiteral', data: { value: literal.value } });
    }

    return {
      BinaryExpression(node) {
        if (!['===', '!==', '==', '!='].includes(node.operator)) return;
        if (isRuntimeIdLiteral(node.right) && isRuntimeIdentifierExpression(node.left)) report(node.right);
        else if (isRuntimeIdLiteral(node.left) && isRuntimeIdentifierExpression(node.right)) report(node.left);
      },
      SwitchStatement(node) {
        if (!isRuntimeIdentifierExpression(node.discriminant)) return;
        for (const switchCase of node.cases) {
          if (isRuntimeIdLiteral(switchCase.test)) report(switchCase.test);
        }
      },
    };
  },
};

module.exports = rule;
module.exports.LEGACY_NON_REGISTRY_RUNTIME_IDS = LEGACY_NON_REGISTRY_RUNTIME_IDS;
module.exports.RUNTIME_IDENTIFIER_RE = RUNTIME_IDENTIFIER_RE;
