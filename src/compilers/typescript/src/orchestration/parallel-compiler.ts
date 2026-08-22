/**
 * Parallel compilation using Deno workers for chunked rule compilation
 * Enables multi-threaded compilation to work around single-threaded @bloqr/compiler-core
 */

import compile, { type IConfiguration } from '../index.ts';
import type { Logger } from './types.ts';
import type { ChunkedConfiguration } from './chunking.ts';
import { logger as defaultLogger } from './logger.ts';
import { CompilationError, ErrorCode } from './errors.ts';
import { stripInternalMetadata } from './config-reader.ts';

/**
 * Worker message types
 */
interface WorkerRequest {
  type: 'compile';
  config: IConfiguration;
  chunkIndex: number;
}

interface WorkerResponse {
  type: 'success' | 'error';
  chunkIndex: number;
  rules?: string[];
  error?: string;
}

/**
 * Compiles a single chunk (worker entry point or direct call)
 * @param config - Configuration for this chunk
 * @returns Compiled rules array
 */
export async function compileChunk(config: IConfiguration): Promise<string[]> {
  try {
    // `compile()` is the core engine's strict-schema boundary - it rejects unrecognized
    // properties, so the `_sourceFormat`/`_sourcePath`/`_chunkMetadata` orchestration-layer
    // metadata a chunk config carries (from readConfiguration()/splitIntoChunks()) must be
    // stripped first.
    const result = await compile(stripInternalMetadata(config));
    return result;
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    throw new CompilationError(
      `Chunk compilation failed: ${message}`,
      ErrorCode.COMPILATION_FAILED,
      { configName: config.name },
      error instanceof Error ? error : undefined,
    );
  }
}

/**
 * Compiles chunks in parallel using simple Promise.all (no workers for simplicity)
 * @param chunks - Array of chunked configurations
 * @param maxParallel - Maximum number of parallel compilations
 * @param logger - Logger instance
 * @returns Array of compiled rule arrays (one per chunk)
 */
export async function compileChunksInParallel(
  chunks: ChunkedConfiguration[],
  maxParallel: number,
  logger: Logger = defaultLogger,
): Promise<string[][]> {
  logger.info(`Compiling ${chunks.length} chunks with max ${maxParallel} parallel workers`);

  const results: string[][] = new Array(chunks.length);
  const errors: Array<{ index: number; error: Error }> = [];

  // Process chunks in batches to limit parallelism
  for (let batchStart = 0; batchStart < chunks.length; batchStart += maxParallel) {
    const batchEnd = Math.min(batchStart + maxParallel, chunks.length);
    const batch = chunks.slice(batchStart, batchEnd);

    logger.info(
      `Processing batch ${Math.floor(batchStart / maxParallel) + 1}/${
        Math.ceil(chunks.length / maxParallel)
      } (chunks ${batchStart + 1}-${batchEnd})`,
    );

    // Compile all chunks in this batch in parallel
    const batchPromises = batch.map(async (chunk, batchIndex) => {
      const chunkIndex = batchStart + batchIndex;

      try {
        logger.debug(
          `Starting chunk ${chunkIndex + 1}/${chunks.length}: ${chunk.name}`,
        );

        const startTime = Date.now();
        const rules = await compileChunk(chunk);
        const elapsed = Date.now() - startTime;

        logger.info(
          `Chunk ${
            chunkIndex + 1
          }/${chunks.length} complete: ${rules.length} rules in ${elapsed}ms`,
        );

        results[chunkIndex] = rules;
      } catch (error) {
        const err = error instanceof Error ? error : new Error(String(error));
        logger.error(
          `Chunk ${chunkIndex + 1}/${chunks.length} failed: ${err.message}`,
        );
        errors.push({ index: chunkIndex, error: err });
      }
    });

    // Wait for all chunks in this batch to complete
    await Promise.all(batchPromises);
  }

  // If any chunks failed, throw an error
  if (errors.length > 0) {
    const errorMessages = errors.map((e) => `Chunk ${e.index + 1}: ${e.error.message}`).join(
      '; ',
    );
    throw new CompilationError(
      `${errors.length} chunk(s) failed: ${errorMessages}`,
      ErrorCode.COMPILATION_FAILED,
    );
  }

  logger.info('All chunks compiled successfully');
  return results;
}

/**
 * Estimates total compilation time for chunked vs non-chunked compilation
 * @param totalRules - Estimated total number of rules
 * @param chunkSize - Rules per chunk
 * @param maxParallel - Maximum parallel workers
 * @returns Estimated time savings ratio (chunked / non-chunked)
 */
export function estimateTimeSavings(
  totalRules: number,
  chunkSize: number,
  maxParallel: number,
): number {
  // Assume linear scaling (simplified model)
  const nonChunkedTime = totalRules;
  const numChunks = Math.ceil(totalRules / chunkSize);
  const batches = Math.ceil(numChunks / maxParallel);
  const chunkedTime = batches * chunkSize;

  return chunkedTime / nonChunkedTime;
}
