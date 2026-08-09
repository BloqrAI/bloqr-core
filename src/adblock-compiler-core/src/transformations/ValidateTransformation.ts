/**
 * Validate Transformation - lightweight, dependency-free implementation.
 *
 * Validates adblock filter rules for DNS blockers using string/regex-based
 * rule classification (via {@link RuleUtils}), not a full AST parser.
 * Removes invalid rules and their preceding comments.
 *
 * Deliberately does not depend on @adguard/agtree — see bloqr-compiler#2200
 * for the companion effort to decouple AGTree from the commercial compiler's
 * core as well.
 */

import {
  type ILogger,
  type IValidationError,
  type IValidationReport,
  TransformationType,
  ValidationErrorType,
  ValidationSeverity,
} from '../types/index.ts';
import { StringUtils, TldUtils } from '../utils/index.ts';
import { RuleUtils } from '../utils/RuleUtils.ts';
import { SyncTransformation } from './base/Transformation.ts';

/**
 * Modifiers that can limit the rule for specific domains.
 */
const ANY_PATTERN_MODIFIER = ['denyallow', 'badfilter', 'client'];

/**
 * Modifiers supported by hosts-level blockers.
 */
const SUPPORTED_MODIFIERS = [
  'important',
  '~important',
  'ctag',
  'dnstype',
  'dnsrewrite',
  ...ANY_PATTERN_MODIFIER,
];

/**
 * Transformation that validates rules for DNS blockers.
 * Removes invalid rules and their preceding comments.
 */
export class ValidateTransformation extends SyncTransformation {
  /** The transformation type identifier */
  public readonly type: TransformationType;
  /** Human-readable name of the transformation */
  public readonly name: string;

  /** Whether IP addresses are allowed in rules */
  private readonly allowIp: boolean;

  /** Last validation report (from most recent executeSync call) */
  private lastValidationReport: IValidationReport | null = null;

  private currentSourceName?: string;

  /**
   * Creates a new `ValidateTransformation`.
   * @param allowIp When `true`, rules targeting bare IP addresses are treated as valid
   * (used by the `ValidateAllowIp` variant); when `false`, they are rejected.
   * @param logger Optional logger used to report validation warnings/errors.
   */
  constructor(allowIp = false, logger?: ILogger) {
    super(logger);
    this.allowIp = allowIp;
    this.type = allowIp ? TransformationType.ValidateAllowIp : TransformationType.Validate;
    this.name = allowIp ? 'ValidateAllowIp' : 'Validate';
  }

  /**
   * Sets the current source name, used for error attribution.
   */
  public setSourceName(sourceName: string | undefined): void {
    this.currentSourceName = sourceName;
  }

  /**
   * Get the validation report from the most recent executeSync call.
   */
  public getValidationReport(): IValidationReport | null {
    return this.lastValidationReport;
  }

  /**
   * Validates the given rules, removing invalid ones.
   *
   * @param rules - Array of rules to validate
   * @returns Array of valid rules
   */
  public executeSync(rules: readonly string[]): readonly string[] {
    const filtered = [...rules];
    const validationErrors: IValidationError[] = [];
    let previousRuleRemoved = false;
    const totalRules = rules.length;

    // Iterate from end to beginning to handle preceding comments
    for (let i = filtered.length - 1; i >= 0; i -= 1) {
      const ruleText = filtered[i];
      const isValidRule = this.isValidRule(ruleText, i + 1, validationErrors);
      const isCommentOrEmpty = StringUtils.isEmpty(ruleText.trim()) ||
        RuleUtils.isComment(ruleText);

      if (!isValidRule) {
        previousRuleRemoved = true;
        filtered.splice(i, 1);
      } else if (previousRuleRemoved && isCommentOrEmpty) {
        this.debug(`Removing a comment or empty line preceding an invalid rule: ${ruleText}`);
        filtered.splice(i, 1);
      } else {
        previousRuleRemoved = false;
      }
    }

    const validRules = filtered.length;
    this.lastValidationReport = {
      errorCount: validationErrors.filter((e) => e.severity === ValidationSeverity.Error).length,
      warningCount:
        validationErrors.filter((e) => e.severity === ValidationSeverity.Warning).length,
      infoCount: validationErrors.filter((e) => e.severity === ValidationSeverity.Info).length,
      errors: validationErrors,
      totalRules,
      validRules,
      invalidRules: totalRules - validRules,
    };

    return filtered;
  }

