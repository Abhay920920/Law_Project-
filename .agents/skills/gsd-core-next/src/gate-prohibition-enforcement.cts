/**
 * `check prohibition-enforcement` as a gate module (#5219, epic #5056, ADR-5057 §4 closing arm C): it
 * returns a `GateResult`; the command router formats it. Imports no io module and performs no direct
 * console/stdout/stderr write (ESLint-enforced).
 *
 * The deterministic test-tier prohibition PRODUCER (#1259, ADR-550 D5d): it locates the wired
 * mechanical check (node-test or lint-rule), proves it fails first, runs it, builds
 * `enforcementEvidence` and delivers the `dispositionForProhibition` result
 * (`runProhibitionEnforcement`, `prohibition-enforcement.cjs`). A producer, not a blocking gate: its
 * disposition is a delivered answer (an `advisory` verdict, never `block`), and the exit status
 * follows it through the seam like every other `check <verb>` (#5170, payload mode).
 *
 * No-throw contract: a throw anywhere is a non-blocking `unreadable` verdict whose payload is the
 * producer's fail-closed disposition (flagged, `unverified`, nothing located), never a crash and
 * never a silent green.
 *
 * Argv after the verb: `<request.json>` or `--json '<inline request>'`. A request is
 * `{ prohibition, check, mode? }`; one that is absent or does not parse is a usage failure.
 */

import { gateVerdict, gateUnreadable, gateUsageFailure, GATE_FAILURE_CODE } from './gate-verdict.cjs';
import type { GateResult } from './gate-verdict.cjs';
import { readTextEvidence, evidenceFound, evidenceFromError } from './gate-evidence.cjs';
import type { Evidence } from './gate-evidence.cjs';
import { runProhibitionEnforcement } from './prohibition-enforcement.cjs';
import type { CheckDescriptor, EnforcementResult } from './prohibition-enforcement.cjs';

interface ProhibitionRequest {
  prohibition: unknown;
  check: CheckDescriptor | null;
  mode?: string;
}

/** Parse the request document. A document that does not parse is typed evidence, never a swallowed `null`. */
function parseDocumentEvidence(text: string): Evidence<Record<string, unknown> | null> {
  try {
    return evidenceFound(JSON.parse(text) as Record<string, unknown> | null);
  } catch (err) {
    return evidenceFromError<Record<string, unknown> | null>(err, 'request');
  }
}

/**
 * Parse a `{ prohibition, check, mode }` request from a JSON file path or inline `--json` string.
 * Returns null when the request is absent, unreadable or does not parse (the caller surfaces a usage
 * failure, never a throw).
 */
function parseRequest(args: readonly string[]): ProhibitionRequest | null {
  const jsonFlagIdx = args.indexOf('--json');
  const inline = args[jsonFlagIdx + 1];
  let payload: string;
  if (jsonFlagIdx !== -1 && typeof inline === 'string') {
    payload = inline;
  } else if (typeof args[0] === 'string' && args[0]) {
    const read = readTextEvidence(args[0]);
    if (read.kind !== 'found') return null;
    payload = read.value;
  } else {
    return null;
  }
  const document = parseDocumentEvidence(payload);
  // A JSON `null` has no `check` / `prohibition` to read: it is no request, as an unparsable one is.
  if (document.kind !== 'found' || document.value === null) return null;
  const parsed = document.value;
  const checkRaw = parsed['check'];
  const check: CheckDescriptor | null = (checkRaw && typeof checkRaw === 'object')
    ? (checkRaw as CheckDescriptor)
    : null;
  const modeRaw = parsed['mode'];
  const mode = typeof modeRaw === 'string' ? modeRaw : undefined;
  return { prohibition: parsed['prohibition'] ?? null, check, ...(mode ? { mode } : {}) };
}

export function evaluateProhibitionEnforcementGate(input: {
  projectDir: string;
  args: readonly string[];
  env?: NodeJS.ProcessEnv;
}): GateResult {
  const req = parseRequest(input.args);
  if (!req) {
    return gateUsageFailure(
      GATE_FAILURE_CODE.SDK_MISSING_ARG,
      'prohibition-enforcement requires a JSON request: check prohibition-enforcement <request.json> | --json \'{"prohibition":{...},"check":{...}}\'',
    );
  }
  try {
    const result = runProhibitionEnforcement(req.prohibition, req.check, req.mode ? { mode: req.mode } : {});
    return gateVerdict('advisory', false, { ...result });
  } catch (err) {
    // The producer's own fail-closed shape (`EnforcementResult`: a `ProhibitionDisposition` plus the
    // located / kind / evidence provenance): typed, so a change to that shape fails the build here
    // instead of drifting silently.
    const failedClosed: EnforcementResult = {
      status: 'unverified',
      flagged: true,
      tier: null,
      reason: 'exception: ' + (err instanceof Error ? err.message : String(err)),
      located: false,
      kind: null,
      evidence: [],
      ...(req.mode ? { mode: req.mode } : {}),
    };
    return gateUnreadable(false, { ...failedClosed });
  }
}
