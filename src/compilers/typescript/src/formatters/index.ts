/**
 * Output formatters module
 */

export {
  AdblockFormatter,
  BaseFormatter,
  createFormatter,
  DnsmasqFormatter,
  DoHFormatter,
  formatOutput,
  HostnameListFormatter,
  HostsFormatter,
  JsonFormatter,
  PiHoleFormatter,
  UnboundFormatter,
} from './OutputFormatter.ts';

export type { FormatterConstructor, FormatterOptions, FormatterResult } from './OutputFormatter.ts';
