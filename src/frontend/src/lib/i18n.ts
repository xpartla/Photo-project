import skTranslations from "../i18n/sk.json";
import enTranslations from "../i18n/en.json";

const translations: Record<string, Record<string, string>> = {
  sk: skTranslations,
  en: enTranslations,
};

export function t(lang: string, key: string): string {
  return translations[lang]?.[key] ?? translations["sk"]?.[key] ?? key;
}

export function getAlternateLang(lang: string): string {
  return lang === "sk" ? "en" : "sk";
}

export function getAlternateUrl(lang: string, path: string): string {
  const altLang = getAlternateLang(lang);
  // Replace the lang prefix in the path
  if (path.startsWith(`/${lang}/`)) {
    const rest = path.slice(lang.length + 2);
    // Handle bilingual route differences
    if (lang === "sk" && rest.startsWith("portfolio/kolekcie/")) {
      return `/${altLang}/portfolio/collections/${rest.slice("portfolio/kolekcie/".length)}`;
    }
    if (lang === "en" && rest.startsWith("portfolio/collections/")) {
      return `/${altLang}/portfolio/kolekcie/${rest.slice("portfolio/collections/".length)}`;
    }
    // Booking routes: rezervacia ↔ booking
    if (lang === "sk" && rest.startsWith("rezervacia/kalendar")) {
      return `/${altLang}/booking/schedule${rest.slice("rezervacia/kalendar".length)}`;
    }
    if (lang === "en" && rest.startsWith("booking/schedule")) {
      return `/${altLang}/rezervacia/kalendar${rest.slice("booking/schedule".length)}`;
    }
    if (lang === "sk" && rest.startsWith("rezervacia/potvrdenie/")) {
      return `/${altLang}/booking/confirmation/${rest.slice("rezervacia/potvrdenie/".length)}`;
    }
    if (lang === "en" && rest.startsWith("booking/confirmation/")) {
      return `/${altLang}/rezervacia/potvrdenie/${rest.slice("booking/confirmation/".length)}`;
    }
    if (lang === "sk" && rest.startsWith("rezervacia")) {
      return `/${altLang}/booking${rest.slice("rezervacia".length)}`;
    }
    if (lang === "en" && rest.startsWith("booking")) {
      return `/${altLang}/rezervacia${rest.slice("booking".length)}`;
    }
    // Login routes: prihlasenie ↔ login
    if (lang === "sk" && rest.startsWith("prihlasenie")) {
      return `/${altLang}/login${rest.slice("prihlasenie".length)}`;
    }
    if (lang === "en" && rest.startsWith("login")) {
      return `/${altLang}/prihlasenie${rest.slice("login".length)}`;
    }
    // Registration routes: registracia ↔ register
    if (lang === "sk" && rest.startsWith("registracia")) {
      return `/${altLang}/register${rest.slice("registracia".length)}`;
    }
    if (lang === "en" && rest.startsWith("register")) {
      return `/${altLang}/registracia${rest.slice("register".length)}`;
    }
    // Account routes: ucet ↔ account (with profil ↔ profile, adresy ↔ addresses)
    if (lang === "sk" && rest.startsWith("ucet/profil")) {
      return `/${altLang}/account/profile${rest.slice("ucet/profil".length)}`;
    }
    if (lang === "en" && rest.startsWith("account/profile")) {
      return `/${altLang}/ucet/profil${rest.slice("account/profile".length)}`;
    }
    if (lang === "sk" && rest.startsWith("ucet/adresy")) {
      return `/${altLang}/account/addresses${rest.slice("ucet/adresy".length)}`;
    }
    if (lang === "en" && rest.startsWith("account/addresses")) {
      return `/${altLang}/ucet/adresy${rest.slice("account/addresses".length)}`;
    }
    if (lang === "sk" && rest.startsWith("ucet")) {
      return `/${altLang}/account${rest.slice("ucet".length)}`;
    }
    if (lang === "en" && rest.startsWith("account")) {
      return `/${altLang}/ucet${rest.slice("account".length)}`;
    }
    // Blog routes: kategoria ↔ category
    if (lang === "sk" && rest.startsWith("blog/kategoria/")) {
      return `/${altLang}/blog/category/${rest.slice("blog/kategoria/".length)}`;
    }
    if (lang === "en" && rest.startsWith("blog/category/")) {
      return `/${altLang}/blog/kategoria/${rest.slice("blog/category/".length)}`;
    }
    // Admin routes
    if (rest.startsWith("admin/blog/novy") || rest.startsWith("admin/blog/new")) {
      const suffix = rest.startsWith("admin/blog/novy") ? rest.slice("admin/blog/novy".length) : rest.slice("admin/blog/new".length);
      return altLang === "sk" ? `/${altLang}/admin/blog/novy${suffix}` : `/${altLang}/admin/blog/new${suffix}`;
    }
    if (rest.startsWith("admin/blog/upravit/") || rest.startsWith("admin/blog/edit/")) {
      const rest2 = rest.startsWith("admin/blog/upravit/") ? rest.slice("admin/blog/upravit/".length) : rest.slice("admin/blog/edit/".length);
      return altLang === "sk" ? `/${altLang}/admin/blog/upravit/${rest2}` : `/${altLang}/admin/blog/edit/${rest2}`;
    }
    // Admin shop section: obchod ↔ shop (sub-routes share English names)
    if (lang === "sk" && rest.startsWith("admin/obchod")) {
      return `/${altLang}/admin/shop${rest.slice("admin/obchod".length)}`;
    }
    if (lang === "en" && rest.startsWith("admin/shop")) {
      return `/${altLang}/admin/obchod${rest.slice("admin/shop".length)}`;
    }
    // Admin booking section: rezervacia ↔ booking (sub-routes share English names)
    if (lang === "sk" && rest.startsWith("admin/rezervacia")) {
      return `/${altLang}/admin/booking${rest.slice("admin/rezervacia".length)}`;
    }
    if (lang === "en" && rest.startsWith("admin/booking")) {
      return `/${altLang}/admin/rezervacia${rest.slice("admin/booking".length)}`;
    }
    // Shop routes: obchod ↔ shop
    if (lang === "sk" && rest.startsWith("obchod/kosik")) {
      return `/${altLang}/shop/cart${rest.slice("obchod/kosik".length)}`;
    }
    if (lang === "en" && rest.startsWith("shop/cart")) {
      return `/${altLang}/obchod/kosik${rest.slice("shop/cart".length)}`;
    }
    if (lang === "sk" && rest.startsWith("obchod/pokladna")) {
      return `/${altLang}/shop/checkout${rest.slice("obchod/pokladna".length)}`;
    }
    if (lang === "en" && rest.startsWith("shop/checkout")) {
      return `/${altLang}/obchod/pokladna${rest.slice("shop/checkout".length)}`;
    }
    if (lang === "sk" && rest.startsWith("obchod/objednavky/")) {
      return `/${altLang}/shop/orders/${rest.slice("obchod/objednavky/".length)}`;
    }
    if (lang === "en" && rest.startsWith("shop/orders/")) {
      return `/${altLang}/obchod/objednavky/${rest.slice("shop/orders/".length)}`;
    }
    if (lang === "sk" && rest.startsWith("obchod")) {
      return `/${altLang}/shop${rest.slice("obchod".length)}`;
    }
    if (lang === "en" && rest.startsWith("shop")) {
      return `/${altLang}/obchod${rest.slice("shop".length)}`;
    }
    return `/${altLang}/${rest}`;
  }
  return `/${altLang}${path}`;
}
