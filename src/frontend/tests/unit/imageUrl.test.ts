import { describe, it, expect } from "vitest";
import { buildSrcset, getFallbackUrl, getVariantUrl, type ImageVariant } from "../../src/lib/imageUrl";

const variants: ImageVariant[] = [
  { width: 400, height: 300, format: "webp", blobUrl: "http://azurite:10000/devstoreaccount1/processed/abc/400w.webp" },
  { width: 800, height: 600, format: "webp", blobUrl: "http://azurite:10000/devstoreaccount1/processed/abc/800w.webp" },
  { width: 1200, height: 900, format: "webp", blobUrl: "http://azurite:10000/devstoreaccount1/processed/abc/1200w.webp" },
  { width: 400, height: 300, format: "jpeg", blobUrl: "http://azurite:10000/devstoreaccount1/processed/abc/400w.jpeg" },
  { width: 1200, height: 900, format: "jpeg", blobUrl: "http://azurite:10000/devstoreaccount1/processed/abc/1200w.jpeg" },
];

describe("imageUrl", () => {
  describe("buildSrcset()", () => {
    it("includes only the requested format and rewrites azurite hostnames", () => {
      const srcset = buildSrcset(variants, "webp");
      expect(srcset).toContain("400w");
      expect(srcset).toContain("800w");
      expect(srcset).toContain("1200w");
      expect(srcset).not.toContain("jpeg");
      // No internal docker hostname should leak to the browser.
      expect(srcset).not.toContain("azurite:10000");
      expect(srcset).toContain("localhost:10000");
    });

    it("sorts widths ascending", () => {
      const srcset = buildSrcset(variants, "webp");
      const widthOrder = [...srcset.matchAll(/(\d+)w/g)].map((m) => Number(m[1]));
      expect(widthOrder).toEqual([...widthOrder].sort((a, b) => a - b));
    });
  });

  describe("getFallbackUrl()", () => {
    it("prefers the 1200w jpeg variant", () => {
      const url = getFallbackUrl(variants);
      expect(url).toContain("1200w.jpeg");
      expect(url).not.toContain("azurite:10000");
    });

    it("returns empty string when no variants exist", () => {
      expect(getFallbackUrl([])).toBe("");
    });
  });

  describe("getVariantUrl()", () => {
    it("builds a URL from photo id, width and format", () => {
      const url = getVariantUrl("abc", 800, "webp");
      expect(url).toContain("/processed/abc/800w.webp");
    });
  });
});
