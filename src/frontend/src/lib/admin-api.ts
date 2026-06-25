// Typed wrappers for admin API endpoints.
//
// Pages call these instead of fetch() so the auth header + base URL stay
// in one place and the response shape is captured.

const TOKEN_KEY = "partlphoto_token";
const DEFAULT_API = "http://localhost:5000";

const apiBase = (): string => {
  if (typeof window === "undefined") return DEFAULT_API;
  const fromEnv = (import.meta as any)?.env?.PUBLIC_CLIENT_API_URL;
  return fromEnv || DEFAULT_API;
};

const getToken = (): string | null =>
  typeof localStorage === "undefined" ? null : localStorage.getItem(TOKEN_KEY);

interface RequestOpts extends Omit<RequestInit, "body"> {
  body?: any;
  raw?: boolean;
}

function redirectToLogin(): void {
  if (typeof window === "undefined") return;
  const lang = document.documentElement.lang === "en" ? "en" : "sk";
  const loginPath = lang === "sk" ? "/sk/prihlasenie" : "/en/login";
  const here = encodeURIComponent(window.location.pathname + window.location.search);
  // Clear the stale token so the navbar + any subsequent requests reflect the
  // signed-out state immediately.
  try { localStorage.removeItem("partlphoto_token"); } catch { /* noop */ }
  try { localStorage.removeItem("partlphoto_user"); } catch { /* noop */ }
  window.location.href = `${loginPath}?return=${here}`;
}

