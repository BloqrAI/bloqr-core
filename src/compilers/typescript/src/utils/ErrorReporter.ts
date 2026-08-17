/**
 * Centralized error reporting for production monitoring.
 * Provides interfaces and implementations for reporting errors to various tracking services.
 */

import { ErrorUtils } from './ErrorUtils.ts';

/**
 * Context data to be sent with error reports.
 */
export interface ErrorContext extends Record<string, unknown> {
  /** Unique request ID */
  requestId?: string;
  /** User IP address */
  ip?: string;
  /** Request URL path */
  path?: string;
  /** Request method */
  method?: string;
  /** Additional custom context */
  [key: string]: unknown;
}

/**
 * Interface for error reporting services.
 * Implementations can send errors to various monitoring platforms.
 */
export interface IErrorReporter {
  /**
   * Reports an error with optional context.
   * @param error - The error to report
   * @param context - Additional context data
   */
  report(error: Error, context?: ErrorContext): void | Promise<void>;

  /**
   * Reports an error synchronously (if possible).
   * Falls back to async reporting if sync is not supported.
   * @param error - The error to report
   * @param context - Additional context data
   */
  reportSync(error: Error, context?: ErrorContext): void;
}

/**
 * Console-based error reporter for development and fallback.
 * Logs errors to console with formatted output.
 */
export class ConsoleErrorReporter implements IErrorReporter {
  constructor(private readonly verbose = false) {}

  /** Delegates to {@linkcode ConsoleErrorReporter.reportSync}. */
  report(error: Error, context?: ErrorContext): void {
    this.reportSync(error, context);
  }

  /** Logs the error (and, if `verbose`, its context) to the console. */
  reportSync(error: Error, context?: ErrorContext): void {
    const formatted = ErrorUtils.format(error);
    console.error('[ErrorReporter]', formatted);

    if (this.verbose && context) {
      console.error('[ErrorReporter] Context:', JSON.stringify(context, null, 2));
    }
  }
}

/**
 * Sentry error reporter for cloud-based error tracking.
 * Compatible with Cloudflare Workers using fetch API.
 */
export class SentryErrorReporter implements IErrorReporter {
  private readonly endpoint: string;
  private readonly environment: string;
  private readonly release?: string;

  /**
   * Creates a new Sentry error reporter.
   * @param dsn - Sentry DSN (Data Source Name)
   * @param options - Additional Sentry options
   */
  constructor(
    dsn: string,
    options?: {
      environment?: string;
      release?: string;
    },
  ) {
    // Extract project ID and public key from DSN
    // DSN format: https://<key>@<organization>.ingest.sentry.io/<project>
    const match = dsn.match(/https:\/\/([^@]+)@([^/]+)\/(\d+)/);
    if (!match) {
      throw new Error('Invalid Sentry DSN format');
    }

    const [, publicKey, host, projectId] = match;
    this.endpoint = `https://${host}/api/${projectId}/store/`;
    this.environment = options?.environment || 'production';
    this.release = options?.release;

    // Store the key for authentication header
    this.sentryKey = publicKey;
  }

  private readonly sentryKey: string;

  /** Sends the error to Sentry's envelope/store endpoint. */
  async report(error: Error, context?: ErrorContext): Promise<void> {
    try {
      const payload = this.buildSentryPayload(error, context);
      const authHeader = `Sentry sentry_version=7, sentry_key=${this.sentryKey}`;

      await fetch(this.endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Sentry-Auth': authHeader,
        },
        body: JSON.stringify(payload),
      });
    } catch (reportError) {
      // Fallback to console if Sentry reporting fails
      console.error('Failed to report error to Sentry:', reportError);
      console.error('Original error:', ErrorUtils.format(error));
    }
  }

  /** Fire-and-forget wrapper around {@linkcode SentryErrorReporter.report}. */
  reportSync(error: Error, context?: ErrorContext): void {
    // Fire and forget - don't wait for response
    this.report(error, context).catch((reportError) => {
      console.error('Failed to report error to Sentry:', reportError);
    });
  }

  private buildSentryPayload(error: Error, context?: ErrorContext): Record<string, unknown> {
    return {
      event_id: this.generateEventId(),
      timestamp: Date.now() / 1000,
      platform: 'javascript',
      environment: this.environment,
      release: this.release,
      exception: {
        values: [
          {
            type: error.name || 'Error',
            value: error.message,
            stacktrace: this.parseStackTrace(error),
          },
        ],
      },
      contexts: {
        runtime: {
          name: 'Deno/Cloudflare Workers',
        },
      },
      extra: context || {},
      tags: {
        errorCode: 'code' in error ? String((error as { code?: string }).code) : undefined,
      },
    };
  }

  private parseStackTrace(error: Error): { frames: Array<Record<string, unknown>> } | undefined {
    if (!error.stack) return undefined;

    type StackFrame = {
      filename: string;
      function: string;
      lineno: number;
      colno: number;
    };

    const lines = error.stack.split('\n');
    const frames: StackFrame[] = lines
      .slice(1) // Skip first line (error message)
      .map((line): StackFrame | null => {
        // Parse stack trace line (format varies by runtime)
        const match = line.match(/at\s+(?:(.+?)\s+\()?(.+?):(\d+):(\d+)\)?/);
        if (!match) return null;

        const [, func, filename, lineNo, colNo] = match;
        return {
          filename: filename || 'unknown',
          function: func || 'anonymous',
          lineno: parseInt(lineNo, 10),
          colno: parseInt(colNo, 10),
        };
      })
      .filter((frame): frame is StackFrame => frame !== null);

    return { frames: frames.reverse() }; // Sentry wants oldest first
  }

  private generateEventId(): string {
    return 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'.replace(/x/g, () => {
      return Math.floor(Math.random() * 16).toString(16);
    });
  }
}

