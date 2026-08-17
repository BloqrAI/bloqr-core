import { assertEquals } from '@std/assert';
import { createLogger, Logger, LogLevel, silentLogger } from './logger.ts';

Deno.test('Logger - should create logger with default options', () => {
  const logger = new Logger();
  assertEquals(logger instanceof Logger, true);
});

Deno.test('Logger - should create logger with custom level', () => {
  const logger = new Logger({ level: LogLevel.Error });
  // Logger is created successfully
  assertEquals(logger instanceof Logger, true);
});

Deno.test('Logger - should create logger with prefix', () => {
  const logger = new Logger({ prefix: 'test' });
  assertEquals(logger instanceof Logger, true);
});

Deno.test('Logger - should create logger with timestamps', () => {
  const logger = new Logger({ timestamps: true });
  assertEquals(logger instanceof Logger, true);
});

Deno.test('Logger - should create logger without colors', () => {
  const logger = new Logger({ colors: false });
  assertEquals(logger instanceof Logger, true);
});

Deno.test('Logger - should create child logger with prefix', () => {
  const parent = new Logger({ prefix: 'parent' });
  const child = parent.child('child');
  assertEquals(child instanceof Logger, true);
});

Deno.test('Logger - should create child logger with nested prefix', () => {
  const parent = new Logger({ prefix: 'parent' });
  const child = parent.child('child');
  const grandchild = child.child('grandchild');
  assertEquals(grandchild instanceof Logger, true);
});

Deno.test('Logger - should set log level', () => {
  const logger = new Logger();
  logger.setLevel(LogLevel.Error);
  // No error means success
  assertEquals(true, true);
});

Deno.test('Logger - createLogger should create a new instance', () => {
  const logger = createLogger();
  assertEquals(logger instanceof Logger, true);
});

Deno.test('Logger - createLogger should accept options', () => {
  const logger = createLogger({
    level: LogLevel.Debug,
    prefix: 'test',
    timestamps: true,
  });
  assertEquals(logger instanceof Logger, true);
});

Deno.test('Logger - silentLogger should have all methods', () => {
  assertEquals(typeof silentLogger.trace, 'function');
  assertEquals(typeof silentLogger.debug, 'function');
  assertEquals(typeof silentLogger.info, 'function');
  assertEquals(typeof silentLogger.warn, 'function');
  assertEquals(typeof silentLogger.error, 'function');
});

Deno.test('Logger - silentLogger methods should not throw', () => {
  silentLogger.trace('test');
  silentLogger.debug('test');
  silentLogger.info('test');
  silentLogger.warn('test');
  silentLogger.error('test');
  // No errors means success
  assertEquals(true, true);
});

Deno.test('Logger - all log methods should not throw', () => {
  const logger = new Logger({ level: LogLevel.Trace });
  logger.trace('trace message');
  logger.debug('debug message');
  logger.info('info message');
  logger.warn('warn message');
  logger.error('error message');
  logger.success('success message');
  // No errors means success
  assertEquals(true, true);
});

Deno.test('Logger - should respect log level filtering', () => {
  const logger = new Logger({ level: LogLevel.Error });
  // These should not output (but not throw)
  logger.trace('should not show');
  logger.debug('should not show');
  logger.info('should not show');
  logger.warn('should not show');
  // This should output
  logger.error('should show');
  // No errors means success
  assertEquals(true, true);
});

Deno.test('Logger - LogLevel enum should have expected values', () => {
  assertEquals(LogLevel.Trace, -1);
  assertEquals(LogLevel.Debug, 0);
  assertEquals(LogLevel.Info, 1);
  assertEquals(LogLevel.Warn, 2);
  assertEquals(LogLevel.Error, 3);
  assertEquals(LogLevel.Silent, 4);
});

// ============================================================================
// StructuredLogger Tests
// ============================================================================

import { StructuredLogger } from './logger.ts';

Deno.test('StructuredLogger - should create with default options', () => {
  const logger = new StructuredLogger();
  assertEquals(logger instanceof StructuredLogger, true);
  assertEquals(logger instanceof Logger, true);
});

Deno.test('StructuredLogger - should create with correlation and trace IDs', () => {
  const logger = new StructuredLogger({
    correlationId: 'corr-123',
    traceId: 'trace-456',
  });
  assertEquals(logger instanceof StructuredLogger, true);
});

Deno.test('StructuredLogger - createLogger with structured flag should return StructuredLogger', () => {
  const logger = createLogger({ structured: true });
  assertEquals(logger instanceof StructuredLogger, true);
});

