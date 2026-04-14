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
