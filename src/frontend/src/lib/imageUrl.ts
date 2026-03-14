/**
 * Image URL abstraction layer.
 * Returns Azurite URLs locally, CDN URLs in production.
 */

const BLOB_BASE_URL =
  import.meta.env.PUBLIC_BLOB_BASE_URL ||
  "http://localhost:10000/devstoreaccount1";

const PROCESSED_CONTAINER = "processed";

export interface ImageVariant {
  width: number;
  height: number;
  format: string;
  blobUrl: string;
}

export interface PhotoData {
  id: string;
  slug: string;
  width: number;
  height: number;
  dominantColor?: string;
  blurhash?: string;
  altTextSk?: string;
  altTextEn?: string;
  variants: ImageVariant[];
}

/**
 * Get the URL for a specific photo variant.
 * In development, this points to Azurite.
 * In production, this would point to the CDN.
 */
export function getVariantUrl(photoId: string, width: number, format: string): string {
  return `${BLOB_BASE_URL}/${PROCESSED_CONTAINER}/${photoId}/${width}w.${format}`;
}

/**
 * Build srcset string for a given format from photo variants.
 */
export function buildSrcset(variants: ImageVariant[], format: string): string {
  return variants
    .filter((v) => v.format === format)
    .sort((a, b) => a.width - b.width)
    .map((v) => `${v.blobUrl} ${v.width}w`)
    .join(", ");
}

/**
 * Get the best fallback image URL (JPEG at 1200w, or largest available).
 */
export function getFallbackUrl(variants: ImageVariant[]): string {
  const jpegs = variants
    .filter((v) => v.format === "jpeg")
    .sort((a, b) => b.width - a.width);

  const preferred = jpegs.find((v) => v.width === 1200);
  return preferred?.blobUrl ?? jpegs[0]?.blobUrl ?? "";
}
