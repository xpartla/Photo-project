import { test, expect, type APIRequestContext } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const API_URL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5000";

async function registerFreshCustomer(request: APIRequestContext): Promise<{ token: string; email: string }> {
  const email = `e2e-cust-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.test`;
  const res = await request.post(`${API_URL}/api/auth/register`, {
    data: { email, password: "secret-password-1234", displayName: "E2E Customer" },
  });
  expect(res.ok()).toBeTruthy();
  const { accessToken } = await res.json();
  return { token: accessToken as string, email };
}

async function primeLocalStorage(page: import("@playwright/test").Page, token: string, email: string): Promise<void> {
  await page.goto("/sk");
  await page.evaluate(
    ([t, e]) => {
      localStorage.setItem("partlphoto_token", t);
      localStorage.setItem("partlphoto_user", JSON.stringify({ id: "x", email: e, role: "Customer" }));
    },
    [token, email] as const,
  );
}

test.describe("account (customer)", () => {
  test("registration page renders and links back to login", async ({ page }) => {
    await page.goto("/sk/registracia");
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(page.locator("a[href='/sk/prihlasenie']")).toBeVisible();
  });

  test("account dashboard requires auth", async ({ page }) => {
    await page.goto("/sk/ucet");
    await page.waitForURL(/\/sk\/prihlasenie/);
    await expect(page).toHaveURL(/prihlasenie/);
  });

  test("authed customer sees the account dashboard", async ({ page, request }) => {
    const { token, email } = await registerFreshCustomer(request);
    await primeLocalStorage(page, token, email);

    await page.goto("/sk/ucet");
    await expect(page.getByRole("heading", { level: 1 })).toContainText(email);
  });

  test("address book: create → list → delete round-trip", async ({ page, request }) => {
    const { token, email } = await registerFreshCustomer(request);
    await primeLocalStorage(page, token, email);

    await page.goto("/sk/ucet/adresy");
    await page.getByRole("button", { name: /Pridať novú adresu/i }).click();

    await page.locator("#addr-label").fill("Home");
    await page.locator("#addr-name").fill("E2E Buyer");
    await page.locator("#addr-street").fill("E2E Street 1");
    await page.locator("#addr-city").fill("Bratislava");
    await page.locator("#addr-postal").fill("81101");

    await page.getByRole("button", { name: /Uložiť/i }).click();

    // Back on the list view — the new card should appear.
    const card = page.locator(".address-card", { hasText: "E2E Buyer" });
    await expect(card).toBeVisible();
    await expect(card.locator(".address-card__badge").first()).toBeVisible();
  });

  test("checkout pre-fills with the saved default shipping address", async ({ page, request }) => {
    const { token, email } = await registerFreshCustomer(request);

    // Seed an address directly via the API.
    await request.post(`${API_URL}/api/account/addresses`, {
      data: {
        label: "Home",
        name: "Prefill Buyer",
        street: "Prefill Str 9",
        city: "Bratislava",
        postalCode: "82102",
        country: "SK",
      },
      headers: { Authorization: `Bearer ${token}` },
    });

    await primeLocalStorage(page, token, email);
    await page.goto("/sk/obchod/pokladna");

    // The saved-address picker should be visible and the inline fields pre-filled.
    await expect(page.locator("#address-picker-field")).toBeVisible();
    await expect(page.locator("#ship-name")).toHaveValue("Prefill Buyer");
    await expect(page.locator("#ship-street")).toHaveValue("Prefill Str 9");
    await expect(page.locator("#ship-city")).toHaveValue("Bratislava");
    await expect(page.locator("#ship-postal")).toHaveValue("82102");
  });

  test("account dashboard has no critical a11y violations", async ({ page, request }) => {
    const { token, email } = await registerFreshCustomer(request);
    await primeLocalStorage(page, token, email);

    await page.goto("/sk/ucet");
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical).toEqual([]);
  });
});