/**
 * Cloudflare Analytics Engine error reporter.
 * Uses Cloudflare's built-in analytics for error tracking without external dependencies.
 */
export class CloudflareErrorReporter implements IErrorReporter {
  /**
   * Creates a new Cloudflare Analytics Engine error reporter.
   * @param dataset - Analytics Engine dataset binding
   * @param options - Additional options
   */
  constructor(
    private readonly dataset: AnalyticsEngineDataset,
    private readonly options?: {
      environment?: string;
      release?: string;
    },
  ) {}

  /** Writes the error as an Analytics Engine data point. */
  async report(error: Error, context?: ErrorContext): Promise<void> {
    try {
      // Write to Analytics Engine
      this.dataset.writeDataPoint({
        blobs: [
          error.name || 'Error',
          error.message,
          error.stack || '',
          this.options?.environment || 'production',
          context?.requestId || '',
          context?.path || '',
        ],
        doubles: [
          Date.now(),
        ],
        indexes: [
          this.options?.environment || 'production',
          error.name || 'Error',
        ],
      });
    } catch (reportError) {
      // Fallback to console if Analytics Engine fails
      console.error('Failed to report error to Analytics Engine:', reportError);
      console.error('Original error:', ErrorUtils.format(error));
    }
  }

  /** Synchronous variant — Analytics Engine's `writeDataPoint` is itself synchronous. */
  reportSync(error: Error, context?: ErrorContext): void {
    // Analytics Engine writeDataPoint is synchronous but returns void
    try {
      this.dataset.writeDataPoint({
        blobs: [
          error.name || 'Error',
          error.message,
          error.stack || '',
          this.options?.environment || 'production',
          context?.requestId || '',
          context?.path || '',
        ],
        doubles: [
          Date.now(),
        ],
        indexes: [
          this.options?.environment || 'production',
          error.name || 'Error',
        ],
      });
    } catch (reportError) {
      console.error('Failed to report error to Analytics Engine:', reportError);
    }
  }
}

/**
 * Composite error reporter that sends errors to multiple reporters.
 * Useful for combining console logging with cloud-based tracking.
 */
export class CompositeErrorReporter implements IErrorReporter {
  /**
   * Creates a new composite error reporter.
   * @param reporters - Array of error reporters to use
   */
  constructor(private readonly reporters: IErrorReporter[]) {}

  /** Reports to every wrapped reporter in parallel. */
  async report(error: Error, context?: ErrorContext): Promise<void> {
    // Report to all reporters in parallel
    await Promise.allSettled(
      this.reporters.map((reporter) => reporter.report(error, context)),
    );
  }

  /** Reports to every wrapped reporter synchronously, in order. */
  reportSync(error: Error, context?: ErrorContext): void {
    // Report to all reporters synchronously
    for (const reporter of this.reporters) {
      try {
        reporter.reportSync(error, context);
      } catch (reportError) {
        console.error('Failed to report error:', reportError);
      }
    }
  }

  /**
   * Adds a reporter to the composite.
   * @param reporter - Reporter to add
   */
  addReporter(reporter: IErrorReporter): void {
    this.reporters.push(reporter);
  }
}

/**
 * No-op error reporter that does nothing.
 * Useful for testing or when error reporting is disabled.
 */
export class NoOpErrorReporter implements IErrorReporter {
  /** Does nothing. */
  report(_error: Error, _context?: ErrorContext): void {
    // Do nothing
  }

  /** Does nothing. */
  reportSync(_error: Error, _context?: ErrorContext): void {
    // Do nothing
  }
}

/**
 * Analytics Engine dataset interface (from @cloudflare/workers-types).
 */
export interface AnalyticsEngineDataset {
  /** Writes a single data point to the bound Analytics Engine dataset. */
  writeDataPoint(event: {
    blobs?: string[];
    doubles?: number[];
    indexes?: string[];
  }): void;
}
