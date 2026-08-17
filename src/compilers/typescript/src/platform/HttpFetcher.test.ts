import { assertEquals, assertRejects } from '@std/assert';
import { HttpFetcher } from './HttpFetcher.ts';

// Unit tests (no network access required)

Deno.test('HttpFetcher - canHandle should return true for http URLs', () => {
  const fetcher = new HttpFetcher();
  assertEquals(fetcher.canHandle('http://example.com'), true);
});

Deno.test('HttpFetcher - canHandle should return true for https URLs', () => {
  const fetcher = new HttpFetcher();
  assertEquals(fetcher.canHandle('https://example.com'), true);
});

Deno.test('HttpFetcher - canHandle should return false for file paths', () => {
  const fetcher = new HttpFetcher();
  assertEquals(fetcher.canHandle('/path/to/file.txt'), false);
  assertEquals(fetcher.canHandle('./relative/path.txt'), false);
  assertEquals(fetcher.canHandle('file.txt'), false);
});

Deno.test('HttpFetcher - canHandle should return false for file URLs', () => {
  const fetcher = new HttpFetcher();
  assertEquals(fetcher.canHandle('file:///path/to/file.txt'), false);
});

Deno.test('HttpFetcher - should use default options', () => {
  const fetcher = new HttpFetcher();
  // Verify it was created without error
  assertEquals(fetcher.canHandle('https://example.com'), true);
});

Deno.test('HttpFetcher - should accept custom options', () => {
  const fetcher = new HttpFetcher({
    timeout: 5000,
    userAgent: 'CustomAgent/1.0',
    allowEmptyResponse: true,
    headers: {
      'Authorization': 'Bearer token',
    },
  });
  // Verify it was created without error
  assertEquals(fetcher.canHandle('https://example.com'), true);
});

// isSafeUrl tests

Deno.test('HttpFetcher.isSafeUrl - should allow public domains', () => {
  assertEquals(HttpFetcher.isSafeUrl('https://easylist.to/easylist/easylist.txt'), true);
  assertEquals(HttpFetcher.isSafeUrl('https://example.com/filter.txt'), true);
  assertEquals(HttpFetcher.isSafeUrl('http://filters.example.org/list.txt'), true);
});

Deno.test('HttpFetcher.isSafeUrl - should allow public routable IPv4', () => {
  assertEquals(HttpFetcher.isSafeUrl('https://8.8.8.8/list.txt'), true);
  assertEquals(HttpFetcher.isSafeUrl('https://1.1.1.1/list.txt'), true);
});