  /**
   * Add a validation error to the provided errors array
   */
  private addValidationError(
    errors: IValidationError[],
    type: ValidationErrorType,
    severity: ValidationSeverity,
    ruleText: string,
    message: string,
    details?: string,
    lineNumber?: number,
  ): void {
    errors.push({
      type,
      severity,
      ruleText,
      message,
      details,
      lineNumber,
      sourceName: this.currentSourceName,
    });
  }

  /**
   * Validates a single rule, returning whether it should be kept.
   */
  protected isValidRule(
    ruleText: string,
    lineNumber: number | undefined,
    errors: IValidationError[],
  ): boolean {
    // Empty or whitespace-only rules are valid
    if (StringUtils.isEmpty(ruleText.trim())) {
      return true;
    }

    // Comments are always valid
    if (RuleUtils.isComment(ruleText)) {
      return true;
    }

    // Cosmetic rules are not valid for DNS blockers
    if (RuleUtils.isCosmeticRule(ruleText)) {
      this.debug(`Cosmetic rules are not supported for DNS blocking: ${ruleText}`);
      this.addValidationError(
        errors,
        ValidationErrorType.CosmeticNotSupported,
        ValidationSeverity.Error,
        ruleText,
        'Cosmetic rules not supported',
        'DNS blockers do not support cosmetic/element hiding rules',
        lineNumber,
      );
      return false;
    }

    // /etc/hosts format rules
    if (RuleUtils.isEtcHostsRule(ruleText)) {
      return this.validateHostRule(ruleText, lineNumber, errors);
    }

    // Everything else is treated as an adblock-style network rule
    return this.validateNetworkRule(ruleText, lineNumber, errors);
  }

  /**
   * Validates a hostname for the blocklist.
   */
  protected validateHostname(
    hostname: string,
    ruleText: string,
    hasLimitModifier: boolean,
    lineNumber: number | undefined,
    errors: IValidationError[],
  ): boolean {
    const result = TldUtils.parse(hostname);

    if (!result.hostname) {
      this.debug(`Invalid hostname ${hostname} in the rule: ${ruleText}`);
      this.addValidationError(
        errors,
        ValidationErrorType.InvalidHostname,
        ValidationSeverity.Error,
        ruleText,
        `Invalid hostname: ${hostname}`,
        'The hostname format is not valid',
        lineNumber,
      );
      return false;
    }

    if (!this.allowIp && result.isIp) {
      this.debug(`IP addresses not allowed: ${hostname} in rule: ${ruleText}`);
      this.addValidationError(
        errors,
        ValidationErrorType.IpNotAllowed,
        ValidationSeverity.Error,
        ruleText,
        `IP address not allowed: ${hostname}`,
        'IP addresses are not permitted in this configuration',
        lineNumber,
      );
      return false;
    }

    if (result.hostname === result.publicSuffix && !hasLimitModifier) {
      this.debug(`Matching the whole public suffix ${hostname} is not allowed: ${ruleText}`);
      this.addValidationError(
        errors,
        ValidationErrorType.PublicSuffixMatch,
        ValidationSeverity.Error,
        ruleText,
        `Public suffix matching not allowed: ${hostname}`,
        'Matching entire public suffixes (like .com, .org) is too broad',
        lineNumber,
      );
      return false;
    }

    return true;
  }

