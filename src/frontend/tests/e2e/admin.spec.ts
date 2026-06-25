import { test, expect, type APIRequestContext, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const API_URL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5000";

async function getAdminToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${API_URL}/api/auth/login`, {
    data: { email: "admin@dogphoto.sk", password: "admin123" },
  });
  expect(res.ok()).toBeTruthy();
  const { accessToken } = await res.json();
  return accessToken as string;
}

async function primeAdmin(page: Page, token: string): Promise<void> {
  await page.goto("/sk");
  await page.evaluate((t) => {
    localStorage.setItem("partlphoto_token", t);
    localStorage.setItem("partlphoto_user", JSON.stringify({ id: "x", email: "admin@dogphoto.sk", role: "Admin" }));
  }, token);
}

test.describe("admin dashboard", () => {
  test("renders the four section cards for an admin", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin");
    await expect(page.locator(".admin__card", { hasText: "Portfólio" })).toBeVisible();
    await expect(page.locator(".admin__card", { hasText: "Obchod" })).toBeVisible();
    await expect(page.locator(".admin__card", { hasText: "Rezervácie" })).toBeVisible();
    await expect(page.locator(".admin__card", { hasText: "Blog" })).toBeVisible();
  });
});

test.describe("admin portfolio", () => {
  test("list page renders for an admin", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/portfolio");
    await expect(page.locator(".admin__title")).toContainText(/Portfólio/i);
  });

  test("upload page shows the drop zone", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/portfolio/upload");
    await expect(page.locator(".admin-dropzone")).toBeVisible();
  });

  test("tag manager renders", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/portfolio/tags");
    await expect(page.locator("#tag-form")).toBeVisible();
  });
});

test.describe("admin shop", () => {
  test("products list renders", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/obchod");
    await expect(page.locator(".admin__title")).toContainText(/Obchod/i);
  });

  test("new-product page shows the editor form", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/obchod/new");
    await expect(page.locator("#title-sk")).toBeVisible();
    await expect(page.locator("#slug")).toBeVisible();
  });

  test("orders list renders", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/obchod/orders");
    await expect(page.locator("#status-filter")).toBeVisible();
  });

  test("lookups page shows both columns", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/obchod/lookups");
    await expect(page.locator("#format-form")).toBeVisible();
    await expect(page.locator("#paper-form")).toBeVisible();
  });
});

test.describe("admin booking", () => {
  test("hub renders three section cards", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/rezervacia");
    const cards = page.locator(".admin__card");
    await expect(cards).toHaveCount(3);
  });

  test("calendar editor renders the month grid and recurring modal trigger", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/rezervacia/calendar");
    await expect(page.locator("#calendar-grid")).toBeVisible();
    await expect(page.locator("#open-recurring")).toBeVisible();
  });

  test("session types list renders", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/rezervacia/types");
    await expect(page.locator("#types-container form").first()).toBeVisible();
  });

  test("bookings list renders with status filter", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/rezervacia/bookings");
    await expect(page.locator("#status-filter")).toBeVisible();
  });
});

test.describe("admin accessibility", () => {
  test("/sk/admin dashboard has no critical violations", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin");
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical).toEqual([]);
  });

  test("/sk/admin/obchod has no critical violations", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/obchod");
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical).toEqual([]);
  });

  test("/sk/admin/rezervacia/calendar has no critical violations", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin/rezervacia/calendar");
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical).toEqual([]);
  });
});
