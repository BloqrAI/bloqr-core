import { TransformationType } from '../types/index.ts';
import { RuleUtils } from '../utils/index.ts';
import { SyncTransformation } from './base/Transformation.ts';

/** Cap on how many individual conflicts are logged at debug level per run. */
const MAX_LOGGED_CONFLICTS = 20;

/**
 * Transformation that detects (but does not resolve) contradictory rule pairs —
 * an exception/allow rule and a blocking rule that target the exact same pattern.
 *
 * Grammar-independent: works identically for DNS/hosts-style network rules
 * (`@@||example.com^` vs `||example.com^`) and browser-syntax cosmetic rules
 * (`example.com#@#.ad` vs `example.com##.ad`), since both grammars use the same
 * `@@`/`#@#` exception-marker convention.
 *
 * Detection-only by design: the consuming engine (DNS resolver or browser
 * extension) already gives an exception rule precedence over a conflicting
 * blocking rule, so silently removing either side here would be a policy
 * decision this transformation doesn't own — it surfaces the conflict via
 * logging so list authors can decide whether the redundant blocking rule
 * should be removed at the source. The rule set returned is always identical
 * to the one passed in.
 */
export class ConflictDetectionTransformation extends SyncTransformation {
  /** The transformation type identifier */
  public readonly type: TransformationType = TransformationType.ConflictDetection;
  /** Human-readable name of the transformation */
  public readonly name = 'ConflictDetection';

  /**
   * Detects exact-match allow/block conflicts and logs them.
   * @param rules - Array of rules to check
   * @returns The same rules, unmodified
   */
  public executeSync(rules: readonly string[]): readonly string[] {
    // Normalized (trimmed) form of every rule, so classification and lookup work
    // correctly even when this transformation runs without TrimLines - a caller
    // can request ConflictDetection alone, and canonical order only orders
    // transformations that were actually requested together.
    const normalizedSet = new Set(rules.map((rule) => rule.trim()));
    const conflicts: string[] = [];

    for (const rule of rules) {
      const trimmedRule = rule.trim();
      if (RuleUtils.isComment(trimmedRule) || RuleUtils.isEmpty(trimmedRule)) continue;

      const blockingForm = ConflictDetectionTransformation.toBlockingForm(trimmedRule);
      if (
        blockingForm !== null && blockingForm !== trimmedRule && normalizedSet.has(blockingForm)
      ) {
        conflicts.push(`'${rule}' conflicts with '${blockingForm}'`);
      }
    }

    if (conflicts.length > 0) {
      this.warn(`Detected ${conflicts.length} conflicting allow/block rule pair(s)`);
      for (const conflict of conflicts.slice(0, MAX_LOGGED_CONFLICTS)) {
        this.debug(conflict);
      }
      if (conflicts.length > MAX_LOGGED_CONFLICTS) {
        this.debug(`... and ${conflicts.length - MAX_LOGGED_CONFLICTS} more`);
      }
    } else {
      this.debug('No conflicting rules detected');
    }

    return rules;
  }

  /**
   * Returns the blocking-rule text an exception rule would conflict with, or
   * `null` if the rule isn't an exception rule (nothing to check).
   *
   * Checks the explicit `@@` network-exception prefix before falling back to the
   * substring-based cosmetic marker check: {@link RuleUtils.isCosmeticRule} matches
   * `#@#` *anywhere* in the rule, so a network rule (e.g. a regex rule delimited by
   * `/…/`) that merely contains that substring in its pattern - not as a cosmetic
   * marker - would otherwise be misclassified as cosmetic instead of as the network
   * exception it actually is.
   * @param trimmedRule - A rule with leading/trailing whitespace already removed.
   */
  private static toBlockingForm(trimmedRule: string): string | null {
    if (RuleUtils.isAllowRule(trimmedRule)) {
      return trimmedRule.slice(2);
    }
    if (RuleUtils.isCosmeticRule(trimmedRule) && trimmedRule.includes('#@#')) {
      return trimmedRule.replace('#@#', '##');
    }
    return null;
  }
}
