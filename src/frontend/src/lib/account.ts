// Client helpers for /api/account/* (profile + address book).
//
// Pages call these instead of fetch() directly so the auth-header + base-URL
// boilerplate stays in one place.

const TOKEN_KEY = "partlphoto_token";
const DEFAULT_API = "http://localhost:5000";

export interface Profile {
  id: string;
  email: string;
  displayName: string | null;
  phone: string | null;
  role: string;
  hasPassword: boolean;
  oauthProvider: string | null;
}

export interface Address {
  id: string;
  label: string | null;
  name: string;
  street: string;
  city: string;
  postalCode: string;
  country: string;
  isDefaultShipping: boolean;
  isDefaultBilling: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface AddressInput {
  label?: string | null;
  name: string;
  street: string;
  city: string;
  postalCode: string;
  country?: string | null;
  isDefaultShipping?: boolean;
  isDefaultBilling?: boolean;
}

export interface UpdateProfileInput {
  displayName?: string | null;
  phone?: string | null;
  currentPassword?: string;
  newPassword?: string;
}

const apiBase = (): string => {
  if (typeof window === "undefined") return DEFAULT_API;
  const fromEnv = (import.meta as any)?.env?.PUBLIC_CLIENT_API_URL;
  return fromEnv || DEFAULT_API;
};

const getToken = (): string | null =>
  typeof localStorage === "undefined" ? null : localStorage.getItem(TOKEN_KEY);

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getToken();
  if (!token) throw new Error("not authenticated");

  const res = await fetch(`${apiBase()}${path}`, {
    ...init,
    headers: {
      ...(init.body ? { "Content-Type": "application/json" } : {}),
      Authorization: `Bearer ${token}`,
      ...(init.headers || {}),
    },
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || `request failed: ${res.status}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

// ── Profile ────────────────────────────────────────────────────────

export const getProfile = (): Promise<Profile> =>
  request<Profile>("/api/account/profile");

export const updateProfile = (input: UpdateProfileInput): Promise<Profile> =>
  request<Profile>("/api/account/profile", {
    method: "PUT",
    body: JSON.stringify(input),
  });

// ── Addresses ──────────────────────────────────────────────────────

export const listAddresses = (): Promise<Address[]> =>
  request<Address[]>("/api/account/addresses");

export const createAddress = (input: AddressInput): Promise<Address> =>
  request<Address>("/api/account/addresses", {
    method: "POST",
    body: JSON.stringify(input),
  });

export const updateAddress = (id: string, input: AddressInput): Promise<Address> =>
  request<Address>(`/api/account/addresses/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });

export const deleteAddress = (id: string): Promise<void> =>
  request<void>(`/api/account/addresses/${id}`, { method: "DELETE" });

export const setDefaultAddress = (
  id: string,
  flags: { shipping?: boolean; billing?: boolean },
): Promise<Address> =>
  request<Address>(`/api/account/addresses/${id}/default`, {
    method: "PUT",
    body: JSON.stringify(flags),
  });

// ── Auth gate ──────────────────────────────────────────────────────

/**
 * Redirect non-authenticated visitors to login. Returns the token if signed in.
 * Mirrors lib/admin.ts requireAdmin but only checks that a token exists.
 */
export function requireAuth(lang: "sk" | "en" = "sk"): string | null {
  const token = getToken();
  if (!token) {
    const here = encodeURIComponent(window.location.pathname + window.location.search);
    const loginPath = lang === "sk" ? "/sk/prihlasenie" : "/en/login";
    window.location.href = `${loginPath}?return=${here}`;
    return null;
  }
  return token;
}
