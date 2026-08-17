/**
 * Rule Utilities - lightweight, dependency-free implementation.
 *
 * Deliberately does NOT depend on @adguard/agtree or any full AST parser.
 * Uses string/regex-based rule classification, matching the semantics of
 * the fallback paths that a full AGTree-backed implementation would use
 * when a rule fails to parse. This keeps the core compiler engine free of
 * third-party AdGuard library coupling — see bloqr-compiler#2200 for the
 * companion effort to make AGTree an optional, swappable implementation
 * detail behind an interface rather than baked into the core there too.
 */

import type {
  IAdblockRule,
  IAdblockRuleTokens,
  IEtcHostsRule,
  IRuleModifier,
} from '../types/index.ts';

/**
 * Converts a domain to ASCII (Punycode) representation.
 * Uses the native URL API which handles IDN conversion.
 */
function domainToASCII(domain: string): string {
  try {
    const url = new URL(`http://${domain}`);
    return url.hostname;
  } catch {
    return domain;
  }
}

// Regular expressions for rule parsing
const DOMAIN_REGEX =
  /^(?=.{1,255}$)[0-9A-Za-z](?:(?:[0-9A-Za-z]|-){0,61}[0-9A-Za-z])?(?:\.[0-9A-Za-z](?:(?:[0-9A-Za-z]|-){0,61}[0-9A-Za-z])?)*\.?$/;
const DOMAIN_PATTERN_REGEX = /(\*\.|)([^\s^$|=]+(?:\.[^\s^$|=]+)+)/g;
// deno-lint-ignore no-control-regex
const NON_ASCII_REGEX = /[^\x00-\x7F]/;
const HOSTS_LINE_REGEX = /^\s*((?:\d{1,3}\.){3}\d{1,3}|::1|0*:0*:0*:0*:0*:0*:0*:0*1?)\s+(.+)$/;
const NETWORK_HOSTNAME_REGEX = /^\|\|([a-z0-9-.]+)\^?$/i;

/**
 * Utility class for working with filtering rules.
 * Pure string/regex based — no AST parsing, no third-party dependency.
 */
export class RuleUtils {
  /**
   * Checks if the rule is a comment.
   */
  public static isComment(ruleText: string): boolean {
    const trimmed = ruleText.trim();
    return (
      trimmed.startsWith('!') ||
      trimmed.startsWith('# ') ||
      trimmed === '#' ||
      trimmed.startsWith('####')
    );
  }

  /**
   * Checks if the rule is an allowing/exception rule.
   */
  public static isAllowRule(ruleText: string): boolean {
    return ruleText.trim().startsWith('@@');
  }

  /**
   * Checks if the rule is just a domain name.
   */
  public static isJustDomain(ruleText: string): boolean {
    return ruleText.includes('.') && DOMAIN_REGEX.test(ruleText);
  }

  /**
   * Checks if the rule is a /etc/hosts format rule (IP followed by one or more hostnames).
   */
  public static isEtcHostsRule(ruleText: string): boolean {
    return HOSTS_LINE_REGEX.test(ruleText.trim());
  }

  /**
   * Checks if the rule contains non-ASCII characters.
   */
  public static containsNonAsciiCharacters(ruleText: string): boolean {
    return NON_ASCII_REGEX.test(ruleText);
  }

  /**
   * Converts non-ASCII domain names to Punycode.
   */
  public static convertNonAsciiToPunycode(line: string): string {
    return line.replace(DOMAIN_PATTERN_REGEX, (match, wildcard: string, domain: string) => {
      if (RuleUtils.containsNonAsciiCharacters(domain)) {
        const punycodeDomain = domainToASCII(domain);
        return wildcard + punycodeDomain;
      }
      return match;
    });
  }

  /**
   * Parses a rule text into pattern/options/whitelist tokens.
   */
  public static parseRuleTokens(ruleText: string): IAdblockRuleTokens {
    const props = RuleUtils.loadAdblockRuleProperties(ruleText.trim());
    let options: string | null = null;
    if (props.options && props.options.length > 0) {
      options = props.options
        .map((opt) => (opt.value ? `${opt.name}=${opt.value}` : opt.name))
        .join(',');
    }

    return {
      pattern: props.pattern,
      options,
      whitelist: props.whitelist,
    };
  }

  /**
   * Extracts hostname from an adblock rule pattern (||domain^ format).
   */
  public static extractHostname(pattern: string): string | null {
    const match = pattern.match(NETWORK_HOSTNAME_REGEX);
    return match ? match[1] : null;
  }

