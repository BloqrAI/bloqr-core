/**
 * Chunking implementation for parallel compilation of large rule lists
 * Splits large sources into manageable chunks for parallel processing
 */

import type { IConfiguration, ISource } from '../index.ts';
import type { ChunkingConfig, Logger } from './types.ts';
import { logger as defaultLogger } from './logger.ts';

/**
 * Default chunking configuration
 */
export const DEFAULT_CHUNKING_CONFIG: Required<ChunkingConfig> = {
  enabled: false,
  chunkSize: 100000, // 100k rules per chunk
  maxParallel: navigator.hardwareConcurrency || 4,
  strategy: 'source',
};

/**
 * Chunk metadata
 */
export interface ChunkMetadata {
  /** Chunk index */
  index: number;
  /** Total number of chunks */
  total: number;
  /** Estimated rule count in this chunk */
  estimatedRules: number;
  /** Sources in this chunk */
  sources: ISource[];
}

/**
 * Chunked configuration
 */
export interface ChunkedConfiguration extends IConfiguration {
  /** Chunk metadata */
  _chunkMetadata?: ChunkMetadata;
}

/**
 * Determines if chunking should be enabled based on configuration and source count
 * @param config - Compiler configuration
 * @param chunkingConfig - Chunking configuration
 * @param logger - Logger instance
 * @returns Whether chunking should be enabled
 */
export function shouldEnableChunking(
  config: IConfiguration,
  chunkingConfig?: ChunkingConfig,
  logger: Logger = defaultLogger,
): boolean {
  const resolved = { ...DEFAULT_CHUNKING_CONFIG, ...chunkingConfig };

  // If no sources, don't chunk
  if (!config.sources || config.sources.length === 0) {
    return false;
  }

  // If explicitly disabled, don't chunk
  if (chunkingConfig?.enabled === false) {
    return false;
  }

  // If strategy is 'source' and we have multiple sources, chunking makes sense
  if (resolved.strategy === 'source' && config.sources.length > 1) {
    logger.debug(`Chunking enabled: ${config.sources.length} sources detected`);
    return true;
  }

  // For 'line-count' strategy, we'd need to estimate total lines
  // For now, enable if explicitly requested
  if (chunkingConfig?.enabled === true) {
    logger.debug('Chunking explicitly enabled in configuration');
    return true;
  }

  return false;
}

/**
 * Splits configuration into chunks based on strategy
 * @param config - Original configuration
 * @param chunkingConfig - Chunking configuration
 * @param logger - Logger instance
 * @returns Array of chunked configurations
 */
export function splitIntoChunks(
  config: IConfiguration,
  chunkingConfig?: ChunkingConfig,
  logger: Logger = defaultLogger,
): ChunkedConfiguration[] {
  const resolved = { ...DEFAULT_CHUNKING_CONFIG, ...chunkingConfig };

  if (!config.sources || config.sources.length === 0) {
    logger.warn('No sources to chunk');
    return [config as ChunkedConfiguration];
  }

  logger.info(`Splitting configuration into chunks (strategy: ${resolved.strategy})`);

  if (resolved.strategy === 'source') {
    return splitBySource(config, resolved, logger);
  } else {
    // Future: implement line-count based chunking
    logger.warn('line-count strategy not yet implemented, falling back to source strategy');
    return splitBySource(config, resolved, logger);
  }
}

/**
 * Splits configuration by source (one or more sources per chunk)
 * @param config - Original configuration
 * @param resolved - Resolved chunking configuration
 * @param logger - Logger instance
 * @returns Array of chunked configurations
 */
function splitBySource(
  config: IConfiguration,
  resolved: Required<ChunkingConfig>,
  logger: Logger,
): ChunkedConfiguration[] {
  const sources = config.sources || [];
  const chunks: ChunkedConfiguration[] = [];

  // Calculate sources per chunk to keep chunks balanced
  const sourcesPerChunk = Math.max(1, Math.ceil(sources.length / resolved.maxParallel));
  const totalChunks = Math.ceil(sources.length / sourcesPerChunk);

  logger.info(
    `Creating ${totalChunks} chunks with ~${sourcesPerChunk} sources each`,
  );

  for (let i = 0; i < totalChunks; i++) {
    const startIdx = i * sourcesPerChunk;
    const endIdx = Math.min(startIdx + sourcesPerChunk, sources.length);
    const chunkSources = sources.slice(startIdx, endIdx);

    const chunk: ChunkedConfiguration = {
      ...config,
      sources: chunkSources,
      name: `${config.name} (chunk ${i + 1}/${totalChunks})`,
      _chunkMetadata: {
        index: i,
        total: totalChunks,
        estimatedRules: 0, // We don't know yet
        sources: chunkSources,
      },
    };

    chunks.push(chunk);
  }

  logger.debug(`Created ${chunks.length} chunks`);
  return chunks;
}

/**
 * Merges compiled results from multiple chunks
 * @param chunks - Array of compiled rule arrays from each chunk
 * @param logger - Logger instance
 * @returns Merged and deduplicated rules array
 */
export function mergeChunks(
  chunks: string[][],
  logger: Logger = defaultLogger,
): string[] {
  logger.info(`Merging ${chunks.length} chunks...`);

  // Flatten all chunks into a single array
  const allRules = chunks.flat();
  logger.debug(`Total rules before deduplication: ${allRules.length}`);

  // Deduplicate while preserving order
  const seen = new Set<string>();
  const deduplicated = allRules.filter((rule) => {
    // Skip empty lines and comments during deduplication
    const trimmed = rule.trim();
    if (!trimmed || trimmed.startsWith('!') || trimmed.startsWith('#')) {
      return true; // Keep comments and empty lines
    }

    if (seen.has(rule)) {
      return false; // Remove duplicate
    }
    seen.add(rule);
    return true;
  });

  logger.info(
    `Merged to ${deduplicated.length} rules (removed ${
      allRules.length - deduplicated.length
    } duplicates)`,
  );

  return deduplicated;
}

/**
 * Gets optimal parallel chunk count based on system resources
 * @param totalChunks - Total number of chunks
 * @param maxParallel - Maximum parallel workers
 * @returns Optimal number of chunks to process in parallel
 */
export function getOptimalParallelCount(
  totalChunks: number,
  maxParallel: number = DEFAULT_CHUNKING_CONFIG.maxParallel,
): number {
  return Math.min(totalChunks, maxParallel);
}
