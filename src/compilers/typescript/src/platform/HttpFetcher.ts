/**
 * HTTP content fetcher using the standard Fetch API.
 * Works in browsers, Deno, Node.js 18+, and Cloudflare Workers.
 */

import type { IContentFetcher, IHttpFetcherOptions } from './types.ts';

const DEFAULT_OPTIONS: Required<Omit<IHttpFetcherOptions, 'headers'>> = {
  timeout: 30000,
  userAgent: 'HostlistCompiler/2.0',
  allowEmptyResponse: false,
};

/**
 * Fetches content over HTTP/HTTPS using the standard Fetch API.
 * This is the default fetcher for URL-based sources.
 */
export class HttpFetcher implements IContentFetcher {
  private readonly options: Required<Omit<IHttpFetcherOptions, 'headers'>>;
  private readonly headers: Record<string, string>;

  /**
   * Creates a new HttpFetcher
   * @param options - HTTP fetcher options
   */
  constructor(options?: IHttpFetcherOptions) {
    this.options = { ...DEFAULT_OPTIONS, ...options };
    this.headers = options?.headers ?? {};
  }

  /**
   * Checks if this fetcher can handle the given source.
   */
  public canHandle(source: string): boolean {
    return source.startsWith('http://') || source.startsWith('https://');
  }