Deno.test('HttpFetcher.isSafeUrl - should block localhost', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://localhost/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://127.0.0.1/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://0.0.0.0/list.txt'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block RFC 1918 private IPv4', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://10.0.0.1/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://10.255.255.255/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://192.168.0.1/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://192.168.255.255/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://172.16.0.1/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://172.31.255.255/list.txt'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block link-local and cloud metadata IPv4', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://169.254.169.254/latest/meta-data/'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://169.254.0.1/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://metadata.google.internal/computeMetadata/v1/'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block IPv6 loopback', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://[::1]/list.txt'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block IPv6 link-local (fe80::/10)', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://[fe80::1]/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://[fe80::1%25eth0]/list.txt'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block IPv6 ULA (fc00::/7)', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://[fc00::1]/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://[fd00::1]/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://[fdff:ffff::1]/list.txt'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should return false for invalid URLs', () => {
  assertEquals(HttpFetcher.isSafeUrl('not-a-url'), false);
  assertEquals(HttpFetcher.isSafeUrl(''), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block Cloudflare Workers subdomains (*.workers.dev)', () => {
  // *.workers.dev URLs are Cloudflare Worker self-references and must never be
  // valid filter-list sources — proxying them creates request loops.
  assertEquals(HttpFetcher.isSafeUrl('https://foo.workers.dev/path'), false);
  assertEquals(HttpFetcher.isSafeUrl('https://bloqr-frontend.workers.dev/favicon.png'), false);
  assertEquals(HttpFetcher.isSafeUrl('https://my-worker.workers.dev/list.txt'), false);
  // Trailing-dot form must also be rejected (RFC 1034 FQDN notation)
  assertEquals(HttpFetcher.isSafeUrl('https://foo.workers.dev./path'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block the full 127.0.0.0/8 loopback range', () => {
  // Only 127.0.0.1 used to be blocked; the whole /8 is loopback.
  assertEquals(HttpFetcher.isSafeUrl('http://127.0.0.2/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://127.1.2.3/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://127.255.255.255/list.txt'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block decimal-encoded IPv4 literals', () => {
  // 2130706433 === 127.0.0.1, 2852039166 === 169.254.169.254
  assertEquals(HttpFetcher.isSafeUrl('http://2130706433/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://2852039166/latest/meta-data/'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should block octal- and hex-encoded IPv4 literals', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://0177.0.0.1/list.txt'), false); // octal 127.0.0.1
  assertEquals(HttpFetcher.isSafeUrl('http://0x7f.0.0.1/list.txt'), false); // hex 127.0.0.1
  assertEquals(HttpFetcher.isSafeUrl('http://0x7f000001/list.txt'), false); // hex 127.0.0.1
});

Deno.test('HttpFetcher.isSafeUrl - should block short-form IPv4 literals', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://127.1/list.txt'), false); // 127.0.0.1
  assertEquals(HttpFetcher.isSafeUrl('http://10.1/list.txt'), false); // 10.0.0.1
});

Deno.test('HttpFetcher.isSafeUrl - should block CGNAT and reserved ranges', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://100.64.0.1/list.txt'), false); // CGNAT
  assertEquals(HttpFetcher.isSafeUrl('http://0.0.0.0/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://0.1.2.3/list.txt'), false); // 0.0.0.0/8
});

Deno.test('HttpFetcher.isSafeUrl - should block IPv4-mapped IPv6 loopback/metadata', () => {
  assertEquals(HttpFetcher.isSafeUrl('http://[::ffff:127.0.0.1]/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://[::ffff:169.254.169.254]/list.txt'), false);
  assertEquals(HttpFetcher.isSafeUrl('http://[::]/list.txt'), false);
});

Deno.test('HttpFetcher.isSafeUrl - should still allow public IPv4 literals in numeric forms', () => {
  // 134744072 === 8.8.8.8 (public) must remain allowed.
  assertEquals(HttpFetcher.isSafeUrl('http://134744072/list.txt'), true);
  assertEquals(HttpFetcher.isSafeUrl('https://8.8.8.8/list.txt'), true);
});

// Integration tests (require network access - marked as ignore for CI)
// Run these with: deno test --allow-net src/platform/HttpFetcher.test.ts

Deno.test({
  name: 'HttpFetcher - should fetch content from a URL',
  ignore: true, // Requires network access
  async fn() {
    const fetcher = new HttpFetcher({ timeout: 10000 });

    // Use a well-known filter list that should be accessible
    const content = await fetcher.fetch('https://easylist.to/easylist/easylist.txt');

    // Verify we got some content
    assertEquals(content.length > 0, true);
    // EasyList should contain typical filter syntax
    assertEquals(content.includes('||'), true);
  },
});

Deno.test({
  name: 'HttpFetcher - should throw on HTTP error',
  ignore: true, // Requires network access
  async fn() {
    const fetcher = new HttpFetcher({ timeout: 5000 });

    await assertRejects(
      async () => await fetcher.fetch('https://httpstat.us/404'),
      Error,
      'HTTP 404',
    );
  },
});

Deno.test({
  name: 'HttpFetcher - should throw on empty response when not allowed',
  ignore: true, // Requires network access
  async fn() {
    const fetcher = new HttpFetcher({
      timeout: 5000,
      allowEmptyResponse: false,
    });

    await assertRejects(
      async () => await fetcher.fetch('https://httpstat.us/204'),
      Error,
    );
  },
});

Deno.test({
  name: 'HttpFetcher - should allow empty response when configured',
  ignore: true, // Requires network access
  async fn() {
    const fetcher = new HttpFetcher({
      timeout: 5000,
      allowEmptyResponse: true,
    });

    const content = await fetcher.fetch('https://httpstat.us/204');
    assertEquals(content, '');
  },
});

Deno.test({
  name: 'HttpFetcher - should throw on timeout',
  ignore: true, // Requires network access
  async fn() {
    const fetcher = new HttpFetcher({
      timeout: 1, // 1ms timeout - should fail
    });

    await assertRejects(
      async () => await fetcher.fetch('https://httpstat.us/200?sleep=5000'),
      Error,
    );
  },
});

Deno.test({
  name: 'HttpFetcher - should throw on network error',
  ignore: true, // Requires network access
  async fn() {
    const fetcher = new HttpFetcher({ timeout: 5000 });

    await assertRejects(
      async () => await fetcher.fetch('https://nonexistent.invalid'),
      Error,
    );
  },
});
