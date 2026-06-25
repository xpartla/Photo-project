import { defineMiddleware } from "astro:middleware";
import { STORE_ENABLED } from "./lib/features";

// While the store is hidden (STORE_ENABLED === false) every public shop route
// (/sk/obchod*, /en/shop*) and admin shop route (/sk/admin/obchod*,
// /en/admin/shop*) is redirected to the language home page, so the pages can
// never be reached by typing a URL. The page files stay in place — re-enabling
// the store is a single env-var flip (PUBLIC_STORE_ENABLED). See
// docs/store-reenable.md.
const STORE_ROUTE = /^\/(sk|en)\/(obchod|shop)(\/|$)/;
const ADMIN_STORE_ROUTE = /^\/(sk|en)\/admin\/(obchod|shop)(\/|$)/;

export const onRequest = defineMiddleware((context, next) => {
  if (!STORE_ENABLED) {
    const { pathname } = context.url;
    if (STORE_ROUTE.test(pathname) || ADMIN_STORE_ROUTE.test(pathname)) {
      const lang = pathname.startsWith("/en") ? "en" : "sk";
      return context.redirect(`/${lang}`);
    }
  }
  return next();
});