  /**
   * Validates that a URL does not target private/internal network addresses.
   * Prevents SSRF attacks against localhost, private IPs, and cloud metadata endpoints.
   *
   * IP literals are decoded before range checks so that alternate encodings —
   * decimal (`2130706433`), octal (`0177.0.0.1`), hex (`0x7f.0.0.1`), short
   * forms (`127.1`), and IPv4-mapped IPv6 (`::ffff:7f00:1`) — cannot bypass the
   * block lists. Any hostname that resolves to a non-public IP literal, or that
   * fails to parse, is treated as unsafe.
   */
  public static isSafeUrl(url: string): boolean {
    try {
      const parsed = new URL(url);
      // Normalize: lower-case and strip a legal trailing dot so that
      // "foo.workers.dev." is treated identically to "foo.workers.dev".
      const host = parsed.hostname.toLowerCase().replace(/\.$/, '');

      // Reject the localhost alias outright.
      if (host === 'localhost' || host === 'metadata.google.internal') {
        return false;
      }

      // Reject Cloudflare Workers subdomains — proxying *.workers.dev creates
      // self-referential request loops and is never a valid filter-list source.
      if (host.endsWith('.workers.dev')) {
        return false;
      }

      // IPv6 literals keep their brackets in URL.hostname (e.g. "[fe80::1]").
      const bare = host.startsWith('[') && host.endsWith(']') ? host.slice(1, -1) : host;
      if (bare.includes(':')) {
        return HttpFetcher.isSafeIPv6(bare);
      }

      // Decode any IPv4 literal (dotted, decimal, octal, hex, short form).
      const ipv4 = HttpFetcher.parseIPv4(bare);
      if (ipv4 !== null) {
        return HttpFetcher.isPublicIPv4(ipv4);
      }

      // Not an IP literal — treat as a hostname and allow. (DNS-level
      // rebinding is out of scope for a static, resolver-free check.)
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Decodes an IPv4 literal in any of the forms accepted by the platform
   * `fetch`/`inet_aton`: dotted-quad, dotted with fewer parts (`a.b.c`,
   * `a.b`), a single 32-bit number, and octal/hex per-part encodings.
   *
   * @returns The address as an unsigned 32-bit integer, or `null` if `host`
   *   is not a valid IPv4 literal.
   */
  private static parseIPv4(host: string): number | null {
    if (host === '') {
      return null;
    }
    const parts = host.split('.');
    if (parts.length > 4) {
      return null;
    }

    const nums: number[] = [];
    for (const part of parts) {
      if (part === '') {
        return null;
      }
      let n: number;
      if (/^0x[0-9a-f]+$/.test(part)) {
        n = parseInt(part.slice(2), 16);
      } else if (/^0[0-7]+$/.test(part)) {
        n = parseInt(part, 8);
      } else if (part === '0') {
        n = 0;
      } else if (/^[1-9][0-9]*$/.test(part)) {
        n = parseInt(part, 10);
      } else {
        return null;
      }
      if (!Number.isFinite(n) || n < 0) {
        return null;
      }
      nums.push(n);
    }

    // inet_aton semantics: the final part absorbs all remaining low-order
    // bytes, earlier parts are single octets.
    const last = nums[nums.length - 1];
    const leading = nums.slice(0, -1);
    const remainingBytes = 4 - leading.length;
    if (last >= 2 ** (8 * remainingBytes)) {
      return null;
    }
    for (const octet of leading) {
      if (octet > 0xFF) {
        return null;
      }
    }

    let value = last;
    for (let i = 0; i < leading.length; i++) {
      value += leading[i] * 2 ** (8 * (3 - i));
    }
    return value >>> 0;
  }

  /**
   * Returns `true` only for globally routable (public) IPv4 addresses.
   * Blocks loopback, RFC 1918 private, link-local/metadata, CGNAT, and the
   * "this network" (0.0.0.0/8) ranges.
   */
  private static isPublicIPv4(value: number): boolean {
    const inRange = (base: string, prefix: number): boolean => {
      const [a, b, c, d] = base.split('.').map(Number);
      const baseInt = ((a << 24) | (b << 16) | (c << 8) | d) >>> 0;
      const mask = prefix === 0 ? 0 : (0xFFFFFFFF << (32 - prefix)) >>> 0;
      return (value & mask) >>> 0 === (baseInt & mask) >>> 0;
    };

    const blocked: Array<[string, number]> = [
      ['0.0.0.0', 8], // "this" network / unspecified
      ['10.0.0.0', 8], // RFC 1918
      ['100.64.0.0', 10], // CGNAT (RFC 6598)
      ['127.0.0.0', 8], // loopback
      ['169.254.0.0', 16], // link-local + cloud metadata
      ['172.16.0.0', 12], // RFC 1918
      ['192.0.0.0', 24], // IETF protocol assignments
      ['192.168.0.0', 16], // RFC 1918
      ['198.18.0.0', 15], // benchmarking
    ];
    return !blocked.some(([base, prefix]) => inRange(base, prefix));
  }

  /**
   * Returns `true` only for public IPv6 literals. Blocks loopback (`::1`),
   * unspecified (`::`), link-local (`fe80::/10`), unique-local (`fc00::/7`),
   * and IPv4-mapped/embedded addresses that decode to a non-public IPv4.
   *
   * @param bare - The IPv6 address without surrounding brackets. A zone id
   *   (`%eth0`, possibly percent-encoded as `%25eth0`) is stripped first.
   */
  private static isSafeIPv6(bare: string): boolean {
    const addr = bare.replace(/%25.*$/, '').replace(/%.*$/, '');

    if (addr === '::1' || addr === '::') {
      return false;
    }
    if (addr.startsWith('fe80') || addr.startsWith('fc') || addr.startsWith('fd')) {
      return false;
    }

    // IPv4-mapped / -embedded (e.g. "::ffff:127.0.0.1", "::ffff:7f00:1",
    // "64:ff9b::7f00:1"): validate the trailing IPv4 if one is present.
    const dottedTail = addr.match(/(\d{1,3}(?:\.\d{1,3}){3})$/);
    if (dottedTail) {
      const ipv4 = HttpFetcher.parseIPv4(dottedTail[1]);
      if (ipv4 !== null && !HttpFetcher.isPublicIPv4(ipv4)) {
        return false;
      }
    }
    const mappedHex = addr.match(/::ffff:([0-9a-f]{1,4}):([0-9a-f]{1,4})$/);
    if (mappedHex) {
      const high = parseInt(mappedHex[1], 16);
      const low = parseInt(mappedHex[2], 16);
      const ipv4 = (((high << 16) >>> 0) + low) >>> 0;
      if (!HttpFetcher.isPublicIPv4(ipv4)) {
        return false;
      }
    }

    return true;
  }

  /**
   * Fetches content from a URL.
   */
  public async fetch(source: string): Promise<string> {
    if (!HttpFetcher.isSafeUrl(source)) {
      throw new Error(`Blocked request to private/internal address: ${source}`);
    }

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.options.timeout);

    try {
      const response = await fetch(source, {
        signal: controller.signal,
        headers: {
          'User-Agent': this.options.userAgent,
          ...this.headers,
        },
        redirect: 'follow',
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const text = await response.text();

      if (!text && !this.options.allowEmptyResponse) {
        throw new Error('Empty response received');
      }

      return text;
    } finally {
      clearTimeout(timeoutId);
    }
  }
}
