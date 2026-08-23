/**
 * Package identity constants for @bloqr/compiler-core.
 */
/** The published version of this package, kept in sync with `deno.json`. */
export const VERSION = '1.3.0';

/** The fully-scoped JSR package name, e.g. for use in User-Agent strings or log output. */
export const PACKAGE_NAME = '@bloqr/compiler-core';

/** A `name/version` User-Agent string identifying this package, suitable for HTTP requests. */
export const USER_AGENT: string = `${PACKAGE_NAME}/${VERSION}`;

/** Convenience bundle of {@linkcode PACKAGE_NAME}, {@linkcode VERSION}, and {@linkcode USER_AGENT}. */
export const PACKAGE_INFO = {
  name: PACKAGE_NAME,
  version: VERSION,
  userAgent: USER_AGENT,
} as const;
