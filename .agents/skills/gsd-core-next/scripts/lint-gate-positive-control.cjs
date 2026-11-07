#!/usr/bin/env node
'use strict';

/**
 * #5204 (epic #5056, ADR-5057 §4 ratchet) — every gate module has a positive control.
 *
 * "A gate module with no test that drives it to its failing verdict is a lint failure." A gate is a
 * module under `src/gate-*.cts` that exports an `evaluate*` function declared to return `GateResult`
 * (a function declaration, a `const` arrow/function expression, or an `export { … }` re-export). It is
 * DISCOVERED, never listed by name, so a new gate is covered without editing this guard. (The verb
 * entries outside gate modules, `phase uat-passed` and `verify artifacts`, are covered by the exit
 * guard `lint-gate-evidence-drift.cjs`, not by this ratchet.)
 *
 * Each gate needs exactly one `gateControl({ gate, module, fn, red, expectRed, redScenario,
 * greenScenario })` call (tests/helpers/gate-positive-control.cjs) as a TOP-LEVEL statement of a
 * `tests/**\/*.test.cjs` file, with `gateControl` bound from that helper. At run time the control drives
 * the gate to its failing verdict and to a different one. This guard parses the sources with
 * `@typescript-eslint/parser` (a real AST, no regex over source) and reports:
 *
 *   no-control          a gate with no (well-formed) `gateControl` call
 *   duplicate-control   a gate with more than one (which one proves it is ambiguous)
 *   wrong-red           the control's `red` is not the failing verdict the gate can reach. Derived
 *                       from the gate's own source, from the exported `evaluate*` and the same-file
 *                       functions it reaches: `block` when some `gateVerdict`/`gateUnreadable` call's
 *                       block argument is not the literal `false`, else `unreadable`. A control cannot
 *                       declare `unreadable` to dodge a blocking arm
 *   no-failing-verdict  a gate that can neither block nor reach `unreadable`: nothing to drive red
 *   wrong-module        the control's `module` does not resolve to `gsd-core/bin/lib/gate-<id>.cjs`
 *   wrong-fn            the control's `fn` is not the gate's exported `evaluate*`
 *   malformed-control   a `gateControl` call whose `gate`/`fn`/`red`/`module` is not a literal the
 *                       guard can read, that is not a top-level statement, or whose `gateControl` is
 *                       not the helper's export (an inert call must not count as a control)
 *   orphan-control      a control naming a gate that does not exist
 *   unclassified-evaluate  an exported `evaluate*` in a gate file that does not declare a `GateResult`
 *                       return (or cannot be resolved), or a gate file with an `export default`,
 *                       an `export * from`, or an `evaluate*` class/object member returning
 *                       `GateResult`: shapes the guard does not read, so the gate must be rewritten as
 *                       one of the shapes it does
 *   multiple-evaluates  a gate file exporting more than one `GateResult` `evaluate*` (one gate module,
 *                       one gate: the second would have no control naming it)
 *
 * The allowlist (`ALLOWLIST`) is EMPTY and stays that way (ADR-5057 §4: "drained to zero, never
 * renewed"); an entry that matches nothing is itself a problem. Fail-closed: scanning zero gates, a
 * `gate-*.cts` file whose name the guard cannot read, or a source the parser cannot read, is a
 * violation — an inert scan must not report a clean tree. `census(root)` re-measures the tree and is
 * asserted zero by tests/lint-gate-positive-control.test.cjs, which also holds the positive controls
 * for this guard (an inline gate/control pair per rule).
 */

const fs = require('node:fs');
const path = require('node:path');
const { runMain } = require('./lib/cli-exit.cjs');

const REPO_ROOT = path.join(__dirname, '..');
const CONTROL_CALL = 'gateControl';
const HELPER_PATH = 'tests/helpers/gate-positive-control.cjs';
const GATE_LIB_DIR = 'gsd-core/bin/lib';

/** Empty by decision (ADR-5057 §4): a gate without a control is fixed, never listed. */
const ALLOWLIST = Object.freeze([]);

const RULES = Object.freeze({
  NO_CONTROL: 'no-control',
  DUPLICATE_CONTROL: 'duplicate-control',
  WRONG_RED: 'wrong-red',
  NO_FAILING_VERDICT: 'no-failing-verdict',
  WRONG_MODULE: 'wrong-module',
  WRONG_FN: 'wrong-fn',
  MALFORMED_CONTROL: 'malformed-control',
  ORPHAN_CONTROL: 'orphan-control',
  UNCLASSIFIED_EVALUATE: 'unclassified-evaluate',
  MULTIPLE_EVALUATES: 'multiple-evaluates',
});