async function request<T>(path: string, opts: RequestOpts = {}): Promise<T> {
  const token = getToken();
  if (!token) { redirectToLogin(); throw new Error("not authenticated"); }

  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
    ...(opts.headers as Record<string, string> | undefined),
  };

  let body: BodyInit | undefined;
  if (opts.body !== undefined) {
    if (opts.raw) {
      body = opts.body as BodyInit;
    } else {
      headers["Content-Type"] = "application/json";
      body = JSON.stringify(opts.body);
    }
  }

  const res = await fetch(`${apiBase()}${path}`, { ...opts, headers, body });

  // The access token is short-lived (15 min). A 401 here means the session
  // has expired mid-flight — bounce to login with a return URL so the admin
  // lands back on the same page after re-authenticating.
  if (res.status === 401) {
    redirectToLogin();
    throw new Error("session expired");
  }

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || `request failed: ${res.status}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

// ── Common types ──────────────────────────────────────────────────

export interface PhotoVariant {
  width: number;
  height: number;
  format: string;
  blobUrl: string;
}

export interface PhotoSummary {
  id: string;
  slug: string;
  title: string;
  titleSk?: string;
  titleEn?: string;
  description?: string;
  descriptionSk?: string;
  descriptionEn?: string;
  altText?: string;
  altTextSk?: string;
  altTextEn?: string;
  width: number;
  height: number;
  variants: PhotoVariant[];
  isPublished?: boolean;
  sortOrder?: number;
  location?: string;
  tags?: { slug: string; name: string }[];
}

export interface TagSummary {
  slug: string;
  name: string;
  nameSk?: string;
  nameEn?: string;
  photoCount?: number;
}

export interface CollectionSummary {
  id: string;
  slug: string;
  name: string;
  nameSk?: string;
  nameEn?: string;
  description?: string;
  descriptionSk?: string;
  descriptionEn?: string;
  coverPhoto?: { photoId: string; variants: PhotoVariant[] };
  photoCount?: number;
}

// ── Portfolio ─────────────────────────────────────────────────────

export const listPhotos = (lang: "sk" | "en" = "sk", params: { tag?: string; collection?: string; size?: number; page?: number; includeUnpublished?: boolean } = {}) => {
  const qs = new URLSearchParams({ lang, size: String(params.size ?? 100) });
  if (params.tag) qs.set("tag", params.tag);
  if (params.collection) qs.set("collection", params.collection);
  if (params.page) qs.set("page", String(params.page));
  if (params.includeUnpublished) qs.set("includeUnpublished", "true");
  return request<{ items: PhotoSummary[]; totalCount: number }>(`/api/portfolio/photos?${qs}`);
};

export const getPhoto = (slug: string, lang: "sk" | "en" = "sk", includeUnpublished = false) => {
  const qs = new URLSearchParams({ lang });
  if (includeUnpublished) qs.set("includeUnpublished", "true");
  return request<PhotoSummary & { tags?: TagSummary[] }>(`/api/portfolio/photos/${slug}?${qs}`);
};

export interface UpdatePhotoInput {
  titleSk?: string | null;
  titleEn?: string | null;
  descriptionSk?: string | null;
  descriptionEn?: string | null;
  altTextSk?: string | null;
  altTextEn?: string | null;
  location?: string | null;
  isPublished?: boolean;
  sortOrder?: number;
  tagSlugs?: string[];
}

export const updatePhoto = (id: string, input: UpdatePhotoInput) =>
  request<{ id: string; slug: string }>(`/api/portfolio/photos/${id}`, { method: "PUT", body: input });

export const deletePhoto = (id: string) =>
  request<void>(`/api/portfolio/photos/${id}`, { method: "DELETE" });

export const listTags = (lang: "sk" | "en" = "sk") =>
  request<TagSummary[]>(`/api/portfolio/tags?lang=${lang}`);

export const createTag = (input: { slug: string; nameSk: string; nameEn: string }) =>
  request<{ id: string; slug: string }>(`/api/portfolio/tags`, { method: "POST", body: input });

export const listCollections = (lang: "sk" | "en" = "sk") =>
  request<CollectionSummary[]>(`/api/portfolio/collections?lang=${lang}`);

export const getCollection = (slug: string, lang: "sk" | "en" = "sk") =>
  request<CollectionSummary & { id: string; photos: { items: PhotoSummary[] } }>(`/api/portfolio/collections/${slug}?lang=${lang}&size=200`);

export interface CollectionInput {
  slug?: string;
  nameSk?: string;
  nameEn?: string;
  descriptionSk?: string | null;
  descriptionEn?: string | null;
  coverPhotoId?: string | null;
  sortOrder?: number;
  photoIds?: string[];
}

export const createCollection = (input: CollectionInput) =>
  request<{ id: string; slug: string }>(`/api/portfolio/collections`, { method: "POST", body: input });

export const updateCollection = (id: string, input: CollectionInput) =>
  request<{ id: string; slug: string }>(`/api/portfolio/collections/${id}`, { method: "PUT", body: input });

export const deleteCollection = (id: string) =>
  request<void>(`/api/portfolio/collections/${id}`, { method: "DELETE" });

// ── Image upload (multipart) ──────────────────────────────────────

export interface UploadInput {
  file: File;
  slug: string;
  titleSk?: string;
  titleEn?: string;
  descriptionSk?: string;
  descriptionEn?: string;
  altTextSk?: string;
  altTextEn?: string;
  location?: string;
}

export async function uploadPhoto(input: UploadInput): Promise<{ id: string; slug: string }> {
  const fd = new FormData();
  fd.append("image", input.file);
  fd.append("slug", input.slug);
  if (input.titleSk) fd.append("titleSk", input.titleSk);
  if (input.titleEn) fd.append("titleEn", input.titleEn);
  if (input.descriptionSk) fd.append("descriptionSk", input.descriptionSk);
  if (input.descriptionEn) fd.append("descriptionEn", input.descriptionEn);
  if (input.altTextSk) fd.append("altTextSk", input.altTextSk);
  if (input.altTextEn) fd.append("altTextEn", input.altTextEn);
  if (input.location) fd.append("location", input.location);

  return request<{ id: string; slug: string }>(`/api/image-pipeline/upload`, {
    method: "POST",
    body: fd,
    raw: true,
  });
}

export const getUploadStatus = (id: string) =>
  request<{ isProcessed: boolean; variantCount: number }>(`/api/image-pipeline/photos/${id}/status`);

// ── Shop ──────────────────────────────────────────────────────────

export interface FormatLookup { id: string; code: string; nameSk: string; nameEn: string; }
export interface PaperLookup { id: string; code: string; nameSk: string; nameEn: string; }

export const listFormats = () => request<FormatLookup[]>(`/api/shop/formats`);
export const listPaperTypes = () => request<PaperLookup[]>(`/api/shop/paper-types`);

export const createFormat = (input: { code: string; nameSk: string; nameEn: string; displayOrder?: number }) =>
  request<FormatLookup>(`/api/shop/formats`, { method: "POST", body: input });

export const createPaperType = (input: { code: string; nameSk: string; nameEn: string; displayOrder?: number }) =>
  request<PaperLookup>(`/api/shop/paper-types`, { method: "POST", body: input });

export interface ProductVariantInput {
  formatCode: string;
  paperTypeCode: string;
  price: number;
  sku?: string | null;
  isAvailable?: boolean;
}

export interface ProductVariantSummary {
  id: string;
  formatCode: string;
  formatName: string;
  paperTypeCode: string;
  paperTypeName: string;
  price: number;
  isAvailable: boolean;
}

export interface ProductSummary {
  id: string;
  slug: string;
  photoId?: string;
  title: string;
  titleSk: string;
  titleEn: string;
  description?: string;
  descriptionSk?: string;
  descriptionEn?: string;
  currency: string;
  isLimitedEdition: boolean;
  editionSize?: number;
  editionSold: number;
  editionRemaining?: number;
  isAvailable: boolean;
  minPrice?: number;
  maxPrice?: number;
  productVariants: ProductVariantSummary[];
  variants?: PhotoVariant[];
}

export const listProducts = (lang: "sk" | "en" = "sk") =>
  request<{ items: ProductSummary[]; total: number }>(`/api/shop/products?lang=${lang}&size=100`);

export const getProductBySlug = (slug: string, lang: "sk" | "en" = "sk") =>
  request<ProductSummary>(`/api/shop/products/${slug}?lang=${lang}`);

export interface CreateProductInput {
  titleSk: string;
  titleEn: string;
  slug: string;
  variants: ProductVariantInput[];
  photoId?: string | null;
  descriptionSk?: string | null;
  descriptionEn?: string | null;
  isLimitedEdition?: boolean;
  editionSize?: number | null;
  isAvailable?: boolean;
}

export const createProduct = (input: CreateProductInput) =>
  request<{ id: string; slug: string }>(`/api/shop/products`, { method: "POST", body: input });

export interface UpdateProductInput {
  titleSk?: string;
  titleEn?: string;
  descriptionSk?: string | null;
  descriptionEn?: string | null;
  editionSize?: number;
  isAvailable?: boolean;
}

export const updateProduct = (id: string, input: UpdateProductInput) =>
  request<{ id: string; slug: string }>(`/api/shop/products/${id}`, { method: "PUT", body: input });

export const deleteProduct = (id: string) =>
  request<void>(`/api/shop/products/${id}`, { method: "DELETE" });

export const addProductVariant = (productId: string, input: ProductVariantInput) =>
  request<{ id: string; price: number }>(`/api/shop/products/${productId}/variants`, { method: "POST", body: input });

export const updateVariant = (variantId: string, input: { price?: number; sku?: string | null; isAvailable?: boolean }) =>
  request<{ id: string }>(`/api/shop/variants/${variantId}`, { method: "PUT", body: input });

export const deleteVariant = (variantId: string) =>
  request<void>(`/api/shop/variants/${variantId}`, { method: "DELETE" });

// ── Orders ────────────────────────────────────────────────────────

export interface AdminOrderItem {
  id: string;
  productId: string;
  variantId: string;
  productSlug?: string;
  productTitle: string;
  formatNameEn?: string;
  paperTypeNameEn?: string;
  quantity: number;
  unitPrice: number;
  editionNumber?: number;
}

export interface AdminOrder {
  id: string;
  status: string;
  paymentId?: string;
  totalAmount: number;
  currency: string;
  shippingAddress: string;
  billingAddress: string;
  items: AdminOrderItem[];
  createdAt: string;
  updatedAt: string;
}

export const adminListOrders = (status?: string) => {
  const qs = status ? `?status=${encodeURIComponent(status)}` : "";
  return request<AdminOrder[]>(`/api/shop/admin/orders${qs}`);
};

export const adminGetOrder = (id: string) =>
  request<AdminOrder>(`/api/shop/orders/${id}`);

export const updateOrderStatus = (id: string, status: string) =>
  request<{ orderId: string; previousStatus: string; newStatus: string }>(
    `/api/shop/orders/${id}/status`,
    { method: "PUT", body: { status } },
  );

// ── Booking ───────────────────────────────────────────────────────

export interface SessionTypeSummary {
  id: string;
  slug: string;
  name: string;
  nameSk?: string;
  nameEn?: string;
  description?: string;
  descriptionSk?: string;
  descriptionEn?: string;
  durationMinutes: number;
  basePrice: number;
  currency: string;
  category: string;
  includes?: string[];
  maxDogs: number;
  isActive: boolean;
}

export const listSessionTypes = (lang: "sk" | "en" = "sk") =>
  request<SessionTypeSummary[]>(`/api/booking/session-types?lang=${lang}`);

export interface UpdateSessionTypeInput {
  nameSk?: string;
  nameEn?: string;
  descriptionSk?: string | null;
  descriptionEn?: string | null;
  durationMinutes?: number;
  basePrice?: number;
  category?: string;
  includesJson?: string[];
  maxDogs?: number;
  isActive?: boolean;
}

export const updateSessionType = (id: string, input: UpdateSessionTypeInput) =>
  request<SessionTypeSummary>(`/api/booking/session-types/${id}`, { method: "PUT", body: input });

export interface CreateSessionTypeInput {
  slug: string;
  nameSk: string;
  nameEn: string;
  descriptionSk?: string | null;
  descriptionEn?: string | null;
  durationMinutes?: number;
  basePrice?: number;
  category?: string;
  includesJson?: string[];
  maxDogs?: number;
  isActive?: boolean;
}

export const createSessionType = (input: CreateSessionTypeInput) =>
  request<SessionTypeSummary>(`/api/booking/session-types`, { method: "POST", body: input });

export interface AvailabilitySlot {
  id: string;
  date: string;
  startTime: string;
  endTime: string;
  isBlocked: boolean;
  isBooked?: boolean;
  isPast?: boolean;
}

export const listAvailability = (month: number, year: number) =>
  request<AvailabilitySlot[]>(`/api/booking/availability?month=${month}&year=${year}`);

export const createAvailability = (input: {
  date: string;
  startTime: string;
  endTime: string;
  slotDurationMinutes?: number | null;
  breakMinutes?: number | null;
  isBlocked?: boolean;
}) => request<{ count: number; slots: AvailabilitySlot[] }>(`/api/booking/availability`, { method: "POST", body: input });

export const updateAvailability = (id: string, input: { startTime?: string; endTime?: string; isBlocked?: boolean }) =>
  request<AvailabilitySlot>(`/api/booking/availability/${id}`, { method: "PUT", body: input });

export const createRecurringAvailability = (input: {
  daysOfWeek: number[];
  fromDate: string;
  toDate: string;
  startTime: string;
  endTime: string;
  slotDurationMinutes: number;
  breakMinutes?: number | null;
}) => request<{ count: number; addedDays: number; skippedDays: number }>(`/api/booking/availability/recurring`, { method: "POST", body: input });

export interface BookingSummary {
  id: string;
  status: string;
  sessionType: { id: string; name: string; slug: string };
  slot: { date: string; startTime: string; endTime: string } | null;
  clientName: string;
  clientEmail: string;
  clientPhone?: string | null;
  dogCount: number;
  specialRequests?: string | null;
  totalPrice: number;
  depositPaid?: boolean;
  createdAt: string;
}

export const listMyBookings = () => request<BookingSummary[]>(`/api/booking/my-bookings`);

export const adminListBookings = (status?: string) => {
  const qs = status ? `?status=${encodeURIComponent(status)}` : "";
  return request<BookingSummary[]>(`/api/booking/admin/bookings${qs}`);
};

export const cancelBooking = (id: string) =>
  request<{ id: string; status: string }>(`/api/booking/bookings/${id}/cancel`, { method: "PUT" });

export const confirmBooking = (id: string) =>
  request<{ id: string; status: string }>(`/api/booking/bookings/${id}/confirm`, { method: "PUT" });

// ── Blog (for dashboard counts only) ──────────────────────────────

export const listBlogDrafts = () =>
  request<{ items: any[]; total: number }>(`/api/blog/posts?includeDrafts=true&size=100`);
