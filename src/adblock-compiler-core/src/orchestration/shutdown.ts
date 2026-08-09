/**
 * Graceful shutdown handler for production deployments
 * Handles SIGTERM, SIGINT signals and cleanup
 *
 * Deno-native, with a Node.js/Bun fallback: signal registration uses
 * `Deno.addSignalListener` when the `Deno` global is present, otherwise
 * `process.on(signal, ...)` (which both Node.js and Bun implement).
 * Likewise, unhandled-rejection reporting uses Deno's
 * `globalThis.addEventListener('unhandledrejection', ...)` under Deno and
 * `process.on('unhandledRejection', ...)` otherwise — Node.js does not
 * implement `globalThis.addEventListener` for process-level events at all,
 * so this fallback is required, not just a compatibility nicety.
 */

import process from 'node:process';
import { ShutdownError } from './errors.ts';
import type { Logger } from './types.ts';

/**
 * Cleanup function type
 */
export type CleanupFn = () => void | Promise<void>;

/**
 * Shutdown handler configuration
 */
export interface ShutdownConfig {
  /** Timeout for cleanup operations in milliseconds (default: 10000) */
  cleanupTimeoutMs: number;
  /** Logger instance */
  logger?: Logger;
}

/**
 * Default shutdown configuration
 */
const DEFAULT_SHUTDOWN_CONFIG: ShutdownConfig = {
  cleanupTimeoutMs: 10000,
};

/**
 * Deno signal type
 */
type DenoSignal = 'SIGTERM' | 'SIGINT' | 'SIGHUP';

/**
 * Manages graceful shutdown with signal handling
 */
export class ShutdownHandler {
  private cleanupFns: CleanupFn[] = [];
  private isShuttingDown = false;
  private shutdownPromise: Promise<void> | null = null;
  private readonly config: ShutdownConfig;
  private readonly logger?: Logger;
  private signalHandlers: Map<DenoSignal, () => void> = new Map();

  /**
   * Creates a new `ShutdownHandler`. Does not itself register signal listeners; call
   * {@linkcode listen} to start watching for `SIGTERM`/`SIGINT`.
   * @param config Partial shutdown configuration, merged over the defaults
   * (10s cleanup timeout, no logger).
   */
  constructor(config: Partial<ShutdownConfig> = {}) {
    this.config = { ...DEFAULT_SHUTDOWN_CONFIG, ...config };
    this.logger = config.logger;
  }

  /**
   * Registers a cleanup function to be called on shutdown
   */
  registerCleanup(fn: CleanupFn): void {
    this.cleanupFns.push(fn);
  }

  /**
   * Removes a cleanup function
   */
  unregisterCleanup(fn: CleanupFn): void {
    const index = this.cleanupFns.indexOf(fn);
    if (index !== -1) {
      this.cleanupFns.splice(index, 1);
    }
  }

  /**
   * Starts listening for shutdown signals
   */
  listen(): void {
    const signals: DenoSignal[] = ['SIGTERM', 'SIGINT', 'SIGHUP'];
    const isDeno = typeof Deno !== 'undefined';

    for (const signal of signals) {
      const handler = (): void => {
        void this.handleSignal(signal);
      };
      this.signalHandlers.set(signal, handler);
      try {
        if (isDeno) {
          Deno.addSignalListener(signal, handler);
        } else {
          process.on(signal, handler);
        }
      } catch {
        // Signal may not be available on all platforms
        this.logger?.debug(`Signal ${signal} not available on this platform`);
      }
    }

    if (isDeno) {
      // Deno-native unhandled-rejection reporting.
      globalThis.addEventListener('unhandledrejection', (event: PromiseRejectionEvent) => {
        const reason = event.reason;
        const message = reason instanceof Error ? reason.message : String(reason);
        this.logger?.error(`Unhandled rejection: ${message}`);
      });
    } else {
      // Node.js has no `globalThis.addEventListener` for process events;
      // Bun implements this same `process.on` API.
      process.on('unhandledRejection', (reason: unknown) => {
        const message = reason instanceof Error ? reason.message : String(reason);
        this.logger?.error(`Unhandled rejection: ${message}`);
      });
    }

    this.logger?.debug('Shutdown handler initialized');
  }

