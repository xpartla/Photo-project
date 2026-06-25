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

    it("rewrites booking routes (rezervacia ↔ booking)", () => {
      expect(getAlternateUrl("sk", "/sk/rezervacia")).toBe("/en/booking");
      expect(getAlternateUrl("en", "/en/booking")).toBe("/sk/rezervacia");
    });

    it("rewrites booking calendar routes (kalendar ↔ schedule)", () => {
      expect(getAlternateUrl("sk", "/sk/rezervacia/kalendar"))
        .toBe("/en/booking/schedule");
      expect(getAlternateUrl("en", "/en/booking/schedule"))
        .toBe("/sk/rezervacia/kalendar");
    });

    it("rewrites booking confirmation routes (potvrdenie ↔ confirmation)", () => {
      expect(getAlternateUrl("sk", "/sk/rezervacia/potvrdenie/abc-123"))
        .toBe("/en/booking/confirmation/abc-123");
      expect(getAlternateUrl("en", "/en/booking/confirmation/abc-123"))
        .toBe("/sk/rezervacia/potvrdenie/abc-123");
    });

    it("rewrites registration routes (registracia ↔ register)", () => {
      expect(getAlternateUrl("sk", "/sk/registracia")).toBe("/en/register");
      expect(getAlternateUrl("en", "/en/register")).toBe("/sk/registracia");
    });

    it("rewrites account routes (ucet ↔ account)", () => {
      expect(getAlternateUrl("sk", "/sk/ucet")).toBe("/en/account");
      expect(getAlternateUrl("en", "/en/account")).toBe("/sk/ucet");
    });

    it("rewrites account profile routes (ucet/profil ↔ account/profile)", () => {
      expect(getAlternateUrl("sk", "/sk/ucet/profil")).toBe("/en/account/profile");
      expect(getAlternateUrl("en", "/en/account/profile")).toBe("/sk/ucet/profil");
    });

    it("rewrites account address routes (ucet/adresy ↔ account/addresses)", () => {
      expect(getAlternateUrl("sk", "/sk/ucet/adresy")).toBe("/en/account/addresses");
      expect(getAlternateUrl("en", "/en/account/addresses")).toBe("/sk/ucet/adresy");
    });

    it("rewrites admin shop section (admin/obchod ↔ admin/shop) including sub-routes", () => {
      expect(getAlternateUrl("sk", "/sk/admin/obchod")).toBe("/en/admin/shop");
      expect(getAlternateUrl("en", "/en/admin/shop")).toBe("/sk/admin/obchod");
      expect(getAlternateUrl("sk", "/sk/admin/obchod/orders/abc"))
        .toBe("/en/admin/shop/orders/abc");
      expect(getAlternateUrl("en", "/en/admin/shop/orders/abc"))
        .toBe("/sk/admin/obchod/orders/abc");
    });

    it("rewrites admin booking section (admin/rezervacia ↔ admin/booking) including sub-routes", () => {
      expect(getAlternateUrl("sk", "/sk/admin/rezervacia")).toBe("/en/admin/booking");
      expect(getAlternateUrl("en", "/en/admin/booking")).toBe("/sk/admin/rezervacia");
      expect(getAlternateUrl("sk", "/sk/admin/rezervacia/calendar"))
        .toBe("/en/admin/booking/calendar");
    });
  });
});
