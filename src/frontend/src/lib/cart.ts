/**
 * Unified cart service.
 *
 * Source of truth depends on auth state:
 *   - Anonymous: localStorage under CART_KEY
 *   - Authenticated: server via /api/shop/cart
 *
 * All UI pages (product detail, cart, checkout, navbar) go through this
 * module — never touching localStorage or the API directly — so the cart
 * can never drift between stores.
 */

const CART_KEY = "partlphoto_cart";
const TOKEN_KEY = "partlphoto_token";
const DEFAULT_API = "http://localhost:5000";

// Keys for composing a stable identity of a local cart line.
// A line is uniquely identified by (productSlug, formatCode, paperTypeCode).
export interface LocalCartItem {
  productSlug: string;
  productId?: string;
  variantId?: string;
  formatCode?: string;
  formatName?: string;
  paperTypeCode?: string;
  paperTypeName?: string;
  title: string;
  price: number;
  currency: string;
  imageUrl: string;
  editionSize: number | null;
  editionSold: number;
  quantity: number;
}

export interface CartLine {
  id?: string;
  variantId: string;
  productId?: string;
  productSlug: string;
  title: string;
  formatCode?: string;
  formatName?: string;
  paperTypeCode?: string;
  paperTypeName?: string;
  price: number;
  currency: string;
  imageUrl?: string;
  editionSize?: number | null;
  editionSold?: number;
  isAvailable: boolean;
  quantity: number;
}

export interface CartSnapshot {
  lines: CartLine[];
  total: number;
}

const apiBase = (): string => {
  if (typeof window === "undefined") return DEFAULT_API;
  const fromEnv = (import.meta as any)?.env?.PUBLIC_CLIENT_API_URL;
  return fromEnv || DEFAULT_API;
};

// Blob URLs come back with the internal Docker hostname (http://azurite:10000);
// rewrite to the browser-accessible form used elsewhere in the app.
const BLOB_BASE_URL =
  (typeof window !== "undefined" && (import.meta as any)?.env?.PUBLIC_BLOB_BASE_URL) ||
  "http://localhost:10000/devstoreaccount1";

function rewriteBlob(url: string | null | undefined): string | undefined {
  if (!url) return undefined;
  return url.replace(/^http:\/\/azurite:10000\/devstoreaccount1/, BLOB_BASE_URL);
}

const getToken = (): string | null =>
  typeof localStorage === "undefined" ? null : localStorage.getItem(TOKEN_KEY);

export const isAuthenticated = (): boolean => !!getToken();

// ── localStorage primitives ─────────────────────────────────────

function readLocal(): LocalCartItem[] {
  if (typeof localStorage === "undefined") return [];
  try {
    return JSON.parse(localStorage.getItem(CART_KEY) || "[]");
  } catch {
    return [];
  }
}

function writeLocal(items: LocalCartItem[]): void {
  if (typeof localStorage === "undefined") return;
  localStorage.setItem(CART_KEY, JSON.stringify(items));
}

function localKey(i: Pick<LocalCartItem, "productSlug" | "formatCode" | "paperTypeCode">): string {
  return `${i.productSlug}::${i.formatCode ?? ""}::${i.paperTypeCode ?? ""}`;
}

// ── Server helpers ───────────────────────────────────────────────

async function serverCart(): Promise<CartSnapshot> {
  const token = getToken();
  if (!token) return { lines: [], total: 0 };

  const res = await fetch(`${apiBase()}/api/shop/cart`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`cart fetch failed: ${res.status}`);
  const data = await res.json();
  return {
    lines: (data.items || []).map((it: any) => ({
      id: it.id,
      variantId: it.variantId,
      productId: it.productId,
      productSlug: it.productSlug,
      title: it.title,
      formatCode: it.formatCode,
      formatName: it.formatNameEn,
      paperTypeCode: it.paperTypeCode,
      paperTypeName: it.paperTypeNameEn,
      price: Number(it.price),
      currency: it.currency || "EUR",
      imageUrl: rewriteBlob(it.imageUrl),
      editionSize: it.editionSize ?? null,
      editionSold: it.editionSold,
      isAvailable: !!it.isAvailable,
      quantity: it.quantity,
    })),
    total: Number(data.total || 0),
  };
}

