/**
 * Circuit Breaker Pattern Implementation
 *
 * Prevents cascading failures by tracking consecutive failures and temporarily
 * blocking requests to failing resources. Implements three states:
 * - CLOSED: Normal operation, requests pass through
 * - OPEN: Too many failures, requests are blocked
 * - HALF_OPEN: Testing if resource has recovered
 *
 * @example
 * ```typescript
 * const breaker = new CircuitBreaker({ threshold: 5, timeout: 60000 });
 *
 * try {
 *     const result = await breaker.execute(() => fetch(url));
 *     console.log('Success:', result);
 * } catch (error) {
 *     console.error('Circuit breaker open or request failed:', error);
 * }
 * ```
 */

import type { ILogger } from '../types/index.ts';
import { silentLogger } from './logger.ts';

/**
 * Circuit breaker states
 */
export enum CircuitBreakerState {
  /** Normal operation - requests pass through */
  CLOSED = 'CLOSED',
  /** Too many failures - requests are blocked */
  OPEN = 'OPEN',
  /** Testing recovery - next request will be attempted */
  HALF_OPEN = 'HALF_OPEN',
}

/**
 * Circuit breaker configuration options
 */
export interface CircuitBreakerOptions {
  /** Number of consecutive failures before opening the circuit */
  threshold?: number;
  /** Time in milliseconds to wait before attempting recovery (HALF_OPEN) */
  timeout?: number;
  /** Logger instance for debugging */
  logger?: ILogger;
  /** Name/identifier for this circuit breaker (for logging) */
  name?: string;
}

/**
 * Circuit breaker statistics
 */
export interface CircuitBreakerStats {
  /** Current state of the circuit breaker */
  state: CircuitBreakerState;
  /** Current number of consecutive failures */
  failureCount: number;
  /** Failure threshold before circuit opens */
  threshold: number;
  /** Timeout in milliseconds before recovery attempt */
  timeout: number;
  /** Time of the last failure, if any */
  lastFailureTime: Date | undefined;
  /** Time remaining until recovery attempt (0 if not OPEN) */
  timeUntilRecovery: number;
}

/**
 * Circuit breaker error thrown when the circuit is open
 */
export class CircuitBreakerOpenError extends Error {
  /** Always `'CircuitBreakerOpenError'`; useful for `instanceof`-free error discrimination. */
  public override readonly name: string = 'CircuitBreakerOpenError';

  /**
   * Creates a new `CircuitBreakerOpenError`.
   * @param message Human-readable description of why the request was rejected.
   * @param state The circuit breaker's state at the time the error was thrown (typically `OPEN`).
   * @param breakerName Optional identifier of the circuit breaker, prepended to the message.
   */
  constructor(
    message: string,
    public readonly state: CircuitBreakerState,
    breakerName?: string,
  ) {
    super(message);
    if (breakerName) {
      this.message = `[${breakerName}] ${message}`;
    }
  }
}

/**
 * Circuit Breaker implementation for fault tolerance
 *
 * Tracks failures and prevents requests to failing resources by:
 * 1. Counting consecutive failures
 * 2. Opening the circuit after threshold is reached
 * 3. Blocking requests while circuit is open
 * 4. Attempting recovery after timeout period
 * 5. Closing circuit on successful recovery
 */
export class CircuitBreaker {
  private failureCount = 0;
  private state: CircuitBreakerState = CircuitBreakerState.CLOSED;
  private lastFailureTime?: Date;
  private readonly threshold: number;
  private readonly timeout: number;
  private readonly logger: ILogger;
  private readonly name: string;

  /**
   * Creates a new circuit breaker
   * @param options - Configuration options
   */
  constructor(options: CircuitBreakerOptions = {}) {
    this.threshold = options.threshold ?? 5;
    this.timeout = options.timeout ?? 60000; // 60 seconds default
    this.logger = options.logger ?? silentLogger;
    this.name = options.name ?? 'CircuitBreaker';
  }

