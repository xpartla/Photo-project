import { describe, it, expect, beforeEach } from "vitest";

// Mock localStorage for node environment
const store: Record<string, string> = {};
const mockLocalStorage = {
  getItem: (key: string) => store[key] ?? null,
  setItem: (key: string, value: string) => { store[key] = value; },
  removeItem: (key: string) => { delete store[key]; },
  clear: () => { for (const key of Object.keys(store)) delete store[key]; },
};
(globalThis as any).localStorage = mockLocalStorage;

const CART_KEY = "partlphoto_cart";

// Inline re-implementations that mirror cart.ts semantics.
// Matches the (productSlug, formatCode, paperTypeCode) identity.
function localKey(i: any): string {
  return `${i.productSlug}::${i.formatCode ?? ""}::${i.paperTypeCode ?? ""}`;
}

function getLocalCart(): any[] {
  try {
    return JSON.parse(localStorage.getItem(CART_KEY) || "[]");
  } catch {
    return [];
  }
}

function addToLocalCart(item: any): void {
  const cart = getLocalCart();
  const key = localKey(item);
  const existing = cart.find((ci) => localKey(ci) === key);
  if (existing) existing.quantity += item.quantity ?? 1;
  else cart.push({ ...item, quantity: item.quantity ?? 1 });
  localStorage.setItem(CART_KEY, JSON.stringify(cart));
}

function removeFromLocalCart(match: any): void {
  const key = localKey(match);
  const cart = getLocalCart().filter((ci: any) => localKey(ci) !== key);
  localStorage.setItem(CART_KEY, JSON.stringify(cart));
}

function updateLocalCartQuantity(match: any, quantity: number): void {
  const cart = getLocalCart();
  const key = localKey(match);
  const item = cart.find((ci: any) => localKey(ci) === key);
  if (item) {
    if (quantity <= 0) { removeFromLocalCart(match); return; }
    item.quantity = quantity;
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
  }
}

function getLocalCartTotal(cart: any[]): number {
  return cart.reduce((sum: number, item: any) => sum + item.price * item.quantity, 0);
}

function formatEditionBadge(
  editionSize: number | null,
  editionSold: number,
  labels: { remaining: string; soldOut: string; openEdition: string },
): string {
  if (editionSize == null) return labels.openEdition;
  const remaining = editionSize - editionSold;
  if (remaining <= 0) return labels.soldOut;
  return `${remaining} ${labels.remaining}`;
}

// ── Tests ─────────────────────────────────────────────────────────

describe("localStorage cart operations", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("starts with empty cart", () => {
    expect(getLocalCart()).toEqual([]);
  });

  it("adds item to cart", () => {
    addToLocalCart({ productSlug: "test-product", formatCode: "a4", paperTypeCode: "matte-200", title: "Test", price: 50, currency: "EUR" });
    const cart = getLocalCart();
    expect(cart).toHaveLength(1);
    expect(cart[0].productSlug).toBe("test-product");
    expect(cart[0].quantity).toBe(1);
  });

  it("increments quantity for existing variant", () => {
    addToLocalCart({ productSlug: "p", formatCode: "a4", paperTypeCode: "matte-200", title: "Test", price: 50, currency: "EUR" });
    addToLocalCart({ productSlug: "p", formatCode: "a4", paperTypeCode: "matte-200", title: "Test", price: 50, currency: "EUR" });
    const cart = getLocalCart();
    expect(cart).toHaveLength(1);
    expect(cart[0].quantity).toBe(2);
  });

  it("keeps different variants of same product as distinct lines", () => {
    addToLocalCart({ productSlug: "p", formatCode: "a4", paperTypeCode: "matte-200", title: "A4 Matte", price: 30, currency: "EUR" });
    addToLocalCart({ productSlug: "p", formatCode: "a3", paperTypeCode: "matte-200", title: "A3 Matte", price: 50, currency: "EUR" });
    addToLocalCart({ productSlug: "p", formatCode: "a4", paperTypeCode: "fine-art-310", title: "A4 Fine Art", price: 55, currency: "EUR" });
    const cart = getLocalCart();
    expect(cart).toHaveLength(3);
  });

  it("adds different products as separate items", () => {
    addToLocalCart({ productSlug: "product-a", formatCode: "a4", paperTypeCode: "matte-200", title: "A", price: 30, currency: "EUR" });
    addToLocalCart({ productSlug: "product-b", formatCode: "a4", paperTypeCode: "matte-200", title: "B", price: 40, currency: "EUR" });
    const cart = getLocalCart();
    expect(cart).toHaveLength(2);
  });

  it("removes item from cart", () => {
    const key = { productSlug: "to-remove", formatCode: "a4", paperTypeCode: "matte-200" };
    addToLocalCart({ ...key, title: "X", price: 10, currency: "EUR" });
    addToLocalCart({ productSlug: "to-keep", formatCode: "a4", paperTypeCode: "matte-200", title: "Y", price: 20, currency: "EUR" });
    removeFromLocalCart(key);
    const cart = getLocalCart();
    expect(cart).toHaveLength(1);
    expect(cart[0].productSlug).toBe("to-keep");
  });

  it("updates quantity", () => {
    const key = { productSlug: "qty-test", formatCode: "a4", paperTypeCode: "matte-200" };
    addToLocalCart({ ...key, title: "T", price: 25, currency: "EUR" });
    updateLocalCartQuantity(key, 5);
    expect(getLocalCart()[0].quantity).toBe(5);
  });

  it("removes item when quantity set to 0", () => {
    const key = { productSlug: "zero-qty", formatCode: "a4", paperTypeCode: "matte-200" };
    addToLocalCart({ ...key, title: "T", price: 10, currency: "EUR" });
    updateLocalCartQuantity(key, 0);
    expect(getLocalCart()).toHaveLength(0);
  });

  it("clears cart", () => {
    addToLocalCart({ productSlug: "a", formatCode: "a4", paperTypeCode: "matte-200", title: "A", price: 10, currency: "EUR" });
    addToLocalCart({ productSlug: "b", formatCode: "a4", paperTypeCode: "matte-200", title: "B", price: 20, currency: "EUR" });
    localStorage.setItem(CART_KEY, "[]");
    expect(getLocalCart()).toHaveLength(0);
  });
});

describe("cart total calculation", () => {
  it("calculates total correctly", () => {
    const cart = [
      { price: 50, quantity: 2 },
      { price: 30, quantity: 1 },
    ];
    expect(getLocalCartTotal(cart)).toBe(130);
  });

  it("returns 0 for empty cart", () => {
    expect(getLocalCartTotal([])).toBe(0);
  });
});

describe("edition display formatting", () => {
  const labels = { remaining: "remaining", soldOut: "Sold out", openEdition: "Open edition" };

  it("shows remaining count for limited editions", () => {
    expect(formatEditionBadge(10, 7, labels)).toBe("3 remaining");
  });

  it("shows sold out when edition is full", () => {
    expect(formatEditionBadge(10, 10, labels)).toBe("Sold out");
  });

  it("shows open edition when no limit", () => {
    expect(formatEditionBadge(null, 0, labels)).toBe("Open edition");
  });

  it("shows sold out when oversold", () => {
    expect(formatEditionBadge(5, 6, labels)).toBe("Sold out");
  });
});
