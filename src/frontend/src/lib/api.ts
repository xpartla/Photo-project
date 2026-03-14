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