function loadParser(root) {
  return require(require.resolve('@typescript-eslint/parser', { paths: [root] }));
}

function isNode(value) {
  return value !== null && typeof value === 'object' && typeof value.type === 'string';
}

/** Depth-first walk; `visit(node, ancestors)`. */
function walk(node, visit, ancestors = []) {
  visit(node, ancestors);
  const next = ancestors.concat(node);
  for (const key of Object.keys(node)) {
    if (key === 'parent' || key === 'loc' || key === 'range' || key === 'tokens' || key === 'comments') continue;
    const value = node[key];
    if (Array.isArray(value)) {
      for (const child of value) if (isNode(child)) walk(child, visit, next);
    } else if (isNode(value)) {
      walk(value, visit, next);
    }
  }
}

function calleeName(call) {
  const callee = call.callee;
  if (callee.type === 'Identifier') return callee.name;
  if (callee.type === 'MemberExpression' && !callee.computed && callee.property.type === 'Identifier') return callee.property.name;
  return null;
}

function stringLiteral(node) {
  return node !== undefined && node !== null && node.type === 'Literal' && typeof node.value === 'string' ? node.value : null;
}

function parse(parser, text, file) {
  return parser.parse(text, { range: true, loc: true, sourceType: 'module', filePath: file });
}

function isFunctionExpressionNode(node) {
  return node !== null && node !== undefined && (node.type === 'ArrowFunctionExpression' || node.type === 'FunctionExpression');
}

// ─── gate discovery ──────────────────────────────────────────────────────────────────────────────

/** The `gate-<id>.cts` basename id, or null for a file that is not a readable gate file name. */
function gateIdOf(file) {
  const match = /(?:^|\/)gate-([a-z0-9-]+)\.cts$/.exec(file.replace(/\\/g, '/'));
  return match === null ? null : match[1];
}

/** Does a function node declare `: GateResult` as its return type? */
function returnsGateResult(fn) {
  const annotation = fn.returnType?.typeAnnotation;
  return annotation?.type === 'TSTypeReference' && annotation.typeName?.type === 'Identifier' && annotation.typeName.name === 'GateResult';
}

/** The file's top-level named functions: `Map<name, functionNode>` (declarations and `const f = () => …`). */
function topLevelFunctions(ast) {
  const defs = new Map();
  const add = (statement) => {
    if (statement.type === 'FunctionDeclaration' && statement.id !== null) defs.set(statement.id.name, statement);
    if (statement.type === 'VariableDeclaration') {
      for (const d of statement.declarations) {
        if (d.id.type === 'Identifier' && isFunctionExpressionNode(d.init)) defs.set(d.id.name, d.init);
      }
    }
  };
  for (const statement of ast.body) {
    add(statement);
    if (statement.type === 'ExportNamedDeclaration' && statement.declaration !== null) add(statement.declaration);
  }
  return defs;
}

/**
 * The exported `evaluate*` functions of a parsed gate file: `{ name, node, classified }`, where
 * `classified` means a resolved function that declares a `GateResult` return. Seen: `export function`,
 * `export const f = () => …`, and `export { f }` / `export { f as evaluateX }`.
 */
function exportedEvaluates(ast) {
  const defs = topLevelFunctions(ast);
  const found = [];
  for (const statement of ast.body) {
    if (statement.type !== 'ExportNamedDeclaration') continue;
    const declaration = statement.declaration;
    if (declaration !== null && declaration.type === 'FunctionDeclaration' && declaration.id !== null && declaration.id.name.startsWith('evaluate')) {
      found.push({ name: declaration.id.name, node: declaration, classified: returnsGateResult(declaration) });
    } else if (declaration !== null && declaration.type === 'VariableDeclaration') {
      for (const d of declaration.declarations) {
        if (d.id.type === 'Identifier' && d.id.name.startsWith('evaluate')) {
          const fn = isFunctionExpressionNode(d.init) ? d.init : null;
          found.push({ name: d.id.name, node: fn ?? d, classified: fn !== null && returnsGateResult(fn) });
        }
      }
    } else if (declaration === null) {
      for (const specifier of statement.specifiers) {
        const exported = specifier.exported.name ?? specifier.exported.value;
        if (typeof exported !== 'string' || !exported.startsWith('evaluate')) continue;
        const fn = defs.get(specifier.local.name) ?? null;
        found.push({ name: exported, node: fn ?? specifier, classified: fn !== null && returnsGateResult(fn) });
      }
    }
  }
  return found;
}