  /**
   * Stops listening for shutdown signals
   */
  unlisten(): void {
    const isDeno = typeof Deno !== 'undefined';
    for (const [signal, handler] of this.signalHandlers) {
      try {
        if (isDeno) {
          Deno.removeSignalListener(signal, handler);
        } else {
          process.off(signal, handler);
        }
      } catch {
        // Signal may not have been registered
      }
    }
    this.signalHandlers.clear();
    this.logger?.debug('Shutdown handler removed');
  }

  /**
   * Handles a shutdown signal
   */
  private async handleSignal(signal: string): Promise<void> {
    this.logger?.info(`Received ${signal}, initiating graceful shutdown...`);
    await this.shutdown(signal);
  }

  /**
   * Initiates graceful shutdown
   */
  async shutdown(reason: string = 'manual'): Promise<void> {
    if (this.isShuttingDown) {
      this.logger?.debug('Shutdown already in progress, waiting...');
      if (this.shutdownPromise) {
        await this.shutdownPromise;
      }
      return;
    }

    this.isShuttingDown = true;
    this.logger?.info(`Starting shutdown (reason: ${reason})`);

    this.shutdownPromise = this.executeCleanup();
    await this.shutdownPromise;
  }

  /**
   * Executes all cleanup functions with timeout
   */
  private async executeCleanup(): Promise<void> {
    const timeoutPromise = new Promise<void>((_, reject) => {
      setTimeout(() => {
        reject(new Error(`Cleanup timed out after ${this.config.cleanupTimeoutMs}ms`));
      }, this.config.cleanupTimeoutMs);
    });

    const cleanupPromise = this.runCleanupFunctions();

    try {
      await Promise.race([cleanupPromise, timeoutPromise]);
      this.logger?.info('Graceful shutdown complete');
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.logger?.error(`Shutdown error: ${message}`);
    }
  }

  /**
   * Runs all registered cleanup functions
   */
  private async runCleanupFunctions(): Promise<void> {
    // Execute cleanup functions in reverse order (LIFO)
    const reversedFns = [...this.cleanupFns].reverse();

    for (const fn of reversedFns) {
      try {
        await fn();
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        this.logger?.error(`Cleanup function failed: ${message}`);
      }
    }
  }

  /**
   * Checks if shutdown is in progress
   */
  get shuttingDown(): boolean {
    return this.isShuttingDown;
  }

  /**
   * Throws if shutdown is in progress
   */
  assertNotShuttingDown(): void {
    if (this.isShuttingDown) {
      throw new ShutdownError('shutdown');
    }
  }
}

/**
 * Global shutdown handler instance
 */
let globalShutdownHandler: ShutdownHandler | null = null;

/**
 * Gets or creates the global shutdown handler
 */
export function getShutdownHandler(config?: Partial<ShutdownConfig>): ShutdownHandler {
  if (!globalShutdownHandler) {
    globalShutdownHandler = new ShutdownHandler(config);
  }
  return globalShutdownHandler;
}

/**
 * Initializes graceful shutdown handling
 */
export function initializeShutdownHandler(config?: Partial<ShutdownConfig>): ShutdownHandler {
  const handler = getShutdownHandler(config);
  handler.listen();
  return handler;
}

/**
 * Creates an abort controller that responds to shutdown
 */
export function createShutdownAwareAbortController(handler: ShutdownHandler): AbortController {
  const controller = new AbortController();

  const cleanup = (): void => {
    controller.abort();
  };

  handler.registerCleanup(cleanup);

  return controller;
}

/**
 * Wraps a promise to reject on shutdown
 */
export function withShutdownCheck<T>(
  promise: Promise<T>,
  handler: ShutdownHandler,
): Promise<T> {
  handler.assertNotShuttingDown();

  const shutdownPromise = new Promise<never>((_, reject) => {
    const checkInterval = setInterval(() => {
      if (handler.shuttingDown) {
        clearInterval(checkInterval);
        reject(new ShutdownError('shutdown'));
      }
    }, 100);

    // Clean up interval when promise resolves
    void promise.finally(() => {
      clearInterval(checkInterval);
    });
  });

  return Promise.race([promise, shutdownPromise]);
}
