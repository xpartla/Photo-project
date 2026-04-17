// Client-side helpers for the admin CMS area.
//
// Admin gating lives in two places:
//   1. The backend enforces it on every admin API call via ICurrentUser.IsAdmin.
//   2. This module hides admin-only UI from non-admins and redirects them to login.
// The backend check is authoritative; the client-side redirect is just UX.

export interface StoredUser {
  id: string;
  email: string;
  role: string;
}

export function getToken(): string | null {
  if (typeof localStorage === "undefined") return null;
  return localStorage.getItem("partlphoto_token");
}

export function getStoredUser(): StoredUser | null {
  if (typeof localStorage === "undefined") return null;
  const raw = localStorage.getItem("partlphoto_user");
  if (!raw) return null;
  try {
    return JSON.parse(raw) as StoredUser;
  } catch {
    return null;
  }
}

export function isAuthenticated(): boolean {
  return getToken() !== null;
}

export function isAdmin(): boolean {
  const u = getStoredUser();
  return u?.role === "Admin";
}

/**
 * Redirect the browser to the login page if the user is not an admin.
 * Returns the current token on success, or triggers a redirect and returns null.
 */
export function requireAdmin(lang: "sk" | "en" = "sk"): string | null {
  const token = getToken();
  if (!token || !isAdmin()) {
    const here = encodeURIComponent(window.location.pathname + window.location.search);
    const loginPath = lang === "sk" ? "/sk/prihlasenie" : "/en/login";
    window.location.href = `${loginPath}?return=${here}`;
    return null;
  }
  return token;
}
