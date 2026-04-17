import { describe, it, expect, beforeEach, vi } from "vitest";
import { getStoredUser, isAdmin, isAuthenticated } from "../../src/lib/admin";

describe("admin helpers", () => {
  beforeEach(() => {
    const store: Record<string, string> = {};
    vi.stubGlobal("localStorage", {
      getItem: (k: string) => store[k] ?? null,
      setItem: (k: string, v: string) => { store[k] = v; },
      removeItem: (k: string) => { delete store[k]; },
      clear: () => { for (const k of Object.keys(store)) delete store[k]; },
    });
  });

  it("returns null user when nothing is stored", () => {
    expect(getStoredUser()).toBeNull();
    expect(isAdmin()).toBe(false);
    expect(isAuthenticated()).toBe(false);
  });

  it("reports Customer role as not-admin", () => {
    localStorage.setItem("partlphoto_token", "t");
    localStorage.setItem("partlphoto_user", JSON.stringify({ id: "1", email: "c@x", role: "Customer" }));
    expect(isAuthenticated()).toBe(true);
    expect(isAdmin()).toBe(false);
  });

  it("reports Admin role as admin", () => {
    localStorage.setItem("partlphoto_token", "t");
    localStorage.setItem("partlphoto_user", JSON.stringify({ id: "1", email: "a@x", role: "Admin" }));
    expect(isAuthenticated()).toBe(true);
    expect(isAdmin()).toBe(true);
  });

  it("tolerates malformed user JSON", () => {
    localStorage.setItem("partlphoto_user", "not-json");
    expect(getStoredUser()).toBeNull();
    expect(isAdmin()).toBe(false);
  });
});