  /**
   * Validates a /etc/hosts rule.
   */
  protected validateHostRule(
    ruleText: string,
    lineNumber: number | undefined,
    errors: IValidationError[],
  ): boolean {
    try {
      const props = RuleUtils.loadEtcHostsRuleProperties(ruleText);

      if (props.hostnames.length === 0) {
        this.info(`The rule has no hostnames: ${ruleText}`);
        return false;
      }

      for (const hostname of props.hostnames) {
        if (!this.validateHostname(hostname, ruleText, false, lineNumber, errors)) {
          return false;
        }
      }

      return true;
    } catch (ex) {
      this.error(`Unexpected error validating /etc/hosts rule: ${ruleText}: ${ex}`);
      return false;
    }
  }

  /**
   * Validates an adblock-style network rule.
   */
  protected validateNetworkRule(
    ruleText: string,
    lineNumber: number | undefined,
    errors: IValidationError[],
  ): boolean {
    try {
      const props = RuleUtils.loadAdblockRuleProperties(ruleText);
      let hasLimitModifier = false;

      if (props.options && props.options.length > 0) {
        for (const mod of props.options) {
          if (!SUPPORTED_MODIFIERS.includes(mod.name)) {
            this.debug(`Contains unsupported modifier ${mod.name}: ${ruleText}`);
            this.addValidationError(
              errors,
              ValidationErrorType.UnsupportedModifier,
              ValidationSeverity.Error,
              ruleText,
              `Unsupported modifier: ${mod.name}`,
              `Supported modifiers: ${SUPPORTED_MODIFIERS.join(', ')}`,
              lineNumber,
            );
            return false;
          }
          const bareName = mod.name.startsWith('~') ? mod.name.slice(1) : mod.name;
          if (ANY_PATTERN_MODIFIER.includes(bareName)) {
            hasLimitModifier = true;
          }
        }
      }

      const pattern = props.pattern;

      // Special case: regex rules are valid
      if (pattern.startsWith('/') && pattern.endsWith('/') && pattern.length > 1) {
        return true;
      }

      let toTest = pattern;
      if (toTest.startsWith('://')) {
        toTest = toTest.substring(3);
      }

      const hostname = props.hostname ?? RuleUtils.extractHostname(pattern);
      if (!hostname) {
        this.debug(`Could not extract a hostname from rule: ${ruleText}`);
        this.addValidationError(
          errors,
          ValidationErrorType.InvalidCharacters,
          ValidationSeverity.Error,
          ruleText,
          'Could not extract a hostname from the rule pattern',
          'DNS blockers require a hostname-based pattern (||domain^)',
          lineNumber,
        );
        return false;
      }

      return this.validateHostname(hostname, ruleText, hasLimitModifier, lineNumber, errors);
    } catch (ex) {
      this.error(`Unexpected error validating network rule: ${ruleText}: ${ex}`);
      return false;
    }
  }
}

/**
 * Convenience transformation that validates rules while allowing raw IP addresses.
 */
export class ValidateAllowIpTransformation extends SyncTransformation {
  /** Always `TransformationType.ValidateAllowIp`. */
  public readonly type: TransformationType = TransformationType.ValidateAllowIp;
  /** Always `'ValidateAllowIp'`. */
  public readonly name = 'ValidateAllowIp';

  private readonly validator: ValidateTransformation;

  /**
   * Creates a new `ValidateAllowIpTransformation`.
   * @param logger Optional logger used to report validation warnings/errors.
   */
  constructor(logger?: ILogger) {
    super(logger);
    this.validator = new ValidateTransformation(true, logger);
  }

  /**
   * Validates the given rules, removing invalid ones while allowing bare-IP hostnames.
   * @param rules Rules to validate.
   * @returns The subset of `rules` that passed validation.
   */
  public executeSync(rules: readonly string[]): readonly string[] {
    return this.validator.executeSync(rules);
  }
}
