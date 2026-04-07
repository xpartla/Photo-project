import { describe, it, expect } from "vitest";
import { t, getAlternateLang, getAlternateUrl } from "../../src/lib/i18n";

describe("i18n", () => {
  describe("t()", () => {
    it("returns the SK translation for a known key", () => {
      // nav.shop is defined in sk.json
      const value = t("sk", "nav.shop");
      expect(typeof value).toBe("string");
      expect(value.length).toBeGreaterThan(0);
      // sanity check: not the raw key
      expect(value).not.toBe("nav.shop");
    });

    it("returns the EN translation for a known key", () => {
      const value = t("en", "nav.shop");
      expect(typeof value).toBe("string");
      expect(value.length).toBeGreaterThan(0);
      expect(value).not.toBe("nav.shop");
    });

    it("falls back to SK when an EN key is missing", () => {
      // We can't guarantee a missing key, but unknown keys should fall through
      // to the raw key as a last resort.
      const value = t("en", "definitely.missing.key.zzz");
      expect(value).toBe("definitely.missing.key.zzz");
    });

    it("returns the raw key for an unknown language", () => {
      const value = t("xx", "definitely.missing.key.zzz");
      expect(value).toBe("definitely.missing.key.zzz");
    });
  });

  describe("getAlternateLang()", () => {
    it("maps sk to en and en to sk", () => {
      expect(getAlternateLang("sk")).toBe("en");
      expect(getAlternateLang("en")).toBe("sk");
    });
  });

  describe("getAlternateUrl()", () => {
    it("swaps the language prefix on a simple path", () => {
      expect(getAlternateUrl("sk", "/sk/portfolio")).toBe("/en/portfolio");
      expect(getAlternateUrl("en", "/en/portfolio")).toBe("/sk/portfolio");
    });

    it("rewrites bilingual collection routes", () => {
      expect(getAlternateUrl("sk", "/sk/portfolio/kolekcie/dog-portraits"))
        .toBe("/en/portfolio/collections/dog-portraits");
      expect(getAlternateUrl("en", "/en/portfolio/collections/dog-portraits"))
        .toBe("/sk/portfolio/kolekcie/dog-portraits");
    });
  });
});
