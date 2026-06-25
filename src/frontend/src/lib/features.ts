/**
 * Feature flags for product areas that ship in the codebase but can be
 * toggled off in a given environment.
 *
 * The e-commerce store (shop, cart, checkout, customer orders and the admin
 * shop CMS) is fully built but hidden for the initial dog-photography launch.
 * Set the env var PUBLIC_STORE_ENABLED="true" to bring it back — the full
 * re-enable checklist lives in docs/store-reenable.md.
 *
 * Default is OFF: an unset/empty/any-non-"true" value keeps the store hidden.
 */
export const STORE_ENABLED = import.meta.env.PUBLIC_STORE_ENABLED === "true";
