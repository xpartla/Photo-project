import { describe, it, expect } from "vitest";
import {
  validateName,
  validateEmail,
  getDayState,
  getAvailableSlots,
  type SlotInfo,
} from "../../src/lib/booking";

describe("booking utilities", () => {
  // ── validateName ──────────────────────────────────────────────

  describe("validateName()", () => {
    it("returns 'required' for empty string", () => {
      expect(validateName("")).toBe("required");
    });

    it("returns 'required' for whitespace-only string", () => {
      expect(validateName("   ")).toBe("required");
    });

    it("returns null for a valid name", () => {
      expect(validateName("Test Client")).toBeNull();
    });

    it("returns null for a single character", () => {
      expect(validateName("A")).toBeNull();
    });
  });

  // ── validateEmail ─────────────────────────────────────────────

  describe("validateEmail()", () => {
    it("returns 'required' for empty string", () => {
      expect(validateEmail("")).toBe("required");
    });

    it("returns 'required' for whitespace-only string", () => {
      expect(validateEmail("   ")).toBe("required");
    });

    it("returns 'invalidEmail' when @ is missing", () => {
      expect(validateEmail("notanemail")).toBe("invalidEmail");
    });

    it("returns 'invalidEmail' when domain is missing", () => {
      expect(validateEmail("user@")).toBe("invalidEmail");
    });

    it("returns 'invalidEmail' when TLD is missing", () => {
      expect(validateEmail("user@domain")).toBe("invalidEmail");
    });

    it("returns null for a valid email", () => {
      expect(validateEmail("user@example.com")).toBeNull();
    });

    it("returns null for email with subdomain", () => {
      expect(validateEmail("user@mail.example.com")).toBeNull();
    });
  });

  // ── getDayState ───────────────────────────────────────────────

  describe("getDayState()", () => {
    const today = "2026-05-10";

    const slots: SlotInfo[] = [
      // Two available slots on May 15
      { date: "2026-05-15", isBlocked: false, isBooked: false, isPast: false },
      { date: "2026-05-15", isBlocked: false, isBooked: false, isPast: false },
      // One blocked slot on May 16
      { date: "2026-05-16", isBlocked: true, isBooked: false, isPast: false },
      // One booked slot on May 17
      { date: "2026-05-17", isBlocked: false, isBooked: true, isPast: false },
      // Past slot on May 1
      { date: "2026-05-01", isBlocked: false, isBooked: false, isPast: true },
      // Mixed: one available + one booked on May 20
      { date: "2026-05-20", isBlocked: false, isBooked: false, isPast: false },
      { date: "2026-05-20", isBlocked: false, isBooked: true, isPast: false },
    ];

    it("returns 'available' when day has at least one available slot", () => {
      expect(getDayState("2026-05-15", slots, today)).toBe("available");
    });

    it("returns 'available' for mixed (available + booked) day", () => {
      expect(getDayState("2026-05-20", slots, today)).toBe("available");
    });

    it("returns 'unavailable' when all slots are blocked", () => {
      expect(getDayState("2026-05-16", slots, today)).toBe("unavailable");
    });

    it("returns 'unavailable' when all slots are booked", () => {
      expect(getDayState("2026-05-17", slots, today)).toBe("unavailable");
    });

    it("returns 'past' when all slots are marked past", () => {
      expect(getDayState("2026-05-01", slots, today)).toBe("past");
    });

    it("returns 'past' for a date before today with no slots", () => {
      expect(getDayState("2026-05-05", slots, today)).toBe("past");
    });

    it("returns 'unavailable' for a future date with no slots", () => {
      expect(getDayState("2026-05-25", slots, today)).toBe("unavailable");
    });
  });

  // ── getAvailableSlots ─────────────────────────────────────────

  describe("getAvailableSlots()", () => {
    const slots: SlotInfo[] = [
      { date: "2026-05-15", isBlocked: false, isBooked: false, isPast: false },
      { date: "2026-05-15", isBlocked: true, isBooked: false, isPast: false },
      { date: "2026-05-15", isBlocked: false, isBooked: false, isPast: true },
      { date: "2026-05-16", isBlocked: false, isBooked: false, isPast: false },
    ];

    it("returns non-past slots for the given date", () => {
      const result = getAvailableSlots(slots, "2026-05-15");
      // 2 non-past slots (available + blocked but not past)
      expect(result).toHaveLength(2);
      expect(result.every((s) => !s.isPast)).toBe(true);
    });

    it("excludes slots from other dates", () => {
      const result = getAvailableSlots(slots, "2026-05-16");
      expect(result).toHaveLength(1);
    });

    it("returns empty array for a date with no slots", () => {
      expect(getAvailableSlots(slots, "2026-05-20")).toEqual([]);
    });

    it("returns empty array for a date with only past slots", () => {
      const pastOnly: SlotInfo[] = [
        { date: "2026-05-01", isBlocked: false, isBooked: false, isPast: true },
      ];
      expect(getAvailableSlots(pastOnly, "2026-05-01")).toEqual([]);
    });
  });
});
