import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { BasePage } from "./pages/BasePage";

test.describe("smoke", () => {
  test("home page (/) redirects to /sk", async ({ page }) => {
    const response = await page.goto("/");
    expect(response).not.toBeNull();
    // After following redirects, we should be on /sk
    expect(page.url()).toMatch(/\/sk\/?$/);
  });

  test("/sk renders with the navbar", async ({ page }) => {
    const home = new BasePage(page);
    await home.goto("/sk");

    await expect(home.navbar()).toBeVisible();
    await expect(home.brand()).toHaveText(/PartlPhoto/);
    await expect(home.langSwitch()).toHaveText(/EN/);
  });

  test("/en renders with the navbar", async ({ page }) => {
    const home = new BasePage(page);
    await home.goto("/en");

    await expect(home.navbar()).toBeVisible();
    await expect(home.langSwitch()).toHaveText(/SK/);
  });

  test("/sk has no critical accessibility violations", async ({ page }) => {
    await page.goto("/sk");
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();

    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical, JSON.stringify(critical, null, 2)).toEqual([]);
  });
});
