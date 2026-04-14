/**
 * Cart utilities — localStorage for anonymous users, server sync for authenticated.
 */

const CART_KEY = "partlphoto_cart";
const API_BASE = "http://localhost:5000";

export interface LocalCartItem {
  productSlug: string;
  productId?: string;
  title: string;
  price: number;
  currency: string;
  imageUrl: string;
  editionSize: number | null;
  editionSold: number;
  quantity: number;
}

// ── localStorage operations ──────────────────────────────────────

export function getLocalCart(): LocalCartItem[] {
  if (typeof localStorage === "undefined") return [];
  try {
    return JSON.parse(localStorage.getItem(CART_KEY) || "[]");
  } catch {
    return [];
  }
}

export function addToLocalCart(item: Omit<LocalCartItem, "quantity"> & { quantity?: number }): void {
  const cart = getLocalCart();
  const existing = cart.find((ci) => ci.productSlug === item.productSlug);
  if (existing) {
    existing.quantity += item.quantity ?? 1;
  } else {
    cart.push({ ...item, quantity: item.quantity ?? 1 });
  }
  localStorage.setItem(CART_KEY, JSON.stringify(cart));
}

export function removeFromLocalCart(slug: string): void {
  const cart = getLocalCart().filter((ci) => ci.productSlug !== slug);
  localStorage.setItem(CART_KEY, JSON.stringify(cart));
}

export function updateLocalCartQuantity(slug: string, quantity: number): void {
  const cart = getLocalCart();
  const item = cart.find((ci) => ci.productSlug === slug);
  if (item) {
    if (quantity <= 0) {
      removeFromLocalCart(slug);
      return;
    }
    item.quantity = quantity;
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
  }
}

export function clearLocalCart(): void {
  localStorage.setItem(CART_KEY, "[]");
}

export function getLocalCartCount(): number {
  return getLocalCart().reduce((sum, item) => sum + item.quantity, 0);
}

export function getLocalCartTotal(): number {
  return getLocalCart().reduce((sum, item) => sum + item.price * item.quantity, 0);
}

// ── Server sync ──────────────────────────────────────────────────

export async function syncCartToServer(token: string, apiBase?: string): Promise<void> {
  const cart = getLocalCart();
  if (cart.length === 0) return;

  const base = apiBase || API_BASE;
  const items = cart.map((ci) => ({
    productSlug: ci.productSlug,
    quantity: ci.quantity,
  }));

  const res = await fetch(`${base}/api/shop/cart/sync`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ items }),
  });

  if (res.ok) {
    clearLocalCart();
  }
}

// ── Display helpers ──────────────────────────────────────────────

export function formatEditionBadge(
  editionSize: number | null | undefined,
  editionSold: number,
  labels: { remaining: string; soldOut: string; openEdition: string }
): string {
  if (editionSize == null) return labels.openEdition;
  const remaining = editionSize - editionSold;
  if (remaining <= 0) return labels.soldOut;
  return `${remaining} ${labels.remaining}`;
}

export function formatPrice(price: number, currency: string): string {
  return `${price} ${currency}`;
}
