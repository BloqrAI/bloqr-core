/**
 * Tests for error reporting implementations.
 */

import { assertEquals, assertExists } from '@std/assert';
import {
  CloudflareErrorReporter,
  CompositeErrorReporter,
  ConsoleErrorReporter,
  NoOpErrorReporter,
  SentryErrorReporter,
} from './ErrorReporter.ts';
import type { AnalyticsEngineDataset, ErrorContext } from './ErrorReporter.ts';

// ============================================================================
// Test Utilities
// ============================================================================

/**
 * Mock Analytics Engine dataset for testing.
 */
class MockAnalyticsEngineDataset implements AnalyticsEngineDataset {
  public dataPoints: Array<{
    blobs?: string[];
    doubles?: number[];
    indexes?: string[];
  }> = [];

  writeDataPoint(event: {
    blobs?: string[];
    doubles?: number[];
    indexes?: string[];
  }): void {
    this.dataPoints.push(event);
  }
}

/**
 * Mock fetch for testing Sentry reporter.
 */
class MockFetch {
  public requests: Array<{
    url: string;
    options: RequestInit;
  }> = [];

  async fetch(input: URL | RequestInfo, options?: RequestInit): Promise<Response> {
    const url = typeof input === 'string'
      ? input
      : input instanceof URL
      ? input.toString()
      : input.url;
    this.requests.push({ url, options: options ?? {} });
    return new Response(JSON.stringify({ id: 'test-event-id' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  }
}

/**
 * Captures console output for testing.
 */
class ConsoleCapture {
  public errors: string[] = [];
  private originalError: typeof console.error;

  constructor() {
    this.originalError = console.error;
    console.error = (...args: unknown[]) => {
      this.errors.push(args.map(String).join(' '));
    };
  }

  restore(): void {
    console.error = this.originalError;
  }
}

// ============================================================================
// ConsoleErrorReporter Tests
// ============================================================================

Deno.test('ConsoleErrorReporter - reports errors to console', () => {
  const capture = new ConsoleCapture();
  const reporter = new ConsoleErrorReporter(false);
  const error = new Error('Test error');

  reporter.report(error);

  capture.restore();
  assertEquals(capture.errors.length, 1);
  assertEquals(capture.errors[0].includes('Test error'), true);
});

Deno.test('ConsoleErrorReporter - verbose mode includes context', () => {
  const capture = new ConsoleCapture();
  const reporter = new ConsoleErrorReporter(true);
  const error = new Error('Test error');
  const context: ErrorContext = {
    requestId: 'req-123',
    path: '/test',
  };

  reporter.report(error, context);

  capture.restore();
  assertEquals(capture.errors.length, 2);
  assertEquals(capture.errors[0].includes('Test error'), true);
  assertEquals(capture.errors[1].includes('req-123'), true);
});

Deno.test('ConsoleErrorReporter - reportSync works', () => {
  const capture = new ConsoleCapture();
  const reporter = new ConsoleErrorReporter(false);
  const error = new Error('Sync test error');

  reporter.reportSync(error);

  capture.restore();
  assertEquals(capture.errors.length, 1);
});

// ============================================================================
// CloudflareErrorReporter Tests
// ============================================================================

Deno.test('CloudflareErrorReporter - writes to Analytics Engine', async () => {
  const dataset = new MockAnalyticsEngineDataset();
  const reporter = new CloudflareErrorReporter(dataset, {
    environment: 'test',
    release: '1.0.0',
  });
  const error = new Error('Analytics test error');
  const context: ErrorContext = {
    requestId: 'req-456',
    path: '/analytics',
  };

  await reporter.report(error, context);

  assertEquals(dataset.dataPoints.length, 1);
  const dataPoint = dataset.dataPoints[0];
  assertExists(dataPoint.blobs);
  assertEquals(dataPoint.blobs![0], 'Error');
  assertEquals(dataPoint.blobs![1], 'Analytics test error');
  assertEquals(dataPoint.blobs![3], 'test');
  assertEquals(dataPoint.blobs![4], 'req-456');
  assertEquals(dataPoint.blobs![5], '/analytics');
  assertExists(dataPoint.doubles);
  assertExists(dataPoint.indexes);
  assertEquals(dataPoint.indexes![0], 'test');
  assertEquals(dataPoint.indexes![1], 'Error');
});

Deno.test('CloudflareErrorReporter - reportSync works', () => {
  const dataset = new MockAnalyticsEngineDataset();
  const reporter = new CloudflareErrorReporter(dataset);
  const error = new Error('Sync analytics error');

  reporter.reportSync(error);

  assertEquals(dataset.dataPoints.length, 1);
});

Deno.test('CloudflareErrorReporter - handles missing context', async () => {
  const dataset = new MockAnalyticsEngineDataset();
  const reporter = new CloudflareErrorReporter(dataset);
  const error = new Error('No context error');

  await reporter.report(error);

  assertEquals(dataset.dataPoints.length, 1);
  const dataPoint = dataset.dataPoints[0];
  assertExists(dataPoint.blobs);
  assertEquals(dataPoint.blobs![4], ''); // Empty requestId
  assertEquals(dataPoint.blobs![5], ''); // Empty path
});

// ============================================================================
// SentryErrorReporter Tests
// ============================================================================

Deno.test('SentryErrorReporter - parses DSN correctly', () => {
  const dsn = 'https://abc123@o123456.ingest.sentry.io/7890';
  const reporter = new SentryErrorReporter(dsn);
  assertExists(reporter);
});

Deno.test('SentryErrorReporter - throws on invalid DSN', () => {
  let threw = false;
  try {
    new SentryErrorReporter('invalid-dsn');
  } catch {
    threw = true;
  }
  assertEquals(threw, true);
});

Deno.test('SentryErrorReporter - builds correct payload', async () => {
  const dsn = 'https://abc123@o123456.ingest.sentry.io/7890';
  const mockFetch = new MockFetch();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = mockFetch.fetch.bind(mockFetch) as typeof globalThis.fetch;

  const reporter = new SentryErrorReporter(dsn, {
    environment: 'test',
    release: '1.0.0',
  });
  const error = new Error('Sentry test error');
  const context: ErrorContext = {
    requestId: 'req-789',
    path: '/sentry',
  };

  await reporter.report(error, context);

  globalThis.fetch = originalFetch;

  assertEquals(mockFetch.requests.length, 1);
  const request = mockFetch.requests[0];
  assertEquals(request.url, 'https://o123456.ingest.sentry.io/api/7890/store/');
  assertExists(request.options.headers);
  const headers = request.options.headers as Record<string, string>;
  assertEquals(headers['Content-Type'], 'application/json');
  assertEquals(headers['X-Sentry-Auth'].includes('sentry_key=abc123'), true);

  const body = JSON.parse(request.options.body as string);
  assertEquals(body.environment, 'test');
  assertEquals(body.release, '1.0.0');
  assertEquals(body.exception.values[0].value, 'Sentry test error');
  assertEquals(body.extra.requestId, 'req-789');
  assertEquals(body.extra.path, '/sentry');
});

Deno.test('SentryErrorReporter - reportSync fires and forgets', () => {
  const dsn = 'https://abc123@o123456.ingest.sentry.io/7890';
  const mockFetch = new MockFetch();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = mockFetch.fetch.bind(mockFetch) as typeof globalThis.fetch;

  const reporter = new SentryErrorReporter(dsn);
  const error = new Error('Sync sentry error');

  reporter.reportSync(error);

  globalThis.fetch = originalFetch;
  // We can't verify the request immediately since it's fire-and-forget
  // but we can verify it doesn't throw
});

// ============================================================================
// CompositeErrorReporter Tests
// ============================================================================

Deno.test('CompositeErrorReporter - reports to all reporters', async () => {
  const dataset = new MockAnalyticsEngineDataset();
  const capture = new ConsoleCapture();

  const reporters = [
    new CloudflareErrorReporter(dataset),
    new ConsoleErrorReporter(false),
  ];
  const composite = new CompositeErrorReporter(reporters);
  const error = new Error('Composite test error');

  await composite.report(error);

  capture.restore();
  assertEquals(dataset.dataPoints.length, 1);
  assertEquals(capture.errors.length, 1);
});

Deno.test('CompositeErrorReporter - reportSync works for all reporters', () => {
  const dataset = new MockAnalyticsEngineDataset();
  const capture = new ConsoleCapture();

  const reporters = [
    new CloudflareErrorReporter(dataset),
    new ConsoleErrorReporter(false),
  ];
  const composite = new CompositeErrorReporter(reporters);
  const error = new Error('Sync composite error');

  composite.reportSync(error);

  capture.restore();
  assertEquals(dataset.dataPoints.length, 1);
  assertEquals(capture.errors.length, 1);
});

Deno.test('CompositeErrorReporter - addReporter works', async () => {
  const dataset1 = new MockAnalyticsEngineDataset();
  const dataset2 = new MockAnalyticsEngineDataset();

  const composite = new CompositeErrorReporter([
    new CloudflareErrorReporter(dataset1),
  ]);
  composite.addReporter(new CloudflareErrorReporter(dataset2));

  const error = new Error('Add reporter test');
  await composite.report(error);

  assertEquals(dataset1.dataPoints.length, 1);
  assertEquals(dataset2.dataPoints.length, 1);
});

Deno.test('CompositeErrorReporter - continues on reporter failure', async () => {
  const dataset = new MockAnalyticsEngineDataset();
  const capture = new ConsoleCapture();

  // Create a failing reporter
  const failingReporter = {
    report: () => Promise.reject(new Error('Reporter failed')),
    reportSync: () => {
      throw new Error('Reporter failed');
    },
  };

  const composite = new CompositeErrorReporter([
    failingReporter,
    new CloudflareErrorReporter(dataset),
  ]);
  const error = new Error('Failure handling test');

  await composite.report(error);

  capture.restore();
  // Should still report to the second reporter despite first one failing
  assertEquals(dataset.dataPoints.length, 1);
});

// ============================================================================
// NoOpErrorReporter Tests
// ============================================================================

Deno.test('NoOpErrorReporter - does nothing', async () => {
  const reporter = new NoOpErrorReporter();
  const error = new Error('NoOp test error');

  // Should not throw
  reporter.report(error);
  await reporter.report(error);
  reporter.reportSync(error);

  // No assertions needed - just verify it doesn't throw
});

// ============================================================================
// Error Stack Trace Parsing Tests
// ============================================================================

Deno.test('SentryErrorReporter - includes stack trace in payload', async () => {
  const dsn = 'https://abc123@o123456.ingest.sentry.io/7890';
  const mockFetch = new MockFetch();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = mockFetch.fetch.bind(mockFetch) as typeof globalThis.fetch;

  const reporter = new SentryErrorReporter(dsn);
  const error = new Error('Stack trace test');

  await reporter.report(error);

  globalThis.fetch = originalFetch;

  const body = JSON.parse(mockFetch.requests[0].options.body as string);
  assertExists(body.exception.values[0].stacktrace);
});
