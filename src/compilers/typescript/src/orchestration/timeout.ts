/**
 * Timeout utilities for resource limiting
 * Provides promise-based timeout handling for production safety
 * Deno-compatible implementation
 */

import { CompilationTimeoutError } from './errors.ts';

/**
 * Wraps a promise with a timeout
 * @param promise - The promise to wrap
 * @param timeoutMs - Timeout in milliseconds
 * @param context - Optional context for error message
 * @returns The promise result or throws on timeout
 */
export async function withTimeout<T>(
  promise: Promise<T>,
  timeoutMs: number,
  context: Record<string, unknown> = {},
): Promise<T> {
  let timeoutId: ReturnType<typeof setTimeout> | undefined;

  const timeoutPromise = new Promise<never>((_, reject) => {
    timeoutId = setTimeout(() => {
      reject(new CompilationTimeoutError(timeoutMs, { metadata: context }));
    }, timeoutMs);
  });

  try {
    const result = await Promise.race([promise, timeoutPromise]);
    return result;
  } finally {
    if (timeoutId !== undefined) {
      clearTimeout(timeoutId);
    }
  }
}

/**
 * Creates a timeout controller for cancellable operations
 */
export interface TimeoutController {
  /** The abort signal to pass to cancellable operations */
  signal: AbortSignal;
  /** Clear the timeout (call when operation completes) */
  clear: () => void;
  /** Check if timeout has occurred */
  isTimedOut: boolean;
}

/**
 * Creates a timeout controller with abort signal
 * @param timeoutMs - Timeout in milliseconds
 * @returns Timeout controller
 */
export function createTimeoutController(timeoutMs: number): TimeoutController {
  const controller = new AbortController();
  let isTimedOut = false;

  const timeoutId = setTimeout(() => {
    isTimedOut = true;
    controller.abort();
  }, timeoutMs);

  return {
    signal: controller.signal,
    clear: () => {
      clearTimeout(timeoutId);
    },
    get isTimedOut() {
      return isTimedOut;
    },
  };
}

/**
 * Retry configuration
 */
export interface RetryConfig {
  /** Maximum number of retry attempts (default: 3) */
  maxAttempts: number;
  /** Initial delay between retries in ms (default: 1000) */
  initialDelayMs: number;
  /** Maximum delay between retries in ms (default: 10000) */
  maxDelayMs: number;
  /** Multiplier for exponential backoff (default: 2) */
  backoffMultiplier: number;
  /** Function to determine if error is retryable */
  isRetryable?: (error: unknown) => boolean;
}

/**
 * Default retry configuration
 */
const DEFAULT_RETRY_CONFIG: RetryConfig = {
  maxAttempts: 3,
  initialDelayMs: 1000,
  maxDelayMs: 10000,
  backoffMultiplier: 2,
};

/**
 * Sleeps for specified duration
 */
function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Executes a function with retry logic
 * @param fn - Function to execute
 * @param config - Retry configuration
 * @returns The function result
 */
export async function withRetry<T>(
  fn: () => Promise<T>,
  config: Partial<RetryConfig> = {},
): Promise<T> {
  const resolvedConfig: RetryConfig = { ...DEFAULT_RETRY_CONFIG, ...config };
  let lastError: unknown;
  let delay = resolvedConfig.initialDelayMs;

  for (let attempt = 1; attempt <= resolvedConfig.maxAttempts; attempt++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error;

      // Check if we should retry
      const isRetryable = resolvedConfig.isRetryable ? resolvedConfig.isRetryable(error) : true;

      if (!isRetryable || attempt === resolvedConfig.maxAttempts) {
        throw error;
      }

      // Wait before retry with exponential backoff
      await sleep(delay);
      delay = Math.min(delay * resolvedConfig.backoffMultiplier, resolvedConfig.maxDelayMs);
    }
  }

  throw lastError;
}

/**
 * Creates a debounced version of a function
 * @param fn - Function to debounce
 * @param delayMs - Debounce delay in milliseconds
 * @returns Debounced function
 */
export function debounce<T extends (...args: Parameters<T>) => void>(
  fn: T,
  delayMs: number,
): (...args: Parameters<T>) => void {
  let timeoutId: ReturnType<typeof setTimeout> | undefined;

  return (...args: Parameters<T>): void => {
    if (timeoutId !== undefined) {
      clearTimeout(timeoutId);
    }
    timeoutId = setTimeout(() => {
      fn(...args);
    }, delayMs);
  };
}

/**
 * Creates a throttled version of a function
 * @param fn - Function to throttle
 * @param intervalMs - Throttle interval in milliseconds
 * @returns Throttled function
 */
export function throttle<T extends (...args: Parameters<T>) => void>(
  fn: T,
  intervalMs: number,
): (...args: Parameters<T>) => void {
  let lastCall = 0;
  let timeoutId: ReturnType<typeof setTimeout> | undefined;

  return (...args: Parameters<T>): void => {
    const now = Date.now();
    const remaining = intervalMs - (now - lastCall);

    if (remaining <= 0) {
      lastCall = now;
      fn(...args);
    } else if (timeoutId === undefined) {
      timeoutId = setTimeout(() => {
        lastCall = Date.now();
        timeoutId = undefined;
        fn(...args);
      }, remaining);
    }
  };
}
