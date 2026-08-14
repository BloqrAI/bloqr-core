import { test as base, chromium, type Browser } from "@playwright/test";

/**
 * Connects to Cloudflare Browser Run over CDP when CLOUDFLARE_BROWSER_RUN_KEY
 * is set (CI, or opt-in locally); otherwise launches Chromium locally.
 * See: https://developers.cloudflare.com/browser-run/cdp/
 */
async function launchBrowser(): Promise<Browser> {
  const key = process.env.CLOUDFLARE_BROWSER_RUN_KEY;
  const endpoint = process.env.CLOUDFLARE_BROWSER_RUN_ENDPOINT;

  if (key && endpoint) {
    const wsUrl = `${endpoint}?secret=${encodeURIComponent(key)}`;
    return chromium.connectOverCDP(wsUrl);
  }

  const executablePath = process.env.PLAYWRIGHT_CHROMIUM_PATH;
  return chromium.launch(executablePath ? { executablePath } : undefined);
}

export const test = base.extend<{}, { sharedBrowser: Browser }>({
  sharedBrowser: [
    async ({}, use) => {
      const browser = await launchBrowser();
      await use(browser);
      await browser.close();
    },
    { scope: "worker" },
  ],
  browser: async ({ sharedBrowser }, use) => {
    await use(sharedBrowser);
  },
});

export { expect } from "@playwright/test";