/**
 * Shapes through which a gate could export an evaluate the guard does not resolve: a default export, an
 * `export * from`, and an `evaluate*` method or object member. Each is reported as unclassified (a gate
 * in such a shape must be rewritten to a shape the guard reads), never skipped.
 */
function unresolvedEvaluateShapes(ast) {
  const found = [];
  walk(ast, (node) => {
    if (node.type === 'ExportDefaultDeclaration' || node.type === 'ExportAllDeclaration') found.push(node);
    // An `evaluate*` member that declares a GateResult return is a gate surface (a lookup table of
    // evaluators that return something else, or a destructuring, is not).
    if ((node.type === 'MethodDefinition' || node.type === 'Property' || node.type === 'PropertyDefinition') && !node.computed
      && node.key.type === 'Identifier' && node.key.name.startsWith('evaluate')
      && isFunctionExpressionNode(node.value) && returnsGateResult(node.value)) found.push(node);
  });
  return found;
}

/**
 * The failing verdict a gate can reach, read from the exported `evaluate*` and the same-file functions
 * it (transitively) calls — never from dead code elsewhere in the file. `block` when some
 * `gateVerdict(outcome, block, …)` / `gateUnreadable(block, …)` call's block argument is anything but the
 * literal `false`; `unreadable` when it only reaches the typed unreadable outcome; null for neither.
 * (An over-approximated `block` cannot bless a gate: the control must then block at run time.)
 */
function deriveRed(ast, evaluateNode) {
  const defs = topLevelFunctions(ast);
  const reached = new Set();
  const queue = [evaluateNode];
  let blocking = false;
  let unreadable = false;
  while (queue.length > 0) {
    const fn = queue.pop();
    if (reached.has(fn)) continue;
    reached.add(fn);
    walk(fn, (node) => {
      if (node.type !== 'CallExpression') return;
      const name = calleeName(node);
      if (name === 'gateVerdict' || name === 'gateUnreadable') {
        if (name === 'gateUnreadable') unreadable = true;
        const blockArg = node.arguments[name === 'gateVerdict' ? 1 : 0];
        if (blockArg !== undefined && !(blockArg.type === 'Literal' && blockArg.value === false)) blocking = true;
      } else if (node.callee.type === 'Identifier' && defs.has(name)) {
        queue.push(defs.get(name));
      }
    });
  }
  if (blocking) return 'block';
  return unreadable ? 'unreadable' : null;
}

// ─── control discovery ───────────────────────────────────────────────────────────────────────────

function isProcessTermination(node) {
  return node.type === 'CallExpression' && node.callee.type === 'MemberExpression' && !node.callee.computed
    && node.callee.object.type === 'Identifier' && node.callee.object.name === 'process'
    && node.callee.property.type === 'Identifier' && ['exit', 'abort', 'kill', 'reallyExit'].includes(node.callee.property.name);
}

/**
 * Why `gateControl` in this file is not the helper's live function; `[]` when it is. It must be bound by
 * exactly one top-level `const { gateControl } = require('…/tests/helpers/gate-positive-control.cjs')`
 * (resolved against the file, not matched by basename), never rebound, assigned or redeclared, and the
 * file must not terminate the process or throw at the top level (the call would never run).
 */
