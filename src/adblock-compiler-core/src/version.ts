/**
 * Package identity constants for @bloqr/compiler-core.
 */
export const VERSION = '1.0.0';
export const PACKAGE_NAME = '@bloqr/compiler-core';
export const USER_AGENT = `${PACKAGE_NAME}/${VERSION}`;

export const PACKAGE_INFO = {
  name: PACKAGE_NAME,
  version: VERSION,
  userAgent: USER_AGENT,
} as const;
