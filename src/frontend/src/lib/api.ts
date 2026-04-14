/**
 * API client for DogPhoto backend.
 * Uses PUBLIC_API_URL in Docker, falls back to localhost for standalone dev.
 */

const API_BASE =
  import.meta.env.PUBLIC_API_URL || "http://localhost:5000";

async function fetchApi<T>(path: string): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`);
  if (!res.ok) {
    throw new Error(`API error ${res.status}: ${path}`);
  }
  return res.json() as Promise<T>;
}

// ── Types ──────────────────────────────────────────────────────────

export interface Variant {
  width: number;
  height: number;
  format: string;
  blobUrl: string;
}

export interface TagRef {
  slug: string;
  name: string;
}

export interface PhotoSummary {
  id: string;
  slug: string;
  title: string;
  altText: string;
  width: number;
  height: number;
  dominantColor?: string;
  blurhash?: string;
  variants: Variant[];
  tags: TagRef[];
}

export interface PhotoDetail extends PhotoSummary {
  titleSk?: string;
  titleEn?: string;
  description?: string;
  descriptionSk?: string;
  descriptionEn?: string;
  altTextSk?: string;
  altTextEn?: string;
  cameraSettings?: string;
  location?: string;
  shotDate?: string;
  collections: { slug: string; name: string }[];
  relatedPhotos: PhotoSummary[];
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  size: number;
  totalCount: number;
  totalPages: number;
}

export interface CollectionSummary {
  id: string;
  slug: string;
  name: string;
  nameSk: string;
  nameEn: string;
  description?: string;
  photoCount: number;
  coverPhoto?: PhotoSummary;
}

export interface CollectionDetail {
  id: string;
  slug: string;
  name: string;
  nameSk: string;
  nameEn: string;
  description?: string;
  descriptionSk?: string;
  descriptionEn?: string;
  photos: PaginatedResult<PhotoSummary>;
}

// ── Booking types ─────────────────────────────────────────────────

export interface SessionType {
  id: string;
  slug: string;
  name: string;
  nameSk: string;
  nameEn: string;
  description?: string;
  descriptionSk?: string;
  descriptionEn?: string;
  durationMinutes: number;
  basePrice: number;
  currency: string;
  category?: string;
  includes?: string[];
  maxDogs: number;
  isActive: boolean;
}

export interface AvailabilitySlot {
  id: string;
  date: string;
  startTime: string;
  endTime: string;
  isBlocked: boolean;
  isBooked: boolean;
  isPast: boolean;
}

export interface BookingConfirmation {
  id: string;
  status: string;
  sessionType: { id: string; name: string; slug: string };
  slot?: { date: string; startTime: string; endTime: string };
  clientName: string;
  clientEmail: string;
  totalPrice: number;
  currency: string;
}

export interface CreateBookingRequest {
  sessionTypeId: string;
  slotId?: string;
  clientName: string;
  clientEmail: string;
  clientPhone?: string;
  dogCount: number;
  specialRequests?: string;
}

export interface BookingDetail {
  id: string;
  status: string;
  sessionType: { id: string; name: string; slug: string };
  slot?: { date: string; startTime: string; endTime: string };
  clientName: string;
  clientEmail: string;
  clientPhone?: string;
  dogCount: number;
  specialRequests?: string;
  totalPrice: number;
  depositPaid: boolean;
  createdAt: string;
}

// ── Shop types ───────────────────────────────────────────────────

export interface ShopProduct {
  id: string;
  slug: string;
  photoId?: string;
  title: string;
  titleSk: string;
  titleEn: string;
  description?: string;
  format?: string;
  paperType?: string;
  price: number;
  currency: string;
  editionSize?: number;
  editionSold: number;
  editionRemaining?: number;
  isAvailable: boolean;
  variants: Variant[];
}

export interface ShopProductList {
  items: ShopProduct[];
  total: number;
  page: number;
  size: number;
}

export interface CartItemResponse {
  id: string;
  productId: string;
  productSlug?: string;
  title: string;
  price: number;
  quantity: number;
  editionSize?: number;
  editionSold: number;
  isAvailable: boolean;
}

export interface CartResponse {
  items: CartItemResponse[];
  total: number;
}

export interface OrderItemResponse {
  id: string;
  productId: string;
  productSlug?: string;
  productTitle: string;
  quantity: number;
  unitPrice: number;
  editionNumber?: number;
}

export interface OrderResponse {
  id: string;
  status: string;
  totalAmount: number;
  currency: string;
  shippingAddress?: string;
  billingAddress?: string;
  items: OrderItemResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateOrderResult {
  orderId: string;
  paymentId: string;
  redirectUrl: string;
  total: number;
  currency: string;
}

export interface MockPaymentInfo {
  paymentId: string;
  orderId: string;
  amount: number;
  currency: string;
  status: string;
  returnUrl: string;
  cancelUrl: string;
}

// ── Portfolio API ──────────────────────────────────────────────────

export async function getPhotos(params?: {
  page?: number;
  size?: number;
  tag?: string;
  collection?: string;
  lang?: string;
}): Promise<PaginatedResult<PhotoSummary>> {
  const qs = new URLSearchParams();
  if (params?.page) qs.set("page", String(params.page));
  if (params?.size) qs.set("size", String(params.size));
  if (params?.tag) qs.set("tag", params.tag);
  if (params?.collection) qs.set("collection", params.collection);
  if (params?.lang) qs.set("lang", params.lang);
  const query = qs.toString();
  return fetchApi(`/api/portfolio/photos${query ? `?${query}` : ""}`);
}

export async function getPhoto(slug: string, lang?: string): Promise<PhotoDetail> {
  const qs = lang ? `?lang=${lang}` : "";
  return fetchApi(`/api/portfolio/photos/${slug}${qs}`);
}

export async function getCollections(lang?: string): Promise<CollectionSummary[]> {
  const qs = lang ? `?lang=${lang}` : "";
  return fetchApi(`/api/portfolio/collections${qs}`);
}

export async function getCollection(
  slug: string,
  params?: { lang?: string; page?: number; size?: number }
): Promise<CollectionDetail> {
  const qs = new URLSearchParams();
  if (params?.lang) qs.set("lang", params.lang);
  if (params?.page) qs.set("page", String(params.page));
  if (params?.size) qs.set("size", String(params.size));
  const query = qs.toString();
  return fetchApi(`/api/portfolio/collections/${slug}${query ? `?${query}` : ""}`);
}

// ── Booking API ───────────────────────────────────────────────────

export async function getSessionTypes(lang?: string): Promise<SessionType[]> {
  const qs = lang ? `?lang=${lang}` : "";
  return fetchApi(`/api/booking/session-types${qs}`);
}

export async function getSessionType(slug: string, lang?: string): Promise<SessionType> {
  const qs = lang ? `?lang=${lang}` : "";
  return fetchApi(`/api/booking/session-types/${slug}${qs}`);
}

export async function getAvailability(month: number, year: number): Promise<AvailabilitySlot[]> {
  return fetchApi(`/api/booking/availability?month=${month}&year=${year}`);
}

export async function createBooking(data: CreateBookingRequest): Promise<BookingConfirmation> {
  const res = await fetch(`${API_BASE}/api/booking/bookings`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error((err as { error?: string }).error || `Booking failed: ${res.status}`);
  }
  return res.json() as Promise<BookingConfirmation>;
}

export async function getBooking(id: string): Promise<BookingDetail> {
  return fetchApi(`/api/booking/bookings/${id}`);
}

// ── Shop API ─────────────────────────────────────────────────────

export async function getProducts(params?: {
  lang?: string;
  format?: string;
  available?: boolean;
  photoId?: string;
  page?: number;
  size?: number;
}): Promise<ShopProductList> {
  const qs = new URLSearchParams();
  if (params?.lang) qs.set("lang", params.lang);
  if (params?.format) qs.set("format", params.format);
  if (params?.available !== undefined) qs.set("available", String(params.available));
  if (params?.photoId) qs.set("photoId", params.photoId);
  if (params?.page) qs.set("page", String(params.page));
  if (params?.size) qs.set("size", String(params.size));
  const query = qs.toString();
  return fetchApi(`/api/shop/products${query ? `?${query}` : ""}`);
}

export async function getProduct(slug: string, lang?: string): Promise<ShopProduct> {
  const qs = lang ? `?lang=${lang}` : "";
  return fetchApi(`/api/shop/products/${slug}${qs}`);
}

export async function getPaymentInfo(paymentId: string): Promise<MockPaymentInfo> {
  return fetchApi(`/api/shop/payments/${paymentId}`);
}

export async function getOrder(id: string, token: string): Promise<OrderResponse> {
  const res = await fetch(`${API_BASE}/api/shop/orders/${id}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`API error ${res.status}`);
  return res.json() as Promise<OrderResponse>;
}

export async function getMyOrders(token: string): Promise<OrderResponse[]> {
  const res = await fetch(`${API_BASE}/api/shop/my-orders`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`API error ${res.status}`);
  return res.json() as Promise<OrderResponse[]>;
}
