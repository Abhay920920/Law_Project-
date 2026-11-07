'use strict';

/**
 * Preload for cutover-equivalence arms that need a deterministic read failure
 * (a mode-bit trick is not portable: root bypasses 000). Loaded with
 * `node --require <this> gsd-tools.cjs ...`; makes fs.readFileSync throw
 * EACCES for any path ending with $GSD_CUTOVER_FAIL_READ_SUFFIX. Everything
 * else reads through untouched, and nothing is written to stdout.
 */

const fs = require('node:fs');

// The suffix is written with "/"; Windows spells the same path with "\".
const slashed = (p) => p.replace(/\\/g, '/');
const suffix = process.env.GSD_CUTOVER_FAIL_READ_SUFFIX && slashed(process.env.GSD_CUTOVER_FAIL_READ_SUFFIX);

if (suffix) {
  const realRead = fs.readFileSync;
  fs.readFileSync = function patchedReadFileSync(target, ...rest) {
    if (typeof target === 'string' && slashed(target).endsWith(suffix)) {
      const err = new Error('EACCES: simulated read failure');
      err.code = 'EACCES';
      throw err;
    }
    return realRead.call(this, target, ...rest);
  };
}