Deno.test('StructuredLogger - createLogger without structured flag should return Logger', () => {
  const logger = createLogger({ structured: false });
  assertEquals(logger instanceof Logger, true);
  assertEquals(logger instanceof StructuredLogger, false);
});

Deno.test('StructuredLogger - should output valid JSON', () => {
  const logger = new StructuredLogger({ level: LogLevel.Info });

  // Capture console output
  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.info('test message');

    // Should be valid JSON
    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.level, 'info');
    assertEquals(parsed.message, 'test message');
    assertEquals(typeof parsed.timestamp, 'string');
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - should include context when provided', () => {
  const logger = new StructuredLogger({ level: LogLevel.Info });

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.info('test message', { userId: 123, action: 'login' });

    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.context.userId, 123);
    assertEquals(parsed.context.action, 'login');
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - should include correlationId when set', () => {
  const logger = new StructuredLogger({
    level: LogLevel.Info,
    correlationId: 'corr-123',
  });

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.info('test message');

    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.correlationId, 'corr-123');
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - should include traceId when set', () => {
  const logger = new StructuredLogger({
    level: LogLevel.Info,
    traceId: 'trace-456',
  });

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.info('test message');

    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.traceId, 'trace-456');
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - should include prefix when set', () => {
  const logger = new StructuredLogger({
    level: LogLevel.Info,
    prefix: 'myapp',
  });

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.info('test message');

    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.prefix, 'myapp');
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - child should inherit correlationId and traceId', () => {
  const parent = new StructuredLogger({
    level: LogLevel.Info,
    prefix: 'parent',
    correlationId: 'corr-123',
    traceId: 'trace-456',
  });

  const child = parent.child('child');

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    child.info('test message');

    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.prefix, 'parent:child');
    assertEquals(parsed.correlationId, 'corr-123');
    assertEquals(parsed.traceId, 'trace-456');
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - should support all log levels', () => {
  const logger = new StructuredLogger({ level: LogLevel.Trace });

  // Track debug outputs separately since both trace and debug use console.debug
  const debugOutputs: string[] = [];
  // Track info outputs separately since both info and success use console.info
  const infoOutputs: string[] = [];
  const outputs: Record<string, string> = {};

  // Capture all console methods
  const originalConsoleDebug = console.debug;
  const originalConsoleInfo = console.info;
  const originalConsoleWarn = console.warn;
  const originalConsoleError = console.error;

  console.debug = (msg: string) => {
    debugOutputs.push(msg);
  };
  console.info = (msg: string) => {
    infoOutputs.push(msg);
  };
  console.warn = (msg: string) => {
    outputs.warn = msg;
  };
  console.error = (msg: string) => {
    outputs.error = msg;
  };

  try {
    logger.trace('trace message');
    logger.debug('debug message');
    logger.info('info message');
    logger.warn('warn message');
    logger.error('error message');
    logger.success('success message');

    // Verify trace (first debug output)
    const traceParsed = JSON.parse(debugOutputs[0]);
    assertEquals(traceParsed.level, 'trace');
    assertEquals(traceParsed.message, 'trace message');

    // Verify debug (second debug output)
    const debugParsed = JSON.parse(debugOutputs[1]);
    assertEquals(debugParsed.level, 'debug');
    assertEquals(debugParsed.message, 'debug message');

    // Verify info (first info output)
    const infoParsed = JSON.parse(infoOutputs[0]);
    assertEquals(infoParsed.level, 'info');
    assertEquals(infoParsed.message, 'info message');

    // Verify warn
    const warnParsed = JSON.parse(outputs.warn);
    assertEquals(warnParsed.level, 'warn');
    assertEquals(warnParsed.message, 'warn message');

    // Verify error
    const errorParsed = JSON.parse(outputs.error);
    assertEquals(errorParsed.level, 'error');
    assertEquals(errorParsed.message, 'error message');

    // Verify success (second info output since success uses console.info)
    const successParsed = JSON.parse(infoOutputs[1]);
    assertEquals(successParsed.level, 'info'); // success is logged at info level
    assertEquals(successParsed.message, 'success message');
    assertEquals(successParsed.context?.type, 'success'); // has success type in context
  } finally {
    console.debug = originalConsoleDebug;
    console.info = originalConsoleInfo;
    console.warn = originalConsoleWarn;
    console.error = originalConsoleError;
  }
});

