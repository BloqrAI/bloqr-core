import { TransformationType } from '../types/index.ts';
import { RuleUtils } from '../utils/index.ts';
import { SyncTransformation } from './base/Transformation.ts';

/**
 * Matches a plain element-hiding cosmetic rule: `<domains>##<selector>`.
 *
 * Deliberately narrower than {@link RuleUtils.isCosmeticRule}: it requires the
 * literal two-character `##` marker with nothing between the hashes, which excludes
 * exception rules (`#@#`) and every other AdGuard cosmetic marker (`#$#`, `#%#`,
 * `#?#`, …) — domains never contain `#`, so this can't misfire on those.
 */
const PLAIN_HIDING_RULE_REGEX = /^([^#]*)##(.+)$/;

/**
 * A uBlock/AdGuard scriptlet-injection rule (`##+js(...)`) reuses the plain `##`
 * marker but its "selector" is a function call, not a CSS selector — combining two
 * scriptlet calls with a comma is not valid combined syntax, unlike CSS selectors.
 * Rules matching this must never be merged.
 */
const SCRIPTLET_SELECTOR_REGEX = /^\+js\(/;

/** Accumulated state for one domain-list's merge group while scanning. */
interface MergeGroup {
  /** Selector strings to join, in first-seen order, deduplicated by trimmed text. */
  selectors: string[];
  /** Trimmed selector text already seen, for O(1) duplicate checks. */
  seen: Set<string>;
  /** Total source rules folded into this group (including duplicates). */
  occurrences: number;
}

/**
 * Transformation that merges structurally-equivalent, redundant rules into a
 * smaller rule set without changing the effective ruleset.
 *
 * v1 scope: merges consecutive-by-domain plain element-hiding rules
 * (`domain##selectorA` + `domain##selectorB` → `domain##selectorA, selectorB`) that
 * share an identical domain list. This is safe and grammar-independent — combining
 * CSS selectors with a comma selects the union of both, the same real-world
 * optimization AdGuard/uBlock Origin compilers already perform — and doesn't depend
 * on which engine (DNS vs. browser) consumes the rule, since only browser-syntax
 * cosmetic rules match the pattern at all. Scriptlet-injection rules (`##+js(...)`)
 * are explicitly excluded (see {@link SCRIPTLET_SELECTOR_REGEX}) since their
 * "selector" is a function call, not a combinable CSS selector.
 *
 * Everything else (network rules, exception rules, non-plain cosmetic markers,
 * comments) passes through unchanged. Only exact-identical domain-list strings are
 * merged — domain lists that are equivalent but written differently (different
 * order, whitespace) are deliberately left alone rather than risk a subtly wrong
 * equivalence judgement.
 */
export class RuleOptimizerTransformation extends SyncTransformation {
  /** The transformation type identifier */
  public readonly type: TransformationType = TransformationType.RuleOptimizer;
  /** Human-readable name of the transformation */
  public readonly name = 'RuleOptimizer';

  /**
   * Merges mergeable element-hiding rules that share a domain list.
   *
   * Runs in O(n) total: each domain's selector list/seen-set is accumulated
   * incrementally while scanning (never re-parsed from an already-merged
   * string), and every group's final rule text is materialized exactly once
   * at the end - a domain with `k` occurrences costs O(k), not O(k²).
   * @param rules - Array of rules to optimize
   * @returns Array with mergeable rules combined
   */
  public executeSync(rules: readonly string[]): readonly string[] {
    const groups = new Map<string, MergeGroup>();
    // Each entry is either a passthrough rule, or a marker for a merge group -
    // resolved to that group's final merged rule text in the second pass.
    const output: Array<string | { domains: string }> = [];

    for (const rule of rules) {
      const match = RuleOptimizerTransformation.matchMergeableRule(rule);
      if (!match) {
        output.push(rule);
        continue;
      }

      const [domains, selector] = match;
      let group = groups.get(domains);
      if (!group) {
        group = { selectors: [], seen: new Set(), occurrences: 0 };
        groups.set(domains, group);
        output.push({ domains });
      }

      group.occurrences += 1;
      const trimmedSelector = selector.trim();
      if (!group.seen.has(trimmedSelector)) {
        group.seen.add(trimmedSelector);
        group.selectors.push(selector);
      }
    }

    let mergedCount = 0;
    const result = output.map((entry) => {
      if (typeof entry === 'string') return entry;
      const group = groups.get(entry.domains)!;
      mergedCount += group.occurrences - 1;
      return `${entry.domains}##${group.selectors.join(', ')}`;
    });

    if (mergedCount > 0) {
      this.info(`Merged ${mergedCount} redundant element-hiding rule(s) into shared selectors`);
    } else {
      this.debug('No mergeable rules found');
    }

    return result;
  }

  /**
   * Returns `[domains, selector]` when `rule` is a mergeable plain element-hiding
   * rule, or `null` otherwise.
   */
  private static matchMergeableRule(rule: string): [string, string] | null {
    if (RuleUtils.isComment(rule) || RuleUtils.isEmpty(rule)) return null;

    const match = PLAIN_HIDING_RULE_REGEX.exec(rule);
    if (!match) return null;

    const [, domains, selector] = match;
    if (SCRIPTLET_SELECTOR_REGEX.test(selector)) return null;

    return [domains, selector];
  }
}
