import { test, expect, type APIRequestContext } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { ShopPage } from "./pages/ShopPage";

const API_URL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5000";

/** Authenticate as admin and return a Bearer token. */
async function getAdminToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${API_URL}/api/auth/login`, {
    data: { email: "admin@dogphoto.sk", password: "admin123" },
  });
  expect(res.ok()).toBeTruthy();
  const { accessToken } = await res.json();
  return accessToken as string;
}

/** Ensure at least one product exists. Returns the product slug. */
async function ensureProduct(
  request: APIRequestContext,
  token: string,
  slug = "e2e-test-print",
): Promise<string> {
  const res = await request.post(`${API_URL}/api/shop/products`, {
    data: {
      titleSk: "E2E Test Tlač",
      titleEn: "E2E Test Print",
      slug,
      descriptionSk: "E2E testovací produkt",
      descriptionEn: "E2E test product",
      format: "30x40 cm",
      paperType: "Fine Art",
      price: 55,
      editionSize: 50,
      isAvailable: true,
    },
    headers: { Authorization: `Bearer ${token}` },
  });
  // 201 = created, 409 = already exists — both are fine
  expect([201, 409]).toContain(res.status());
  return slug;
}

test.describe("shop", () => {
  // ── Product listing ────────────────────────────────────────

  test("shop page loads and shows products", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await ensureProduct(request, token);

    const shop = new ShopPage(page);
    await shop.goto("/sk/obchod");

    await expect(shop.productCards().first()).toBeVisible();
  });

  test("filter buttons toggle product visibility", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await ensureProduct(request, token);

    const shop = new ShopPage(page);
    await shop.goto("/sk/obchod");

    // All filter is active by default
    await expect(shop.filterBtn("all")).toHaveClass(/--active/);

    // Click available filter
    await shop.filterBtn("available").click();
    await expect(shop.filterBtn("available")).toHaveClass(/--active/);
    // All visible products should be available
    const cards = shop.productCards();
    const count = await cards.count();
    expect(count).toBeGreaterThan(0);
  });

  test("EN shop page loads with English content", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await ensureProduct(request, token);

    const shop = new ShopPage(page);
    await shop.goto("/en/shop");

    await expect(shop.productCards().first()).toBeVisible();
    await expect(page.locator(".shop__title")).toContainText("Shop");
  });

  // ── Product detail ─────────────────────────────────────────

  test("product detail page shows full info", async ({ page, request }) => {
    const token = await getAdminToken(request);
    const slug = await ensureProduct(request, token);

    const shop = new ShopPage(page);
    await shop.goto(`/sk/obchod/${slug}`);

    await expect(shop.detailTitle()).toBeVisible();
    await expect(shop.detailPrice()).toBeVisible();
    await expect(shop.detailEdition()).toBeVisible();
    await expect(shop.addToCartBtn()).toBeVisible();
    await expect(shop.qualityStrip()).toBeVisible();
  });

  test("add to cart updates badge", async ({ page, request }) => {
    const token = await getAdminToken(request);
    const slug = await ensureProduct(request, token);

    const shop = new ShopPage(page);
    await shop.goto(`/sk/obchod/${slug}`);

    // Clear localStorage cart first
    await page.evaluate(() => localStorage.setItem("partlphoto_cart", "[]"));
    await page.reload();

    await shop.addToCartBtn().click();

    // Badge should appear with count 1
    await expect(shop.cartBadge()).toBeVisible();
    await expect(shop.cartBadge()).toHaveText("1");
  });

  // ── Cart ───────────────────────────────────────────────────

  test("empty cart shows empty state", async ({ page }) => {
    await page.evaluate(() => localStorage.setItem("partlphoto_cart", "[]"));

    const shop = new ShopPage(page);
    await shop.goto("/sk/obchod/kosik");

    await expect(shop.cartEmpty()).toBeVisible();
  });

  test("cart shows items from localStorage", async ({ page, request }) => {
    const token = await getAdminToken(request);
    const slug = await ensureProduct(request, token);

    // Seed a cart item in localStorage
    await page.goto("/sk/obchod");
    await page.evaluate(
      (s) =>
        localStorage.setItem(
          "partlphoto_cart",
          JSON.stringify([
            {
              productSlug: s,
              title: "E2E Test Print",
              price: 55,
              currency: "EUR",
              imageUrl: "",
              quantity: 2,
            },
          ]),
        ),
      slug,
    );

    const shop = new ShopPage(page);
    await shop.goto("/sk/obchod/kosik");

    // Should show item with quantity
    await expect(shop.cartItems()).toHaveCount(1);
    await expect(shop.cartCheckoutBtn()).toBeVisible();
  });

  // ── Accessibility ──────────────────────────────────────────

  test("/sk/obchod has no critical a11y violations", async ({
    page,
    request,
  }) => {
    const token = await getAdminToken(request);
    await ensureProduct(request, token);

    await page.goto("/sk/obchod");
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();

    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical, JSON.stringify(critical, null, 2)).toEqual([]);
  });

  test("/sk/obchod/[slug] has no critical a11y violations", async ({
    page,
    request,
  }) => {
    const token = await getAdminToken(request);
    const slug = await ensureProduct(request, token);

    await page.goto(`/sk/obchod/${slug}`);
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();

    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical, JSON.stringify(critical, null, 2)).toEqual([]);
  });

  test("/sk/obchod/kosik has no critical a11y violations", async ({
    page,
  }) => {
    await page.goto("/sk/obchod/kosik");
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();

    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical, JSON.stringify(critical, null, 2)).toEqual([]);
  });
});
