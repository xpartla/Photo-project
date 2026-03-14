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
    return `/${altLang}/${rest}`;
  }
  return `/${altLang}${path}`;
}