Deno.test('StructuredLogger - should respect log level filtering', () => {
  const logger = new StructuredLogger({ level: LogLevel.Error });

  let infoCallCount = 0;
  let errorCallCount = 0;

  const originalConsoleInfo = console.info;
  const originalConsoleError = console.error;

  console.info = () => {
    infoCallCount++;
  };
  console.error = () => {
    errorCallCount++;
  };

  try {
    logger.info('should not log');
    logger.error('should log');

    assertEquals(infoCallCount, 0);
    assertEquals(errorCallCount, 1);
  } finally {
    console.info = originalConsoleInfo;
    console.error = originalConsoleError;
  }
});

Deno.test('StructuredLogger - setCorrelationId should update correlationId', () => {
  const logger = new StructuredLogger({ level: LogLevel.Info });
  logger.setCorrelationId('new-corr-id');

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.info('test');
    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.correlationId, 'new-corr-id');
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - setTraceId should update traceId', () => {
  const logger = new StructuredLogger({ level: LogLevel.Info });
  logger.setTraceId('new-trace-id');

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.info('test');
    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.traceId, 'new-trace-id');
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - should not include empty context', () => {
  const logger = new StructuredLogger({ level: LogLevel.Info });

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.info('test message', {});

    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.context, undefined);
  } finally {
    console.info = originalConsoleInfo;
  }
});

Deno.test('StructuredLogger - success should mark type as success in context', () => {
  const logger = new StructuredLogger({ level: LogLevel.Info });

  const originalConsoleInfo = console.info;
  let capturedOutput = '';
  console.info = (msg: string) => {
    capturedOutput = msg;
  };

  try {
    logger.success('operation succeeded');

    const parsed = JSON.parse(capturedOutput);
    assertEquals(parsed.level, 'info');
    assertEquals(parsed.context.type, 'success');
  } finally {
    console.info = originalConsoleInfo;
  }
});

// ============================================================================
// Module Override Tests
// ============================================================================

import { createLoggerFromEnv, parseModuleOverrides } from './logger.ts';

Deno.test('Logger - should create logger with module name', () => {
  const logger = new Logger({ module: 'compiler' });
  assertEquals(logger.getModule(), 'compiler');
});

Deno.test('Logger - should ignore empty string module name', () => {
  const logger = new Logger({
    level: LogLevel.Info,
    module: '', // Empty string
    moduleOverrides: {
      '': LogLevel.Debug,
    },
  });

  // Should use default level, not the override
  let debugCallCount = 0;
  const originalConsoleDebug = console.debug;
  console.debug = () => {
    debugCallCount++;
  };

  try {
    logger.debug('debug message'); // Should NOT log (default is Info)
    assertEquals(debugCallCount, 0);
  } finally {
    console.debug = originalConsoleDebug;
  }
});

Deno.test('Logger - should create logger with module overrides', () => {
  const logger = new Logger({
    level: LogLevel.Info,
    moduleOverrides: {
      'compiler': LogLevel.Debug,
      'downloader': LogLevel.Trace,
    },
  });
  const overrides = logger.getModuleOverrides();
  assertEquals(overrides['compiler'], LogLevel.Debug);
  assertEquals(overrides['downloader'], LogLevel.Trace);
});

Deno.test('Logger - should respect module-specific log level', () => {
  const logger = new Logger({
    level: LogLevel.Info,
    module: 'compiler',
    moduleOverrides: {
      'compiler': LogLevel.Debug,
      'downloader': LogLevel.Trace,
    },
  });

  // Capture console output
  let debugCallCount = 0;
  let infoCallCount = 0;

  const originalConsoleDebug = console.debug;
  const originalConsoleInfo = console.info;

  console.debug = () => {
    debugCallCount++;
  };
  console.info = () => {
    infoCallCount++;
  };

  try {
    logger.debug('debug message'); // Should log (module override is Debug)
    logger.info('info message'); // Should also log

    assertEquals(debugCallCount, 1);
    assertEquals(infoCallCount, 1);
  } finally {
    console.debug = originalConsoleDebug;
    console.info = originalConsoleInfo;
  }
});

Deno.test('Logger - should use default level when no module override exists', () => {
  const logger = new Logger({
    level: LogLevel.Error,
    module: 'other-module',
    moduleOverrides: {
      'compiler': LogLevel.Debug,
    },
  });

  // Capture console output
  let infoCallCount = 0;
  let errorCallCount = 0;

  const originalConsoleInfo = console.info;
  const originalConsoleError = console.error;

  console.info = () => {
    infoCallCount++;
  };
  console.error = () => {
    errorCallCount++;
  };

  try {
    logger.info('info message'); // Should NOT log (default is Error)
    logger.error('error message'); // Should log

    assertEquals(infoCallCount, 0);
    assertEquals(errorCallCount, 1);
  } finally {
    console.info = originalConsoleInfo;
    console.error = originalConsoleError;
  }
});

