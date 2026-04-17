import type { Locator } from "@playwright/test";
import { BasePage } from "./BasePage";

export class BlogPage extends BasePage {
  postCards(): Locator {
    return this.page.locator(".blog-card");
  }

  filterSummary(): Locator {
    return this.page.locator(".blog-filters-wrap__summary");
  }

  searchInput(): Locator {
    return this.page.locator('.blog-filters input[name="q"]');
  }

  categorySelect(): Locator {
    return this.page.locator('.blog-filters select[name="category"]');
  }

  submitButton(): Locator {
    return this.page.locator(".blog-filters__submit");
  }

  postTitle(): Locator {
    return this.page.locator(".blog-detail__title");
  }
}