async function serverAdd(variantId: string, quantity = 1): Promise<void> {
  const token = getToken();
  if (!token) throw new Error("not authenticated");
  const res = await fetch(`${apiBase()}/api/shop/cart/items`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ variantId, quantity }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || `add failed: ${res.status}`);
  }
}

async function serverUpdate(itemId: string, quantity: number): Promise<void> {
  const token = getToken();
  if (!token) throw new Error("not authenticated");
  await fetch(`${apiBase()}/api/shop/cart/items/${itemId}`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ quantity }),
  });
}

async function serverRemove(itemId: string): Promise<void> {
  const token = getToken();
  if (!token) throw new Error("not authenticated");
  await fetch(`${apiBase()}/api/shop/cart/items/${itemId}`, {
    method: "DELETE",
    headers: { Authorization: `Bearer ${token}` },
  });
}

// ── Public API ────────────────────────────────────────────────────

export async function getCart(): Promise<CartSnapshot> {
  if (isAuthenticated()) return serverCart();
  const local = readLocal();
  return {
    lines: local.map((i): CartLine => ({
      variantId: i.variantId || "",
      productId: i.productId,
      productSlug: i.productSlug,
      title: i.title,
      formatCode: i.formatCode,
      formatName: i.formatName,
      paperTypeCode: i.paperTypeCode,
      paperTypeName: i.paperTypeName,
      price: i.price,
      currency: i.currency,
      imageUrl: i.imageUrl,
      editionSize: i.editionSize,
      editionSold: i.editionSold,
      isAvailable: true,
      quantity: i.quantity,
    })),
    total: local.reduce((s, i) => s + i.price * i.quantity, 0),
  };
}

export async function addToCart(
  item: Omit<LocalCartItem, "quantity"> & { quantity?: number },
): Promise<void> {
  const qty = item.quantity ?? 1;
  if (isAuthenticated()) {
    if (!item.variantId) throw new Error("variantId required when authenticated");
    await serverAdd(item.variantId, qty);
    return;
  }
  const cart = readLocal();
  const key = localKey(item);
  const existing = cart.find((c) => localKey(c) === key);
  if (existing) existing.quantity += qty;
  else cart.push({ ...item, quantity: qty });
  writeLocal(cart);
}

export async function updateQuantity(line: CartLine, quantity: number): Promise<void> {
  if (isAuthenticated()) {
    if (!line.id) throw new Error("server cart line has no id");
    if (quantity <= 0) await serverRemove(line.id);
    else await serverUpdate(line.id, quantity);
    return;
  }
  const cart = readLocal();
  const key = localKey(line);
  const idx = cart.findIndex((c) => localKey(c) === key);
  if (idx === -1) return;
  if (quantity <= 0) cart.splice(idx, 1);
  else cart[idx].quantity = quantity;
  writeLocal(cart);
}

export async function removeFromCart(line: CartLine): Promise<void> {
  await updateQuantity(line, 0);
}

export async function getCartCount(): Promise<number> {
  const cart = await getCart();
  return cart.lines.reduce((s, l) => s + l.quantity, 0);
}

// ── Login bridge ──────────────────────────────────────────────────

/**
 * Push the local cart to the server after login, then clear localStorage.
 * Safe to call even if the local cart is empty.
 */
export async function syncLocalCartToServer(token: string): Promise<void> {
  const local = readLocal();
  if (local.length === 0) return;

  const items = local
    .filter((i) => i.productSlug)
    .map((i) => ({
      productSlug: i.productSlug,
      formatCode: i.formatCode,
      paperTypeCode: i.paperTypeCode,
      variantId: i.variantId,
      quantity: i.quantity,
    }));

  const res = await fetch(`${apiBase()}/api/shop/cart/sync`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ items }),
  });
  if (res.ok) writeLocal([]);
}

// ── Display helpers (kept for compatibility with existing tests) ──

export function formatEditionBadge(
  editionSize: number | null | undefined,
  editionSold: number,
  labels: { remaining: string; soldOut: string; openEdition: string },
): string {
  if (editionSize == null) return labels.openEdition;
  const remaining = editionSize - editionSold;
  if (remaining <= 0) return labels.soldOut;
  return `${remaining} ${labels.remaining}`;
}

export function formatPrice(price: number, currency: string): string {
  return `${price} ${currency}`;
}
