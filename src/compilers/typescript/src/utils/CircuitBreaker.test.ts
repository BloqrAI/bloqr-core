/**
 * Tests for CircuitBreaker
 */

import { assertEquals, assertRejects } from '@std/assert';
import { CircuitBreaker, CircuitBreakerOpenError, CircuitBreakerState } from './CircuitBreaker.ts';
import { silentLogger } from './logger.ts';

Deno.test('CircuitBreaker', async (t) => {
  await t.step('should start in CLOSED state', () => {
    const breaker = new CircuitBreaker();
    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
    assertEquals(breaker.getFailureCount(), 0);
  });

  await t.step('should execute function successfully when CLOSED', async () => {
    const breaker = new CircuitBreaker();
    const result = await breaker.execute(() => Promise.resolve('success'));
    assertEquals(result, 'success');
    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
  });

  await t.step('should increment failure count on error', async () => {
    const breaker = new CircuitBreaker({ threshold: 5 });

    await assertRejects(
      () => breaker.execute(() => Promise.reject(new Error('test error'))),
      Error,
      'test error',
    );

    assertEquals(breaker.getFailureCount(), 1);
    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
  });

  await t.step('should open circuit after threshold failures', async () => {
    const breaker = new CircuitBreaker({ threshold: 3 });

    // Fail 3 times to reach threshold
    for (let i = 0; i < 3; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
        Error,
        'fail',
      );
    }

    assertEquals(breaker.getState(), CircuitBreakerState.OPEN);
    assertEquals(breaker.getFailureCount(), 3);
  });

  await t.step('should block requests when circuit is OPEN', async () => {
    const breaker = new CircuitBreaker({ threshold: 2, timeout: 10000 });

    // Fail twice to open circuit
    for (let i = 0; i < 2; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    assertEquals(breaker.getState(), CircuitBreakerState.OPEN);

    // Next request should be blocked
    await assertRejects(
      () => breaker.execute(() => Promise.resolve('should not execute')),
      CircuitBreakerOpenError,
      'Circuit breaker is OPEN',
    );
  });

  await t.step('should transition to HALF_OPEN after timeout', async () => {
    const breaker = new CircuitBreaker({ threshold: 2, timeout: 100 }); // 100ms timeout

    // Open the circuit
    for (let i = 0; i < 2; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    assertEquals(breaker.getState(), CircuitBreakerState.OPEN);

    // Wait for timeout
    await new Promise((resolve) => setTimeout(resolve, 150));

    // Next request should transition to HALF_OPEN and succeed
    const result = await breaker.execute(() => Promise.resolve('recovery'));
    assertEquals(result, 'recovery');
    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
  });

  await t.step('should close circuit on successful recovery from HALF_OPEN', async () => {
    const breaker = new CircuitBreaker({ threshold: 2, timeout: 50 });

    // Open the circuit
    for (let i = 0; i < 2; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    // Wait for timeout
    await new Promise((resolve) => setTimeout(resolve, 100));

    // Successful request should close circuit
    const result = await breaker.execute(() => Promise.resolve('recovered'));
    assertEquals(result, 'recovered');
    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
    assertEquals(breaker.getFailureCount(), 0);
  });

  await t.step('should reopen circuit on failed recovery from HALF_OPEN', async () => {
    const breaker = new CircuitBreaker({ threshold: 2, timeout: 50 });

    // Open the circuit
    for (let i = 0; i < 2; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    // Wait for timeout to transition to HALF_OPEN
    await new Promise((resolve) => setTimeout(resolve, 100));

    // Failed recovery should reopen circuit
    await assertRejects(
      () => breaker.execute(() => Promise.reject(new Error('recovery failed'))),
      Error,
      'recovery failed',
    );

    assertEquals(breaker.getState(), CircuitBreakerState.OPEN);
  });

  await t.step('should reset circuit breaker manually', async () => {
    const breaker = new CircuitBreaker({ threshold: 2 });

    // Open the circuit
    for (let i = 0; i < 2; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    assertEquals(breaker.getState(), CircuitBreakerState.OPEN);

    // Reset manually
    breaker.reset();

    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
    assertEquals(breaker.getFailureCount(), 0);
    assertEquals(breaker.getLastFailureTime(), undefined);
  });

  await t.step('should provide accurate statistics', async () => {
    const breaker = new CircuitBreaker({ threshold: 3, timeout: 5000, name: 'test-breaker' });

    // Initial stats
    let stats = breaker.getStats();
    assertEquals(stats.state, CircuitBreakerState.CLOSED);
    assertEquals(stats.failureCount, 0);
    assertEquals(stats.threshold, 3);
    assertEquals(stats.timeout, 5000);

    // Fail once
    await assertRejects(
      () => breaker.execute(() => Promise.reject(new Error('fail'))),
    );

    stats = breaker.getStats();
    assertEquals(stats.failureCount, 1);
    assertEquals(stats.state, CircuitBreakerState.CLOSED);

    // Fail to threshold
    for (let i = 0; i < 2; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    stats = breaker.getStats();
    assertEquals(stats.state, CircuitBreakerState.OPEN);
    assertEquals(stats.failureCount, 3);
    assertEquals(stats.lastFailureTime !== undefined, true);
    assertEquals(stats.timeUntilRecovery > 0, true);
  });

  await t.step('should use custom threshold and timeout', async () => {
    const breaker = new CircuitBreaker({ threshold: 10, timeout: 2000 });

    // Should not open until 10 failures
    for (let i = 0; i < 9; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
    assertEquals(breaker.getFailureCount(), 9);

    // 10th failure should open
    await assertRejects(
      () => breaker.execute(() => Promise.reject(new Error('fail'))),
    );

    assertEquals(breaker.getState(), CircuitBreakerState.OPEN);
  });

  await t.step('should handle multiple successive successes', async () => {
    const breaker = new CircuitBreaker({ threshold: 3 });

    // Multiple successes should keep circuit closed
    for (let i = 0; i < 5; i++) {
      const result = await breaker.execute(() => Promise.resolve(`success-${i}`));
      assertEquals(result, `success-${i}`);
    }

    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
    assertEquals(breaker.getFailureCount(), 0);
  });

  await t.step('should reset failure count on success after failures', async () => {
    const breaker = new CircuitBreaker({ threshold: 5 });

    // Fail a few times
    for (let i = 0; i < 3; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    assertEquals(breaker.getFailureCount(), 3);

    // Success should reset count
    await breaker.execute(() => Promise.resolve('success'));

    assertEquals(breaker.getFailureCount(), 0);
    assertEquals(breaker.getState(), CircuitBreakerState.CLOSED);
  });

  await t.step('should use custom logger', async () => {
    // Use silentLogger to verify no errors are thrown when using a custom logger
    const breaker = new CircuitBreaker({
      threshold: 2,
      logger: silentLogger,
      name: 'test-circuit',
    });

    // Open the circuit - verify it still works with custom logger
    for (let i = 0; i < 2; i++) {
      await assertRejects(
        () => breaker.execute(() => Promise.reject(new Error('fail'))),
      );
    }

    // Verify circuit is open - this confirms logger was used without errors
    assertEquals(breaker.getState(), CircuitBreakerState.OPEN);
  });

  await t.step('should handle CircuitBreakerOpenError correctly', async () => {
    const breaker = new CircuitBreaker({ threshold: 1, timeout: 10000, name: 'test-breaker' });

    // Open circuit
    await assertRejects(
      () => breaker.execute(() => Promise.reject(new Error('fail'))),
    );

    // Try to execute while open
    try {
      await breaker.execute(() => Promise.resolve('should not run'));
    } catch (error) {
      assertEquals(error instanceof CircuitBreakerOpenError, true);
      if (error instanceof CircuitBreakerOpenError) {
        assertEquals(error.state, CircuitBreakerState.OPEN);
        assertEquals(error.name, 'CircuitBreakerOpenError');
        assertEquals(error.message.includes('Circuit breaker is OPEN'), true);
      }
    }
  });

  await t.step('should calculate time until recovery correctly', async () => {
    const breaker = new CircuitBreaker({ threshold: 1, timeout: 1000 });

    // Open circuit
    await assertRejects(
      () => breaker.execute(() => Promise.reject(new Error('fail'))),
    );

    const stats1 = breaker.getStats();
    assertEquals(stats1.timeUntilRecovery > 0, true);
    assertEquals(stats1.timeUntilRecovery <= 1000, true);

    // Wait a bit
    await new Promise((resolve) => setTimeout(resolve, 500));

    const stats2 = breaker.getStats();
    assertEquals(stats2.timeUntilRecovery < stats1.timeUntilRecovery, true);
  });

  await t.step('should handle rapid successive failures', async () => {
    const breaker = new CircuitBreaker({ threshold: 5 });

    // Rapid failures
    const promises = Array(10).fill(null).map(() =>
      breaker.execute(() => Promise.reject(new Error('fail'))).catch(() => {
        // Ignore errors for this test
      })
    );

    await Promise.all(promises);

    // Should be open after 5 failures
    assertEquals(breaker.getState(), CircuitBreakerState.OPEN);
    assertEquals(breaker.getFailureCount() >= 5, true);
  });

  await t.step('should preserve last failure time', async () => {
    const breaker = new CircuitBreaker({ threshold: 2 });

    assertEquals(breaker.getLastFailureTime(), undefined);

    // First failure
    await assertRejects(
      () => breaker.execute(() => Promise.reject(new Error('fail'))),
    );

    const firstFailureTime = breaker.getLastFailureTime();
    assertEquals(firstFailureTime !== undefined, true);

    await new Promise((resolve) => setTimeout(resolve, 50));

    // Second failure
    await assertRejects(
      () => breaker.execute(() => Promise.reject(new Error('fail'))),
    );

    const secondFailureTime = breaker.getLastFailureTime();
    assertEquals(secondFailureTime !== undefined, true);
    assertEquals(secondFailureTime! > firstFailureTime!, true);
  });
});
