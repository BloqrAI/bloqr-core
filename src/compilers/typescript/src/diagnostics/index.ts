/**
 * Minimal diagnostics/tracing seam for the compiler-core engine.
 *
 * Provides just the types, the in-memory DiagnosticsCollector, and a no-op
 * default (NoOpDiagnosticsCollector / createNoOpContext) so FilterCompiler
 * and SourceCompiler can accept an optional diagnostics collector without
 * any observability backend wired in by default.
 *
 * NOTE: bloqr-compiler's OpenTelemetry/Sentry exporters and provider
 * abstractions (CompositeDiagnosticsProvider, OpenTelemetryDiagnosticsProvider,
 * SentryDiagnosticsProvider) are commercial/SaaS-integration concerns and
 * are not part of this core engine.
 */
export * from './types.ts';
export { DiagnosticsCollector, NoOpDiagnosticsCollector } from './DiagnosticsCollector.ts';
export {
  createChildContext,
  createNoOpContext,
  createTracingContext,
  getOrCreateContext,
  traceAsync,
  traced,
  tracedAsync,
  traceSync,
} from './TracingContext.ts';
