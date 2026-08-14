import { test, expect } from "./fixtures";

test("homepage loads and shows the site title", async ({ page, baseURL }) => {
  await page.goto(baseURL ?? "http://localhost:8000");
  await expect(page).toHaveTitle(/AdGuard Tools and Utilities/i);
});

test("navigation links resolve without errors", async ({ page, baseURL }) => {
  await page.goto(baseURL ?? "http://localhost:8000");

  const errors: string[] = [];
  page.on("pageerror", (err) => errors.push(err.message));

  const links = await page.locator("nav a[href^='/']").all();
  expect(links.length).toBeGreaterThan(0);

  expect(errors).toEqual([]);
});
