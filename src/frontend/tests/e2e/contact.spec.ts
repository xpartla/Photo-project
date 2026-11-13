import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test.describe("contact", () => {
  test("/sk/kontakt shows e-mail, phone and a booking link", async ({ page }) => {
    const response = await page.goto("/sk/kontakt");
    expect(response?.status()).toBe(200);

    await expect(page.locator(".pf-hero__title")).toHaveText("Kontakt");
    await expect(page.locator(".contact__card")).toHaveCount(3);
    await expect(page.locator(".contact a[href^='mailto:']")).toBeVisible();
    await expect(page.locator(".contact a[href^='tel:']")).toBeVisible();
    await expect(page.locator(".contact__cta-btn")).toHaveAttribute("href", "/sk/rezervacia");
  });

  test("/en/contact renders in English and points hreflang at the Slovak page", async ({ page }) => {
    const response = await page.goto("/en/contact");
    expect(response?.status()).toBe(200);

    await expect(page.locator(".pf-hero__title")).toHaveText("Contact");
    await expect(page.locator(".contact__cta-btn")).toHaveAttribute("href", "/en/booking");
    await expect(page.locator("link[rel=alternate][hreflang=sk]")).toHaveAttribute("href", /\/sk\/kontakt$/);
  });

  test("the navbar contact link leads to the page", async ({ page }) => {
    await page.goto("/sk");
    await page.locator(".navbar a[href='/sk/kontakt']").first().click();
    await expect(page).toHaveURL(/\/sk\/kontakt$/);
    await expect(page.locator(".pf-hero__title")).toHaveText("Kontakt");
  });

  test("/sk/kontakt has no accessibility violations", async ({ page }) => {
    await page.goto("/sk/kontakt");
    const results = await new AxeBuilder({ page })
      .include(".contact")
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();

    expect(results.violations, JSON.stringify(results.violations, null, 2)).toEqual([]);
  });
});
