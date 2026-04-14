import { BasePage } from "./BasePage";
import type { Locator } from "@playwright/test";

export class ShopPage extends BasePage {
  // ── Product listing ─────────────────────────────────────────
  productCards(): Locator {
    return this.page.locator(".product-card");
  }

  productCardTitle(index: number): Locator {
    return this.page.locator(".product-card__title").nth(index);
  }

  filterBtn(filter: string): Locator {
    return this.page.locator(`.shop__filter-btn[data-filter="${filter}"]`);
  }

  // ── Product detail ──────────────────────────────────────────
  detailTitle(): Locator {
    return this.page.locator(".product-detail__title");
  }

  detailPrice(): Locator {
    return this.page.locator(".product-detail__price");
  }

  detailEdition(): Locator {
    return this.page.locator(".product-detail__edition");
  }

  addToCartBtn(): Locator {
    return this.page.locator("#add-to-cart-btn");
  }

  qualityStrip(): Locator {
    return this.page.locator(".product-quality");
  }

  // ── Cart ────────────────────────────────────────────────────
  cartTitle(): Locator {
    return this.page.locator(".cart__title");
  }

  cartItems(): Locator {
    return this.page.locator(".cart-item");
  }

  cartEmpty(): Locator {
    return this.page.locator(".cart__empty");
  }

  cartCheckoutBtn(): Locator {
    return this.page.locator(".cart__checkout-btn");
  }

  // ── Navbar cart badge ───────────────────────────────────────
  cartBadge(): Locator {
    return this.page.locator("#cart-count-badge");
  }
}
