/**
 * Tests for custom error classes
 * Deno-native testing implementation
 */

import { assertEquals, assertInstanceOf, assertStringIncludes } from '@std/assert';
import {
  CompilationError,
  CompilationTimeoutError,
  CompilerError,
  ConfigNotFoundError,
  ConfigParseError,
  ErrorCode,
  ErrorSeverity,
  isCompilerError,
  isRecoverable,
  PathTraversalError,
  ResourceLimitError,
  ShutdownError,
  wrapError,
} from './errors.ts';

// CompilerError tests
Deno.test('CompilerError - creates error with all properties', () => {
  const error = new CompilerError(
    'Test error',
    ErrorCode.COMPILATION_FAILED,
    ErrorSeverity.ERROR,
    { filePath: '/test/path' },
  );

  assertEquals(error.message, 'Test error');
  assertEquals(error.code, ErrorCode.COMPILATION_FAILED);
  assertEquals(error.severity, ErrorSeverity.ERROR);
  assertEquals(error.context.filePath, '/test/path');
  assertInstanceOf(error.timestamp, Date);
  assertEquals(error.name, 'CompilerError');
});

Deno.test('CompilerError - uses default severity', () => {
  const error = new CompilerError('Test', ErrorCode.COMPILATION_FAILED);
  assertEquals(error.severity, ErrorSeverity.ERROR);
});

Deno.test('CompilerError - converts to JSON', () => {
  const error = new CompilerError('Test error', ErrorCode.CONFIG_NOT_FOUND);
  const json = error.toJSON();

  assertEquals(json.name, 'CompilerError');
  assertEquals(json.code, ErrorCode.CONFIG_NOT_FOUND);
  assertEquals(json.message, 'Test error');
  assertEquals(typeof json.timestamp, 'string');
});

Deno.test('CompilerError - creates log string', () => {
  const error = new CompilerError(
    'Test error',
    ErrorCode.COMPILATION_FAILED,
    ErrorSeverity.ERROR,
    { configName: 'test-config' },
  );

  const logStr = error.toLogString();
  assertStringIncludes(logStr, '[E2001]');
  assertStringIncludes(logStr, 'Test error');
  assertStringIncludes(logStr, 'test-config');
});

Deno.test('CompilerError - preserves cause error', () => {
  const cause = new Error('Original error');
  const error = new CompilerError(
    'Wrapped error',
    ErrorCode.COMPILATION_FAILED,
    ErrorSeverity.ERROR,
    {},
    cause,
  );

  assertEquals(error.cause, cause);
  assertEquals(error.toJSON().cause, 'Original error');
});

// ConfigNotFoundError tests
Deno.test('ConfigNotFoundError - creates with file path', () => {
  const error = new ConfigNotFoundError('/path/to/config.yaml');

  assertEquals(error.code, ErrorCode.CONFIG_NOT_FOUND);
  assertEquals(error.context.filePath, '/path/to/config.yaml');
  assertStringIncludes(error.message, '/path/to/config.yaml');
  assertEquals(error.name, 'ConfigNotFoundError');
});

// ConfigParseError tests
Deno.test('ConfigParseError - creates with format details', () => {
  const error = new ConfigParseError('/path/config.json', 'json', 'Unexpected token');

  assertEquals(error.code, ErrorCode.CONFIG_PARSE_ERROR);
  assertEquals(error.context.filePath, '/path/config.json');
  assertEquals(error.context.metadata?.format, 'json');
  assertStringIncludes(error.message, 'JSON');
  assertStringIncludes(error.message, 'Unexpected token');
});

// CompilationTimeoutError tests
Deno.test('CompilationTimeoutError - includes timeout duration', () => {
  const error = new CompilationTimeoutError(30000);

  assertEquals(error.code, ErrorCode.COMPILATION_TIMEOUT);
  assertEquals(error.timeoutMs, 30000);
  assertStringIncludes(error.message, '30000ms');
});

// PathTraversalError tests
Deno.test('PathTraversalError - has critical severity', () => {
  const error = new PathTraversalError('../../../etc/passwd');

  assertEquals(error.code, ErrorCode.PATH_TRAVERSAL_ERROR);
  assertEquals(error.severity, ErrorSeverity.CRITICAL);
  assertEquals(error.context.filePath, '../../../etc/passwd');
});

// ShutdownError tests
Deno.test('ShutdownError - has warning severity', () => {
  const error = new ShutdownError('SIGTERM');

  assertEquals(error.code, ErrorCode.SHUTDOWN_REQUESTED);
  assertEquals(error.severity, ErrorSeverity.WARNING);
  assertEquals(error.signal, 'SIGTERM');
  assertStringIncludes(error.message, 'SIGTERM');
});

// ResourceLimitError tests
Deno.test('ResourceLimitError - includes limit details', () => {
  const error = new ResourceLimitError('file size', 1000, 2000);

  assertEquals(error.code, ErrorCode.RESOURCE_LIMIT_EXCEEDED);
  assertEquals(error.resource, 'file size');
  assertEquals(error.limit, 1000);
  assertEquals(error.actual, 2000);
  assertStringIncludes(error.message, 'file size');
  assertStringIncludes(error.message, '2000 > 1000');
});

// wrapError tests
Deno.test('wrapError - returns CompilerError as-is', () => {
  const original = new CompilerError('Test', ErrorCode.COMPILATION_FAILED);
  const wrapped = wrapError(original);

  assertEquals(wrapped, original);
});

Deno.test('wrapError - wraps Error instance', () => {
  const original = new Error('Standard error');
  const wrapped = wrapError(original);

  assertInstanceOf(wrapped, CompilerError);
  assertEquals(wrapped.message, 'Standard error');
  assertEquals(wrapped.cause, original);
});

Deno.test('wrapError - wraps non-Error values', () => {
  const wrapped = wrapError('string error');

  assertInstanceOf(wrapped, CompilerError);
  assertEquals(wrapped.message, 'string error');
});

Deno.test('wrapError - uses provided error code', () => {
  const wrapped = wrapError('error', ErrorCode.CONFIG_NOT_FOUND);
  assertEquals(wrapped.code, ErrorCode.CONFIG_NOT_FOUND);
});

// isCompilerError tests
Deno.test('isCompilerError - returns true for CompilerError', () => {
  const error = new CompilerError('Test', ErrorCode.COMPILATION_FAILED);
  assertEquals(isCompilerError(error), true);
});

Deno.test('isCompilerError - returns true for subclasses', () => {
  assertEquals(isCompilerError(new ConfigNotFoundError('/path')), true);
  assertEquals(isCompilerError(new CompilationError('test')), true);
});

Deno.test('isCompilerError - returns false for standard Error', () => {
  assertEquals(isCompilerError(new Error('test')), false);
});

Deno.test('isCompilerError - returns false for non-errors', () => {
  assertEquals(isCompilerError('string'), false);
  assertEquals(isCompilerError(null), false);
  assertEquals(isCompilerError(undefined), false);
});

// isRecoverable tests
Deno.test('isRecoverable - returns true for warning severity', () => {
  const error = new ShutdownError('SIGTERM');
  assertEquals(isRecoverable(error), true);
});

Deno.test('isRecoverable - returns false for error severity', () => {
  const error = new CompilationError('Failed');
  assertEquals(isRecoverable(error), false);
});

Deno.test('isRecoverable - returns false for critical severity', () => {
  const error = new PathTraversalError('../etc/passwd');
  assertEquals(isRecoverable(error), false);
});

Deno.test('isRecoverable - returns false for non-CompilerError', () => {
  assertEquals(isRecoverable(new Error('test')), false);
});