  /**
   * Parses a /etc/hosts rule into its IP and hostnames.
   */
  public static loadEtcHostsRuleProperties(ruleText: string): IEtcHostsRule {
    const match = ruleText.trim().match(HOSTS_LINE_REGEX);
    if (!match) {
      throw new TypeError(`Invalid /etc/hosts rule: ${ruleText}`);
    }

    const hostnames = match[2]
      .split(/\s+/)
      .map((h) => h.trim())
      .filter((h) => h.length > 0 && !h.startsWith('#'));

    return {
      ruleText,
      hostnames,
    };
  }

  /**
   * Parses an adblock-style rule into its structural properties.
   * Splits on '$' for modifiers and '@@' prefix for whitelist status —
   * the same shape AdGuard-syntax network rules use.
   */
  public static loadAdblockRuleProperties(ruleText: string): IAdblockRule {
    const isWhitelist = ruleText.startsWith('@@');
    const withoutPrefix = isWhitelist ? ruleText.slice(2) : ruleText;

    const dollarIndex = withoutPrefix.indexOf('$');
    const pattern = dollarIndex === -1 ? withoutPrefix : withoutPrefix.slice(0, dollarIndex);
    const optionsStr = dollarIndex === -1 ? null : withoutPrefix.slice(dollarIndex + 1);

    let options: IRuleModifier[] | null = null;
    if (optionsStr) {
      options = optionsStr.split(',').map((opt) => {
        const eqIndex = opt.indexOf('=');
        if (eqIndex === -1) {
          return { name: opt.trim(), value: null };
        }
        return {
          name: opt.slice(0, eqIndex).trim(),
          value: opt.slice(eqIndex + 1).trim(),
        };
      });
    }

    const hostname = RuleUtils.extractHostname(pattern);

    return {
      ruleText,
      pattern,
      whitelist: isWhitelist,
      options,
      hostname,
    };
  }

  /**
   * Finds a modifier by name in the rule's options.
   */
  public static findModifier(ruleProps: IAdblockRule, name: string): IRuleModifier | null {
    if (!ruleProps.options) {
      return null;
    }

    for (const option of ruleProps.options) {
      if (option.name === name || option.name === `~${name}`) {
        return option;
      }
    }

    return null;
  }

  /**
   * Removes a modifier by name from the rule's options.
   */
  public static removeModifier(ruleProps: IAdblockRule, name: string): boolean {
    if (!ruleProps.options) {
      return false;
    }

    let found = false;
    for (let i = ruleProps.options.length - 1; i >= 0; i -= 1) {
      const option = ruleProps.options[i];
      if (option.name === name || option.name === `~${name}`) {
        ruleProps.options.splice(i, 1);
        found = true;
      }
    }

    return found;
  }

  /**
   * Converts an AdblockRule back to string representation.
   */
  public static adblockRuleToString(ruleProps: IAdblockRule): string {
    let ruleText = '';

    if (ruleProps.whitelist) {
      ruleText = '@@';
    }

    ruleText += ruleProps.pattern;

    if (ruleProps.options && ruleProps.options.length > 0) {
      ruleText += '$';

      for (let i = 0; i < ruleProps.options.length; i += 1) {
        const option = ruleProps.options[i];
        ruleText += option.name;

        if (option.value) {
          ruleText += '=';
          ruleText += option.value;
        }

        if (i < ruleProps.options.length - 1) {
          ruleText += ',';
        }
      }
    }

    return ruleText;
  }

  /**
   * Check if a rule is empty or whitespace only.
   */
  public static isEmpty(ruleText: string): boolean {
    return ruleText.trim() === '';
  }

  /**
   * Check if a rule is a cosmetic rule (element-hiding / scriptlet syntax).
   * Recognizes the common cosmetic markers without full AST parsing.
   */
  public static isCosmeticRule(ruleText: string): boolean {
    return /#[@$%?]{0,2}#/.test(ruleText);
  }

  /**
   * Check if a rule looks like a network (blocking) rule — i.e. not a
   * comment, not empty, not a cosmetic rule, and not a /etc/hosts rule.
   */
  public static isNetworkRule(ruleText: string): boolean {
    if (RuleUtils.isEmpty(ruleText) || RuleUtils.isComment(ruleText)) {
      return false;
    }
    if (RuleUtils.isCosmeticRule(ruleText)) {
      return false;
    }
    if (RuleUtils.isEtcHostsRule(ruleText)) {
      return false;
    }
    return true;
  }
}
