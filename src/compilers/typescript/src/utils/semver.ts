/**
 * Shared semantic versioning utilities.
 * Provides a regex and helper for validating SemVer strings.
 */

/**
 * Official semver regex (semver.org / GitHub).
 */
export const SEMVER_REGEX =
  /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$/;

/**
 * Returns true if the string is a valid semantic version.
 */
export function isValidSemver(version: string): boolean {
  return SEMVER_REGEX.test(version);
}
