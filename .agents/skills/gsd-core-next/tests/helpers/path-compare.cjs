'use strict';

/**
 * Separator- and realpath-insensitive comparison of payloads that embed a
 * temp-project path (#5139).
 *
 * A gate payload spells the project directory however the platform does: POSIX
 * `/var/...` or its `/private/var/...` realpath on macOS, `C:\Users\RUNNER~1\...`
 * (8.3 short temp alias, backslashes) on Windows, where the long spelling is
 * only reachable through `fs.realpathSync.native`. Tests build expected values
 * with forward slashes. Both sides are therefore rewritten through ONE
 * function: every spelling of the temp root becomes a token, and path
 * separators after it become `/`.
 *
 * Call `tempRootAliases(dir)` while `dir` still exists (realpath needs it).
 */

const fs = require('node:fs');

const { escapeRegex } = require('../../gsd-core/bin/lib/pattern.cjs');

/** Every spelling of `dir`: as given, realpath, native (long-name) realpath, macOS /private-stripped. Longest first. */
function tempRootAliases(dir) {
  const forms = new Set([dir]);
  for (const resolve of [fs.realpathSync, fs.realpathSync.native]) {
    try { forms.add(resolve(dir)); } catch { /* dir gone or unresolvable: keep the given form */ }
  }
  for (const form of [...forms]) {
    if (form.startsWith('/private/')) forms.add(form.slice('/private'.length));
  }
  return [...forms].sort((a, b) => b.length - a.length);
}

// Any run of separators (raw, or doubled by JSON escaping) matches one separator.
function aliasPattern(alias) {
  return alias.split(/[\\/]+/).map(escapeRegex).join('[\\\\/]+');
}

function buildMatcher(aliases, caseInsensitive) {
  const source = aliases.map((a) => `(?:${aliasPattern(a)})`).join('|');
  return new RegExp(`(?:${source})(?![\\w~-])`, caseInsensitive ? 'gi' : 'g');
}

function canonicalizeString(str, matcher, token) {
  let hit = false;
  matcher.lastIndex = 0;
  const replaced = str.replace(matcher, () => { hit = true; return token; });
  if (!hit) return str;
  const [head, ...tails] = replaced.split(token);
  return [head, ...tails.map((t) => t.replace(/\\+/g, '/'))].join(token);
}

function walk(value, matcher, token) {
  if (typeof value === 'string') return canonicalizeString(value, matcher, token);
  if (Array.isArray(value)) return value.map((v) => walk(v, matcher, token));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, walk(v, matcher, token)]));
  }
  return value;
}

function matcherFor(aliases, opts) {
  const caseInsensitive = opts.caseInsensitive !== undefined ? opts.caseInsensitive : process.platform === 'win32';
  return buildMatcher(aliases, caseInsensitive);
}

/** Deeply rewrite every string in `value`: temp-root spellings -> `token`, separators after it -> `/`. Key order is preserved. */
function canonicalizeTempPaths(value, aliases, opts = {}) {
  return walk(value, matcherFor(aliases, opts), opts.token || '<tmp>');
}

/**
 * Same rewrite for captured process output. JSON output is parsed, rewritten
 * and re-serialised in its own layout (so a JSON-escaped backslash path is
 * handled as a string value); anything else is rewritten as plain text.
 */
function canonicalizeTempText(text, aliases, opts = {}) {
  const token = opts.token || '<tmp>';
  const matcher = matcherFor(aliases, opts);
  let parsed;
  try { parsed = JSON.parse(text); } catch { parsed = undefined; }
  if (parsed !== null && typeof parsed === 'object') {
    const trailing = text.slice(text.trimEnd().length);
    const body = text.slice(0, text.length - trailing.length);
    for (const indent of [2, undefined]) {
      if (JSON.stringify(parsed, null, indent) === body) {
        return JSON.stringify(walk(parsed, matcher, token), null, indent) + trailing;
      }
    }
  }
  return canonicalizeString(text, matcher, token);
}

module.exports = { tempRootAliases, canonicalizeTempPaths, canonicalizeTempText };
