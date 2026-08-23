/**
 * Syntax detection for the multi-engine compiler: decides whether a source (or a
 * single line) belongs to the DNS/hosts grammar or the browser-syntax grammar.
 *
 * Pure, dependency-free, and safe to call on every source regardless of whether it
 * declares an explicit engine — used both to resolve auto-detection and to warn when
 * a declared engine disagrees with the sniffed one.
 */

import { type EngineKind, type ISource, SourceType } from '../types/index.ts';
import type { EngineDetectorOptions, GroupedSources, LineEngineSignal } from './types.ts';

const DEFAULT_SAMPLE_SIZE = 200;
const DEFAULT_FALLBACK: EngineKind = 'dns';

/** Cosmetic-rule separators from AdGuard's browser-syntax grammar (AGTree superset). */
const COSMETIC_SEPARATORS = ['#@#', '#?#', '#$#', '#@$#', '#%#', '#@%#', '##'] as const;

/** Hosts-file line: `0.0.0.0 example.com` / `127.0.0.1 example.com` / IPv6 form. */
const HOSTS_LINE_RE = /^(?:\d{1,3}\.){3}\d{1,3}\s+\S+|^::1?\s+\S+/;

/** Bare DNS-blocklist line: `example.com`, `||example.com^`, `@@||example.com^`. */
const DNS_ADBLOCK_LINE_RE = /^@{0,2}\|{0,2}[a-z0-9*][a-z0-9.*-]*\^?(\$[a-z0-9_,=~-]*)?$/i;

/**
 * Modifiers that only make sense on a browser-syntax network rule (URL/resource
 * filtering options a DNS resolver has no concept of).
 */
const BROWSER_ONLY_MODIFIERS = [
  'script',
  'stylesheet',
  'image',
  'object',
  'xmlhttprequest',
  'subdocument',
  'ping',
  'websocket',
  'webrtc',
  'document',
  'elemhide',
  'generichide',
  'genericblock',
  'jsinject',
  'popup',
  'csp',
  'removeparam',
  'redirect',
  'replace',
  'app',
  'cookie',
  'permissions',
];

/**
 * Classifies a single non-empty, non-comment line as a `dns` signal, a `browser`
 * signal, or `null` (no strong signal either way).
 */
export function classifyLine(rawLine: string): LineEngineSignal {
  const line = rawLine.trim();
  if (line.length === 0) return null;

  // Cosmetic/element-hiding/scriptlet rules are exclusively browser-syntax.
  for (const sep of COSMETIC_SEPARATORS) {
    if (line.includes(sep)) return 'browser';
  }

  // Hosts-file syntax is exclusively DNS.
  if (HOSTS_LINE_RE.test(line)) return 'dns';

  // Network rule with a `$` modifier list — inspect the modifiers.
  const dollarIndex = line.indexOf('$');
  if (
    dollarIndex !== -1 && (line.startsWith('||') || line.startsWith('@@') || line.startsWith('|'))
  ) {
    const modifiers = line.slice(dollarIndex + 1).toLowerCase();
    if (BROWSER_ONLY_MODIFIERS.some((mod) => modifiers.includes(mod))) {
      return 'browser';
    }
    // A bare domain rule with only DNS-relevant modifiers (e.g. `important`,
    // `dnstype`) reads as a DNS signal.
    return 'dns';
  }

  if (DNS_ADBLOCK_LINE_RE.test(line)) return 'dns';

  return null;
}

/**
 * Detects the engine for a source's already-fetched rule lines by sampling up to
 * `sampleSize` classifiable lines and taking a majority vote. Ties (including an
 * empty/all-null sample) resolve to `fallback`.
 */
export function detectEngineFromLines(
  lines: readonly string[],
  options: EngineDetectorOptions = {},
): EngineKind {
  const sampleSize = options.sampleSize ?? DEFAULT_SAMPLE_SIZE;
  const fallback = options.fallback ?? DEFAULT_FALLBACK;

  let dnsVotes = 0;
  let browserVotes = 0;
  let sampled = 0;

  for (const line of lines) {
    const trimmed = line.trim();
    if (
      trimmed.length === 0 ||
      (trimmed.startsWith('!') ||
        (trimmed.startsWith('#') && !COSMETIC_SEPARATORS.some((s) => trimmed.includes(s))))
    ) {
      continue;
    }

    const signal = classifyLine(trimmed);
    if (signal === 'dns') dnsVotes++;
    else if (signal === 'browser') browserVotes++;
    else continue;

    sampled++;
    if (sampled >= sampleSize) break;
  }

  if (dnsVotes === 0 && browserVotes === 0) return fallback;
  if (dnsVotes === browserVotes) return fallback;
  return browserVotes > dnsVotes ? 'browser' : 'dns';
}

/**
 * Resolves the engine for a source per the resolution order: explicit
 * `source.engine` > legacy `SourceType.Hosts` > content sniffing > configuration
 * default > `'dns'`.
 *
 * `lines` is optional — omit it (or pass an empty array) when only the declarative
 * signals (explicit engine / hosts type / config default) should be consulted, e.g.
 * before a source has been downloaded.
 */
export function detectSourceEngine(
  source: Pick<ISource, 'engine' | 'type'>,
  lines: readonly string[] = [],
  configDefaultEngine?: EngineKind,
  options: EngineDetectorOptions = {},
): EngineKind {
  if (source.engine) return source.engine;
  if (source.type === SourceType.Hosts) return 'dns';
  if (lines.length > 0) {
    return detectEngineFromLines(lines, {
      ...options,
      fallback: configDefaultEngine ?? options.fallback,
    });
  }
  return configDefaultEngine ?? options.fallback ?? DEFAULT_FALLBACK;
}

/**
 * Buckets sources by resolved engine, preserving relative order within each bucket.
 * Content sniffing is skipped here (declarative signals only) — callers that have
 * already downloaded source content should resolve per-source via
 * {@link detectSourceEngine} with `lines` before grouping.
 */
export function groupSourcesByEngine(
  sources: readonly ISource[],
  configDefaultEngine?: EngineKind,
  options: EngineDetectorOptions = {},
): GroupedSources {
  const grouped: GroupedSources = { dns: [], browser: [] };
  for (const source of sources) {
    const engine = detectSourceEngine(source, [], configDefaultEngine, options);
    grouped[engine].push(source);
  }
  return grouped;
}
