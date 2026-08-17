/**
 * Platform abstraction layer for cross-runtime compatibility (Deno-native
 * fetchers). WorkerCompiler, BrowserFetcher, and FeatureFlagService are
 * Cloudflare Workers-specific and are not part of this core engine.
 */

export type {
  IContentFetcher,
  IHttpFetcherOptions,
  IPlatformCompilerOptions,
  PreFetchedContent,
} from './types.ts';

export { HttpFetcher } from './HttpFetcher.ts';
export { PreFetchedContentFetcher } from './PreFetchedContentFetcher.ts';
export { CompositeFetcher } from './CompositeFetcher.ts';
export { PlatformDownloader } from './PlatformDownloader.ts';
export type { PlatformDownloaderOptions } from './PlatformDownloader.ts';
