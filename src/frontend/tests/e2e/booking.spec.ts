import { test, expect, type APIRequestContext } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { BookingPage } from "./pages/BookingPage";

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

/** Create availability slots for a given date via admin API. */
async function seedSlots(
  request: APIRequestContext,
  token: string,
  date: string,
) {
  const res = await request.post(`${API_URL}/api/booking/availability`, {
    data: {
      date,
      startTime: "09:00",
      endTime: "16:00",
      slotDurationMinutes: 90,
      breakMinutes: 0,
    },
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(res.ok()).toBeTruthy();
}

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

  // ── Full booking journey ──────────────────────────────────────

  test("full booking journey: calendar → slot → form → confirmation", async ({
    page,
    request,
  }) => {
    // Seed availability for the 15th of next month
    const now = new Date();
    const target = new Date(now.getFullYear(), now.getMonth() + 1, 15);
    const targetYear = target.getFullYear();
    const targetMonth = target.getMonth() + 1;
    const dateStr = `${targetYear}-${String(targetMonth).padStart(2, "0")}-15`;

    const token = await getAdminToken(request);
    await seedSlots(request, token, dateStr);

    // Navigate to calendar
    const booking = new BookingPage(page);
    await booking.goto("/sk/rezervacia/kalendar");
    await expect(page.locator(".calendar")).toBeVisible();

    // Navigate to target month (click next until the month label matches)
    const expectedLabel = page.locator("#cal-month-label");
    // Keep clicking next until we land on the target month
    for (let i = 0; i < 12; i++) {
      const label = await expectedLabel.textContent();
      if (label?.includes(String(targetYear)) && label?.includes(String(targetMonth).padStart(2, "0")) === false) {
        // Check if we're on the right month by looking for available days
        const availableDays = booking.calendarDays();
        if ((await availableDays.count()) > 0) break;
      }
      // If there are available days, we're likely on the right month
      if ((await booking.calendarDays().count()) > 0) break;
      await booking.calendarNextBtn().click();
      await page.waitForTimeout(300);
    }

    // Click the first available day
    const availableDay = booking.calendarDays().first();
    await expect(availableDay).toBeVisible();
    await availableDay.click();

    // Slot picker should appear
    await expect(booking.slotPicker()).toBeVisible();

    // Click the first available slot
    const slot = booking.slotButtons().first();
    await expect(slot).toBeVisible();
    await slot.click();

    // Booking form should appear
    await expect(booking.bookingFormContainer()).toBeVisible();

    // Fill the form
    await booking.formName().fill("E2E Test Client");
    await booking.formEmail().fill("e2e-test@example.com");
    await booking.formPhone().fill("+421900111222");

    // Submit
    await booking.submitButton().click();

    // Should redirect to confirmation page
    await page.waitForURL(/potvrdenie/, { timeout: 10000 });
    await expect(booking.confirmationTitle()).toBeVisible();

    // Confirmation page should contain the client name
    await expect(page.locator(".booking-confirmation__summary")).toContainText(
      "E2E Test Client",
    );
  });

  // ── Form validation ───────────────────────────────────────────

  test("booking form shows validation errors for empty required fields", async ({
    page,
    request,
  }) => {
    // Seed a slot so we can reach the form
    const now = new Date();
    const target = new Date(now.getFullYear(), now.getMonth() + 1, 16);
    const targetYear = target.getFullYear();
    const targetMonth = target.getMonth() + 1;
    const dateStr = `${targetYear}-${String(targetMonth).padStart(2, "0")}-16`;

    const token = await getAdminToken(request);
    await seedSlots(request, token, dateStr);

    const booking = new BookingPage(page);
    await booking.goto("/sk/rezervacia/kalendar");

    // Navigate to target month
    for (let i = 0; i < 12; i++) {
      if ((await booking.calendarDays().count()) > 0) break;
      await booking.calendarNextBtn().click();
      await page.waitForTimeout(300);
    }

    // Select day and slot to get the form
    await booking.calendarDays().first().click();
    await booking.slotButtons().first().click();
    await expect(booking.bookingFormContainer()).toBeVisible();

    // Submit with empty fields
    await booking.submitButton().click();

    // Should show validation errors, NOT navigate away
    await expect(booking.formError("name")).not.toBeEmpty();
    await expect(booking.formError("email")).not.toBeEmpty();
    expect(page.url()).toContain("kalendar");
  });
});