function gateControlBindingProblems(ast, file) {
  const problems = [];
  let helperBindings = 0;
  for (const statement of ast.body) {
    if (statement.type !== 'VariableDeclaration' || statement.kind !== 'const') continue;
    for (const d of statement.declarations) {
      if (d.id.type !== 'ObjectPattern' || d.init === null || d.init.type !== 'CallExpression') continue;
      if (d.init.callee.type !== 'Identifier' || d.init.callee.name !== 'require') continue;
      const required = stringLiteral(d.init.arguments[0]);
      if (required === null || resolvedModule(file, required) !== HELPER_PATH) continue;
      if (d.id.properties.some((p) => p.type === 'Property' && !p.computed && p.key.type === 'Identifier'
        && p.key.name === CONTROL_CALL && p.value.type === 'Identifier' && p.value.name === CONTROL_CALL)) helperBindings += 1;
    }
  }
  if (helperBindings !== 1) problems.push(`${CONTROL_CALL} is not bound by exactly one top-level const from require('…/${HELPER_PATH}')`);

  // Every other way of introducing or changing the name `gateControl`: a plain declarator, a function or
  // import of that name, a parameter, a second destructuring, and any assignment or update that targets it.
  const isName = (n) => n !== null && n !== undefined && n.type === 'Identifier' && n.name === CONTROL_CALL;
  const mentionsName = (n) => { let hit = false; walk(n, (x) => { if (isName(x)) hit = true; }); return hit; };
  let otherDeclarations = 0;
  let patternBindings = 0;
  let reassigned = false;
  let terminates = false;
  walk(ast, (node) => {
    if (node.type === 'VariableDeclarator') {
      if (isName(node.id)) otherDeclarations += 1;
      if (node.id.type === 'ObjectPattern' && node.id.properties.some((p) => p.type === 'Property' && isName(p.value))) patternBindings += 1;
    }
    if ((node.type === 'FunctionDeclaration' && isName(node.id)) || (node.type === 'ImportSpecifier' && isName(node.local))) otherDeclarations += 1;
    if ((node.type === 'FunctionDeclaration' || node.type === 'FunctionExpression' || node.type === 'ArrowFunctionExpression') && node.params.some(mentionsName)) otherDeclarations += 1;
    if (node.type === 'AssignmentExpression' && mentionsName(node.left)) reassigned = true;
    if (node.type === 'UpdateExpression' && isName(node.argument)) reassigned = true;
    if (isProcessTermination(node)) terminates = true;
  });
  if (patternBindings > 1 || otherDeclarations > 0) problems.push(`${CONTROL_CALL} is declared more than once in the file`);
  if (reassigned) problems.push(`${CONTROL_CALL} is reassigned`);
  if (terminates || ast.body.some((s) => s.type === 'ThrowStatement')) problems.push('the file terminates the process or throws at the top level, so a control may never run');
  return problems;
}

/** The literal property values of a `gateControl({...})` argument; a non-literal value reads as undefined. */
function readControl(call, file) {
  const spec = call.arguments[0];
  const control = { file, line: call.loc.start.line, gate: undefined, fn: undefined, red: undefined, modulePath: undefined, malformed: [] };
  if (spec === undefined || spec.type !== 'ObjectExpression') {
    control.malformed.push('argument is not an object literal');
    return control;
  }
  const property = (name) => spec.properties.find((p) => p.type === 'Property' && !p.computed
    && ((p.key.type === 'Identifier' && p.key.name === name) || stringLiteral(p.key) === name));
  for (const key of ['gate', 'fn', 'red']) {
    const prop = property(key);
    const value = prop === undefined ? null : stringLiteral(prop.value);
    if (value === null) control.malformed.push(`${key} is not a string literal`);
    else control[key] = value;
  }
  const expectProp = property('expectRed');
  if (expectProp === undefined || expectProp.value.type !== 'ObjectExpression' || expectProp.value.properties.length === 0) {
    control.malformed.push('expectRed is not a non-empty object literal naming the arm the red scenario reaches');
  }
  const moduleProp = property('module');
  const requireCall = moduleProp === undefined ? null : moduleProp.value;
  if (requireCall === null || requireCall.type !== 'CallExpression' || requireCall.callee.type !== 'Identifier'
    || requireCall.callee.name !== 'require' || stringLiteral(requireCall.arguments[0]) === null) {
    control.malformed.push('module is not require(<string literal>)');
  } else {
    control.modulePath = stringLiteral(requireCall.arguments[0]);
  }
  return control;
}

/** Every `gateControl(...)` call in one test source; a call that is not a real control is `malformed`. */
function scanControls(text, file, parser) {
  const ast = parse(parser, text, file);
  const bindingProblems = gateControlBindingProblems(ast, file);
  const topLevel = new Set(ast.body.filter((s) => s.type === 'ExpressionStatement').map((s) => s.expression));
  const controls = [];
  walk(ast, (node) => {
    if (node.type === 'CallExpression' && node.callee.type === 'Identifier' && node.callee.name === CONTROL_CALL) {
      const control = readControl(node, file);
      control.malformed.push(...bindingProblems);
      if (!topLevel.has(node)) control.malformed.push('call is not a top-level statement (it may never run)');
      controls.push(control);
    }
  });
  return controls;
}

// ─── scan ────────────────────────────────────────────────────────────────────────────────────────

/** The path a control's `module` resolves to, relative to the repo root, posix. */
function resolvedModule(controlFile, modulePath) {
  return path.posix.normalize(path.posix.join(path.posix.dirname(controlFile.replace(/\\/g, '/')), modulePath.replace(/\\/g, '/')));
}