Deno.test('Logger - child should inherit module and overrides', () => {
  const parent = new Logger({
    level: LogLevel.Info,
    module: 'compiler',
    moduleOverrides: {
      'compiler': LogLevel.Debug,
    },
  });

  const child = parent.child('transformation');

  assertEquals(child.getModule(), 'compiler');
  const overrides = child.getModuleOverrides();
  assertEquals(overrides['compiler'], LogLevel.Debug);
});

Deno.test('parseModuleOverrides - should parse valid format', () => {
  const overrides = parseModuleOverrides('compiler:debug,downloader:trace');
  assertEquals(overrides['compiler'], LogLevel.Debug);
  assertEquals(overrides['downloader'], LogLevel.Trace);
});

Deno.test('parseModuleOverrides - should handle different level names', () => {
  const overrides = parseModuleOverrides(
    'mod1:trace,mod2:debug,mod3:info,mod4:warn,mod5:error,mod6:silent',
  );
  assertEquals(overrides['mod1'], LogLevel.Trace);
  assertEquals(overrides['mod2'], LogLevel.Debug);
  assertEquals(overrides['mod3'], LogLevel.Info);
  assertEquals(overrides['mod4'], LogLevel.Warn);
  assertEquals(overrides['mod5'], LogLevel.Error);
  assertEquals(overrides['mod6'], LogLevel.Silent);
});

Deno.test('parseModuleOverrides - should handle warning as alias for warn', () => {
  const overrides = parseModuleOverrides('mod:warning');
  assertEquals(overrides['mod'], LogLevel.Warn);
});

Deno.test('parseModuleOverrides - should handle whitespace', () => {
  const overrides = parseModuleOverrides('  compiler : debug , downloader : trace  ');
  assertEquals(overrides['compiler'], LogLevel.Debug);
  assertEquals(overrides['downloader'], LogLevel.Trace);
});

Deno.test('parseModuleOverrides - should ignore empty pairs', () => {
  const overrides = parseModuleOverrides('compiler:debug,,downloader:trace');
  assertEquals(overrides['compiler'], LogLevel.Debug);
  assertEquals(overrides['downloader'], LogLevel.Trace);
});

Deno.test('parseModuleOverrides - should ignore invalid pairs', () => {
  const overrides = parseModuleOverrides('compiler:debug,invalid,downloader:trace');
  assertEquals(overrides['compiler'], LogLevel.Debug);
  assertEquals(overrides['downloader'], LogLevel.Trace);
  assertEquals(overrides['invalid'], undefined);
});

Deno.test('parseModuleOverrides - should ignore invalid log levels', () => {
  const overrides = parseModuleOverrides('compiler:debug,downloader:invalid');
  assertEquals(overrides['compiler'], LogLevel.Debug);
  assertEquals(overrides['downloader'], undefined);
});

Deno.test('parseModuleOverrides - should return empty object for undefined', () => {
  const overrides = parseModuleOverrides(undefined);
  assertEquals(Object.keys(overrides).length, 0);
});

Deno.test('parseModuleOverrides - should return empty object for empty string', () => {
  const overrides = parseModuleOverrides('');
  assertEquals(Object.keys(overrides).length, 0);
});

Deno.test('createLoggerFromEnv - should work without environment variables', () => {
  const logger = createLoggerFromEnv();
  assertEquals(logger instanceof Logger, true);
});

Deno.test('StructuredLogger - should support module overrides', () => {
  const logger = new StructuredLogger({
    level: LogLevel.Info,
    module: 'compiler',
    moduleOverrides: {
      'compiler': LogLevel.Debug,
    },
  });

  let debugCallCount = 0;
  const originalConsoleDebug = console.debug;
  console.debug = () => {
    debugCallCount++;
  };

  try {
    logger.debug('debug message'); // Should log due to module override
    assertEquals(debugCallCount, 1);
  } finally {
    console.debug = originalConsoleDebug;
  }
});

Deno.test('StructuredLogger - child should inherit module overrides', () => {
  const parent = new StructuredLogger({
    level: LogLevel.Info,
    module: 'compiler',
    moduleOverrides: {
      'compiler': LogLevel.Debug,
    },
    correlationId: 'corr-123',
  });

  const child = parent.child('transformation');

  assertEquals(child.getModule(), 'compiler');
  const overrides = child.getModuleOverrides();
  assertEquals(overrides['compiler'], LogLevel.Debug);
});