  /**
   * Executes a function with circuit breaker protection
   *
   * @param fn - Async function to execute
   * @returns Result of the function
   * @throws CircuitBreakerOpenError if circuit is open
   * @throws Error from the executed function if it fails
   */
  async execute<T>(fn: () => Promise<T>): Promise<T> {
    // Check if we should transition from OPEN to HALF_OPEN
    if (this.state === CircuitBreakerState.OPEN) {
      if (this.shouldAttemptRecovery()) {
        this.logger.debug(`[${this.name}] Circuit transitioning to HALF_OPEN for recovery attempt`);
        this.state = CircuitBreakerState.HALF_OPEN;
      } else {
        const timeRemaining = this.getTimeUntilRecovery();
        throw new CircuitBreakerOpenError(
          `Circuit breaker is OPEN. Retry in ${Math.ceil(timeRemaining / 1000)}s`,
          this.state,
          this.name,
        );
      }
    }

    try {
      const result = await fn();
      this.onSuccess();
      return result;
    } catch (error) {
      this.onFailure();
      throw error;
    }
  }

  /**
   * Handles successful execution
   */
  private onSuccess(): void {
    const previousState = this.state;

    // Reset failure count and close circuit
    this.failureCount = 0;
    this.state = CircuitBreakerState.CLOSED;
    this.lastFailureTime = undefined;

    if (previousState !== CircuitBreakerState.CLOSED) {
      this.logger.info(`[${this.name}] Circuit breaker recovered and closed`);
    }
  }

  /**
   * Handles failed execution
   */
  private onFailure(): void {
    this.failureCount++;
    this.lastFailureTime = new Date();

    if (this.state === CircuitBreakerState.HALF_OPEN) {
      // Failed recovery attempt - back to OPEN
      this.logger.warn(`[${this.name}] Recovery attempt failed, circuit reopening`);
      this.state = CircuitBreakerState.OPEN;
    } else if (this.failureCount >= this.threshold) {
      // Threshold exceeded - open circuit
      this.logger.warn(
        `[${this.name}] Circuit breaker opened after ${this.failureCount} failures (threshold: ${this.threshold})`,
      );
      this.state = CircuitBreakerState.OPEN;
    } else {
      this.logger.debug(
        `[${this.name}] Failure ${this.failureCount}/${this.threshold} recorded`,
      );
    }
  }

  /**
   * Checks if enough time has passed to attempt recovery
   */
  private shouldAttemptRecovery(): boolean {
    if (!this.lastFailureTime) {
      return true;
    }

    const timeSinceLastFailure = Date.now() - this.lastFailureTime.getTime();
    return timeSinceLastFailure >= this.timeout;
  }

  /**
   * Gets time remaining until recovery attempt
   */
  private getTimeUntilRecovery(): number {
    if (!this.lastFailureTime) {
      return 0;
    }

    const timeSinceLastFailure = Date.now() - this.lastFailureTime.getTime();
    return Math.max(0, this.timeout - timeSinceLastFailure);
  }

  /**
   * Gets the current state of the circuit breaker
   */
  getState(): CircuitBreakerState {
    return this.state;
  }

  /**
   * Gets the current failure count
   */
  getFailureCount(): number {
    return this.failureCount;
  }

  /**
   * Gets the last failure time
   */
  getLastFailureTime(): Date | undefined {
    return this.lastFailureTime;
  }

  /**
   * Manually resets the circuit breaker to CLOSED state
   * Useful for testing or manual recovery
   */
  reset(): void {
    this.logger.info(`[${this.name}] Circuit breaker manually reset`);
    this.failureCount = 0;
    this.state = CircuitBreakerState.CLOSED;
    this.lastFailureTime = undefined;
  }

  /**
   * Gets circuit breaker statistics
   */
  getStats(): CircuitBreakerStats {
    return {
      state: this.state,
      failureCount: this.failureCount,
      threshold: this.threshold,
      timeout: this.timeout,
      lastFailureTime: this.lastFailureTime,
      timeUntilRecovery: this.state === CircuitBreakerState.OPEN ? this.getTimeUntilRecovery() : 0,
    };
  }
}
