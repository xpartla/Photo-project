import type { Page, Locator } from "@playwright/test";

/**
 * Base Page Object — shared helpers for navigating the PartlPhoto site.
 * Subclass this in feature-specific page objects (PortfolioPage, ShopPage, …)
 * as they're added in later epics.
 */
export class BasePage {
  constructor(public readonly page: Page) {}

  async goto(path: string): Promise<void> {
    await this.page.goto(path);
  }

  navbar(): Locator {
    return this.page.locator("[data-navbar]");
  }

  brand(): Locator {
    return this.page.locator(".navbar__brand");
  }

  langSwitch(): Locator {
    return this.page.locator(".navbar__lang-btn");
  }

  loginButton(): Locator {
    return this.page.locator("[data-login-btn]").first();
  }
}
