// The store is hidden unless the stack was started with PUBLIC_STORE_ENABLED=true
// (see docs/store-reenable.md). While hidden, every shop and admin-shop route
// redirects to the home page, so specs that need the store skip themselves.
// Pass the same variable to Playwright when the store is on:
//   PUBLIC_STORE_ENABLED=true npm run test:e2e
export const STORE_ENABLED = process.env.PUBLIC_STORE_ENABLED === "true";
export const STORE_HIDDEN_REASON = "store is hidden (set PUBLIC_STORE_ENABLED=true to run store specs)";