/**
 * Scan in-memory sources. `gates`: `[{ file, text }]` for each `src/gate-*.cts`; `controls`:
 * `[{ file, text }]` for each test source that calls `gateControl`. Returns
 * `{ gates, controls, violations, allowlisted, problems }`.
 */
function scanSources({ gates: gateSources, controls: controlSources }, parser, { allowlist = ALLOWLIST } = {}) {
  const problems = [];
  const violations = [];
  const gates = [];

  for (const source of gateSources) {
    const id = gateIdOf(source.file);
    if (id === null) {
      if (/(?:^|\/)gate-[^/]*\.cts$/.test(source.file.replace(/\\/g, '/'))) {
        problems.push(`${source.file} is a gate file whose name is not gate-<a-z0-9-> .cts: it cannot be discovered, which must not report a clean tree`);
      }
      continue;
    }
    let ast;
    try {
      ast = parse(parser, source.text, source.file);
    } catch (error) {
      problems.push(`${source.file} could not be parsed (${error.message}): an unreadable gate host must not report a clean tree`);
      continue;
    }
    const evaluates = exportedEvaluates(ast);
    for (const e of evaluates.filter((x) => !x.classified)) {
      violations.push({ file: source.file, line: e.node.loc.start.line, rule: RULES.UNCLASSIFIED_EVALUATE, gate: id });
    }
    for (const node of unresolvedEvaluateShapes(ast)) {
      violations.push({ file: source.file, line: node.loc.start.line, rule: RULES.UNCLASSIFIED_EVALUATE, gate: id });
    }
    const classified = evaluates.filter((x) => x.classified);
    if (classified.length === 0) continue;
    // One gate module, one `evaluate*`: a second would be a gate no control names.
    for (const extra of classified.slice(1)) {
      violations.push({ file: source.file, line: extra.node.loc.start.line, rule: RULES.MULTIPLE_EVALUATES, gate: id });
    }
    const first = classified[0];
    gates.push({ id, file: source.file, fn: first.name, line: first.node.loc.start.line, red: deriveRed(ast, first.node) });
  }

  const controls = [];
  for (const source of controlSources) {
    try {
      controls.push(...scanControls(source.text, source.file, parser));
    } catch (error) {
      problems.push(`${source.file} could not be parsed (${error.message}): an unreadable control file must not report a clean tree`);
    }
  }

  if (gates.length === 0) problems.push('discovered zero gate modules (src/gate-*.cts exporting an evaluate* that returns GateResult): an inert scan must not report a clean tree');

  for (const control of controls) {
    for (const reason of control.malformed) {
      violations.push({ file: control.file, line: control.line, rule: RULES.MALFORMED_CONTROL, gate: control.gate ?? null, detail: reason });
    }
  }
  const wellFormed = controls.filter((c) => c.malformed.length === 0);

  for (const gate of gates) {
    const mine = wellFormed.filter((c) => c.gate === gate.id);
    if (gate.red === null) {
      violations.push({ file: gate.file, line: gate.line, rule: RULES.NO_FAILING_VERDICT, gate: gate.id });
    }
    if (mine.length === 0) {
      violations.push({ file: gate.file, line: gate.line, rule: RULES.NO_CONTROL, gate: gate.id });
      continue;
    }
    if (mine.length > 1) violations.push({ file: gate.file, line: gate.line, rule: RULES.DUPLICATE_CONTROL, gate: gate.id });
    for (const control of mine) {
      if (gate.red !== null && control.red !== gate.red) {
        violations.push({ file: control.file, line: control.line, rule: RULES.WRONG_RED, gate: gate.id, detail: `declares ${control.red}, the gate reaches ${gate.red}` });
      }
      const expectedModule = `${GATE_LIB_DIR}/gate-${gate.id}.cjs`;
      const actualModule = resolvedModule(control.file, control.modulePath);
      if (actualModule !== expectedModule) {
        violations.push({ file: control.file, line: control.line, rule: RULES.WRONG_MODULE, gate: gate.id, detail: `requires ${actualModule}, expected ${expectedModule}` });
      }
      if (control.fn !== gate.fn) {
        violations.push({ file: control.file, line: control.line, rule: RULES.WRONG_FN, gate: gate.id, detail: `drives ${control.fn}, the gate exports ${gate.fn}` });
      }
    }
  }
  const known = new Set(gates.map((g) => g.id));
  for (const control of wellFormed) {
    if (!known.has(control.gate)) violations.push({ file: control.file, line: control.line, rule: RULES.ORPHAN_CONTROL, gate: control.gate });
  }

  // The allowlist: a violation matching an entry on (gate, rule) is tolerated; an entry that matches
  // nothing is stale and is itself a problem.
  const kept = [];
  const allowlisted = [];
  const used = new Set();
  for (const v of violations) {
    const index = allowlist.findIndex((e) => e.gate === v.gate && e.rule === v.rule);
    if (index === -1) {
      kept.push(v);
    } else {
      used.add(index);
      allowlisted.push(v);
    }
  }
  allowlist.forEach((e, index) => {
    if (!used.has(index)) problems.push(`allowlist entry ${e.gate} [${e.rule}] matches no violation: remove the stale entry`);
  });

  return { gates, controls, violations: kept, allowlisted, problems };
}

