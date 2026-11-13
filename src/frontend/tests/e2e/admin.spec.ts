import { test, expect, type APIRequestContext, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { STORE_ENABLED, STORE_HIDDEN_REASON } from "./store";

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
  test("renders a card for every enabled section", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);

    await page.goto("/sk/admin");
    await expect(page.locator(".admin__card", { hasText: "Portfólio" })).toBeVisible();
    await expect(page.locator(".admin__card", { hasText: "Rezervácie" })).toBeVisible();
    await expect(page.locator(".admin__card", { hasText: "Blog" })).toBeVisible();
    await expect(page.locator(".admin__card", { hasText: "Obchod" })).toHaveCount(STORE_ENABLED ? 1 : 0);
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
  test.skip(!STORE_ENABLED, STORE_HIDDEN_REASON);

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

  test("a package can be created, then deactivated without disappearing", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await primeAdmin(page, token);
    const slug = `e2e-${Date.now()}`;

    await page.goto("/sk/admin/rezervacia/types");
    await page.locator("#new-type-btn").click();
    const form = page.locator("#new-type-form");
    await form.locator("[name=slug]").fill(slug);
    await form.locator("[name=nameSk]").fill(`Balík ${slug}`);
    await form.locator("[name=nameEn]").fill(`Package ${slug}`);
    await form.locator("[name=basePrice]").fill("9999");
    await form.locator("[name=includesSk]").fill("prvá položka\ndruhá položka");
    await form.locator("[name=includesEn]").fill("first item\nsecond item");
    await form.locator("button[type=submit]").click();

    const card = page.locator("#types-container form", { hasText: `Balík ${slug}` });
    await expect(card).toBeVisible();
    await expect(card.locator("[data-field=includesSk]")).toHaveValue("prvá položka\ndruhá položka");
    await expect(card.locator("[data-field=includesEn]")).toHaveValue("first item\nsecond item");
    await expect(card.locator("[data-inactive]")).toHaveCount(0);

    await card.locator("[data-field=isActive]").uncheck();
    await card.locator("button[type=submit]").click();
    await expect(card.locator("[data-inactive]")).toBeVisible();

    // Still listed for the admin after a reload, gone from the public API.
    await page.reload();
    await expect(page.locator("#types-container form", { hasText: `Balík ${slug}` }).locator("[data-inactive]")).toBeVisible();
    const publicList = await (await request.get(`${API_URL}/api/booking/session-types`)).json();
    expect(publicList.some((st: { slug: string }) => st.slug === slug)).toBe(false);
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
    test.skip(!STORE_ENABLED, STORE_HIDDEN_REASON);

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
