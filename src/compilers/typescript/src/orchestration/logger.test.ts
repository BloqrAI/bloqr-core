/**
 * Tests for enhanced logger
 * Deno-native testing implementation
 */

import { assertEquals, assertStringIncludes } from '@std/assert';
import { stub } from '@std/testing/mock';
import {
  createDevelopmentLogger,
  createLogger,
  createProductionLogger,
  LogLevel,
  parseLogLevel,
} from './logger.ts';

// Helper to capture console output
function setupConsoleMocks() {
  const logs: string[] = [];
  const warns: string[] = [];
  const errors: string[] = [];
  const debugs: string[] = [];

  const logStub = stub(console, 'log', (...args: unknown[]) => {
    logs.push(String(args[0]));
  });
  const warnStub = stub(console, 'warn', (...args: unknown[]) => {
    warns.push(String(args[0]));
  });
  const errorStub = stub(console, 'error', (...args: unknown[]) => {
    errors.push(String(args[0]));
  });
  const debugStub = stub(console, 'debug', (...args: unknown[]) => {
    debugs.push(String(args[0]));
  });

  return {
    logs,
    warns,
    errors,
    debugs,
    restore: () => {
      logStub.restore();
      warnStub.restore();
      errorStub.restore();
      debugStub.restore();
    },
  };
}

// createLogger tests
Deno.test('createLogger - creates logger with default config', () => {
  const logger = createLogger();
  assertEquals(logger.config !== undefined, true);
  assertEquals(logger.config.debugEnabled, false);
  assertEquals(logger.config.jsonFormat, false);
});

Deno.test('createLogger - supports boolean parameter for backwards compatibility', () => {
  const logger = createLogger(true);
  assertEquals(logger.config.debugEnabled, true);
});

Deno.test('createLogger - logs info messages', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger();
    logger.info('Test message');

    assertEquals(mocks.logs.length > 0, true);
    assertStringIncludes(mocks.logs[0], '[INFO]');
    assertStringIncludes(mocks.logs[0], 'Test message');
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - logs warn messages', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger();
    logger.warn('Warning message');

    assertEquals(mocks.warns.length > 0, true);
    assertStringIncludes(mocks.warns[0], '[WARN]');
    assertStringIncludes(mocks.warns[0], 'Warning message');
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - logs error messages', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger();
    logger.error('Error message');

    assertEquals(mocks.errors.length > 0, true);
    assertStringIncludes(mocks.errors[0], '[ERROR]');
    assertStringIncludes(mocks.errors[0], 'Error message');
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - does not log debug when disabled', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger({ debugEnabled: false });
    logger.debug('Debug message');

    assertEquals(mocks.debugs.length, 0);
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - logs debug when enabled', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger({ debugEnabled: true, minLevel: LogLevel.DEBUG });
    logger.debug('Debug message');

    assertEquals(mocks.debugs.length > 0, true);
    assertStringIncludes(mocks.debugs[0], '[DEBUG]');
    assertStringIncludes(mocks.debugs[0], 'Debug message');
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - outputs JSON format when configured', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger({ jsonFormat: true });
    logger.info('Test message');

    assertEquals(mocks.logs.length > 0, true);
    // Should be valid JSON
    const parsed = JSON.parse(mocks.logs[0]);
    assertEquals(parsed.level, 'INFO');
    assertEquals(parsed.message, 'Test message');
    assertEquals(parsed.timestamp !== undefined, true);
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - includes app name in JSON output', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger({ jsonFormat: true, appName: 'my-app' });
    logger.info('Test');

    const parsed = JSON.parse(mocks.logs[0]);
    assertEquals(parsed.app, 'my-app');
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - respects minimum log level', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger({ minLevel: LogLevel.WARN });

    logger.info('Info');
    logger.warn('Warn');

    assertEquals(mocks.logs.length, 0);
    assertEquals(mocks.warns.length > 0, true);
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - creates child logger with additional context', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger({ jsonFormat: true });
    const child = logger.child({ requestId: '123' });

    child.info('Test');

    const parsed = JSON.parse(mocks.logs[0]);
    assertEquals(parsed.context.requestId, '123');
  } finally {
    mocks.restore();
  }
});

Deno.test('createLogger - checks if log level is enabled', () => {
  const logger = createLogger({ minLevel: LogLevel.WARN });

  assertEquals(logger.isLevelEnabled(LogLevel.INFO), false);
  assertEquals(logger.isLevelEnabled(LogLevel.WARN), true);
  assertEquals(logger.isLevelEnabled(LogLevel.ERROR), true);
});

Deno.test('createLogger - includes extra args in output', () => {
  const mocks = setupConsoleMocks();
  try {
    const logger = createLogger({ jsonFormat: true });
    logger.info('Test', { extra: 'data' });

    const parsed = JSON.parse(mocks.logs[0]);
    assertEquals(parsed.args !== undefined, true);
    assertEquals(parsed.args[0].extra, 'data');
  } finally {
    mocks.restore();
  }
});

// createProductionLogger tests
Deno.test('createProductionLogger - creates JSON-formatted logger', () => {
  const logger = createProductionLogger();
  assertEquals(logger.config.jsonFormat, true);
  assertEquals(logger.config.debugEnabled, false);
});

Deno.test('createProductionLogger - uses custom app name', () => {
  const logger = createProductionLogger('custom-app');
  assertEquals(logger.config.appName, 'custom-app');
});

// createDevelopmentLogger tests
Deno.test('createDevelopmentLogger - enables debug logging', () => {
  const logger = createDevelopmentLogger();
  assertEquals(logger.config.debugEnabled, true);
  assertEquals(logger.config.jsonFormat, false);
  assertEquals(logger.config.minLevel, LogLevel.DEBUG);
});

// parseLogLevel tests
Deno.test('parseLogLevel - parses valid log levels', () => {
  assertEquals(parseLogLevel('DEBUG'), LogLevel.DEBUG);
  assertEquals(parseLogLevel('INFO'), LogLevel.INFO);
  assertEquals(parseLogLevel('WARN'), LogLevel.WARN);
  assertEquals(parseLogLevel('ERROR'), LogLevel.ERROR);
  assertEquals(parseLogLevel('SILENT'), LogLevel.SILENT);
});

Deno.test('parseLogLevel - is case-insensitive', () => {
  assertEquals(parseLogLevel('debug'), LogLevel.DEBUG);
  assertEquals(parseLogLevel('Info'), LogLevel.INFO);
  assertEquals(parseLogLevel('WARNING'), LogLevel.WARN);
});

Deno.test('parseLogLevel - defaults to INFO for unknown levels', () => {
  assertEquals(parseLogLevel('unknown'), LogLevel.INFO);
  assertEquals(parseLogLevel(''), LogLevel.INFO);
});

// LogLevel ordering tests
Deno.test('LogLevel - has correct ordering', () => {
  assertEquals(LogLevel.DEBUG < LogLevel.INFO, true);
  assertEquals(LogLevel.INFO < LogLevel.WARN, true);
  assertEquals(LogLevel.WARN < LogLevel.ERROR, true);
  assertEquals(LogLevel.ERROR < LogLevel.SILENT, true);
});