// ─── repository walk ─────────────────────────────────────────────────────────────────────────────

function listGateFiles(root) {
  const dir = path.join(root, 'src');
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir).filter((name) => /^gate-.*\.cts$/.test(name)).sort().map((name) => `src/${name}`);
}

/** Every `*.test.cjs` under tests/ (not fixtures or node_modules): the files the test runner executes. */
function listControlFiles(root) {
  const out = [];
  const walkDir = (dir, rel) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      if (entry.name === 'fixtures' || entry.name === 'node_modules') continue;
      const childRel = rel === '' ? entry.name : `${rel}/${entry.name}`;
      if (entry.isDirectory()) walkDir(path.join(dir, entry.name), childRel);
      else if (entry.name.endsWith('.test.cjs')) out.push(childRel);
    }
  };
  const testsDir = path.join(root, 'tests');
  if (fs.existsSync(testsDir)) walkDir(testsDir, 'tests');
  return out.sort();
}

function scanRepo(root, parser = loadParser(root), options = {}) {
  const gates = listGateFiles(root).map((file) => ({ file, text: fs.readFileSync(path.join(root, file), 'utf8') }));
  const controls = [];
  for (const file of listControlFiles(root)) {
    const text = fs.readFileSync(path.join(root, file), 'utf8');
    // A cheap prefilter only: the AST decides what is a control.
    if (text.includes(`${CONTROL_CALL}(`)) controls.push({ file, text });
  }
  return scanSources({ gates, controls }, parser, options);
}

/** The census the phase publishes: every class of site this guard forbids, counted over the tree. */
function census(root = REPO_ROOT, parser = loadParser(root), options = {}) {
  const { gates, controls, violations, allowlisted, problems } = scanRepo(root, parser, options);
  const count = (rule) => violations.filter((v) => v.rule === rule).length;
  return {
    gates: gates.length,
    controls: controls.length,
    noControl: count(RULES.NO_CONTROL),
    duplicateControl: count(RULES.DUPLICATE_CONTROL),
    wrongRed: count(RULES.WRONG_RED),
    noFailingVerdict: count(RULES.NO_FAILING_VERDICT),
    wrongModule: count(RULES.WRONG_MODULE),
    wrongFn: count(RULES.WRONG_FN),
    malformedControl: count(RULES.MALFORMED_CONTROL),
    orphanControl: count(RULES.ORPHAN_CONTROL),
    unclassifiedEvaluate: count(RULES.UNCLASSIFIED_EVALUATE),
    multipleEvaluates: count(RULES.MULTIPLE_EVALUATES),
    allowlisted: allowlisted.length,
    total: violations.length,
    problems,
  };
}

function main() {
  const { gates, violations, problems } = scanRepo(REPO_ROOT);
  if (violations.length === 0 && problems.length === 0) {
    process.stdout.write(`ok gate-positive-control: ${gates.length} gate modules, each with a positive control\n`);
    return 0;
  }
  process.stderr.write('ERROR gate-positive-control: a gate module has no positive control that drives it to its failing verdict (ADR-5057 §4, #5204)\n');
  for (const v of violations) process.stderr.write(`  - ${v.file}:${v.line} [${v.rule}]${v.gate ? ` ${v.gate}` : ''}${v.detail ? `: ${v.detail}` : ''}\n`);
  for (const p of problems) process.stderr.write(`  - ${p}\n`);
  process.stderr.write('Add a gateControl({ gate, module, fn, red, expectRed, redScenario, greenScenario }) for it (tests/helpers/gate-positive-control.cjs, tests/gate-positive-control.test.cjs).\n');
  return 1;
}

if (require.main === module) runMain(main);

module.exports = { scanSources, scanRepo, census, loadParser, RULES, ALLOWLIST, gateIdOf, deriveRed, exportedEvaluates };
