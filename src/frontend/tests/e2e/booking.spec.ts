import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { BookingPage } from "./pages/BookingPage";

test.describe("booking", () => {
  test("/sk/rezervacia lists session types", async ({ page }) => {
    const booking = new BookingPage(page);
    await booking.goto("/sk/rezervacia");

    await expect(booking.sessionCards()).toHaveCount(3);
  });

  test("clicking a session card opens the detail modal", async ({ page }) => {
    const booking = new BookingPage(page);
    await booking.goto("/sk/rezervacia");

    // Click the first session card CTA
    await booking.sessionCardCta(0).click();

    // Modal should be visible
    const modal = page.locator(".session-modal-overlay--open");
    await expect(modal).toBeVisible();

    // Modal contains session title and book CTA
    await expect(modal.locator(".session-modal__title")).toBeVisible();
    await expect(modal.locator(".session-modal__cta")).toBeVisible();

    // Close via X button
    await modal.locator("[data-close-modal]").click();
    await expect(modal).not.toBeVisible();
  });

  test("modal closes on Escape key", async ({ page }) => {
    const booking = new BookingPage(page);
    await booking.goto("/sk/rezervacia");

    await booking.sessionCardCta(0).click();
    await expect(page.locator(".session-modal-overlay--open")).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(page.locator(".session-modal-overlay--open")).not.toBeVisible();
  });

  test("/en/booking lists session types in English", async ({ page }) => {
    const booking = new BookingPage(page);
    await booking.goto("/en/booking");

    await expect(booking.sessionCards()).toHaveCount(3);
  });

  test("calendar page loads", async ({ page }) => {
    await page.goto("/sk/rezervacia/kalendar");

    await expect(page.locator(".calendar")).toBeVisible();
    await expect(page.locator("#session-type-select")).toBeVisible();
  });

  test("/sk/rezervacia has no critical a11y violations", async ({ page }) => {
    await page.goto("/sk/rezervacia");
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();

    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical, JSON.stringify(critical, null, 2)).toEqual([]);
  });

  test("/sk/rezervacia/kalendar has no critical a11y violations", async ({ page }) => {
    await page.goto("/sk/rezervacia/kalendar");
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();

    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical, JSON.stringify(critical, null, 2)).toEqual([]);
  });
});
