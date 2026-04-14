/**
 * Pure booking utility functions — validation and calendar state logic.
 * Mirrors the algorithms used in BookingCalendar.astro for testability.
 */

export function validateName(name: string): "required" | null {
  if (!name.trim()) return "required";
  return null;
}

export function validateEmail(email: string): "required" | "invalidEmail" | null {
  if (!email.trim()) return "required";
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return "invalidEmail";
  return null;
}

export interface SlotInfo {
  date: string;
  isBlocked: boolean;
  isBooked: boolean;
  isPast: boolean;
}

export type DayState = "past" | "available" | "unavailable";

/**
 * Determine the calendar display state for a given date.
 * Uses the same logic as BookingCalendar.astro's renderCalendar().
 */
export function getDayState(
  dateStr: string,
  slots: SlotInfo[],
  todayStr: string,
): DayState {
  const daySlots = slots.filter((s) => s.date === dateStr);
  const availableSlots = daySlots.filter(
    (s) => !s.isBlocked && !s.isBooked && !s.isPast,
  );
  const isPast =
    daySlots.length > 0
      ? daySlots.every((s) => s.isPast)
      : new Date(dateStr) < new Date(todayStr);

  if (isPast) return "past";
  if (availableSlots.length > 0) return "available";
  return "unavailable";
}

/**
 * Return all non-past slots for a given date (includes blocked/booked — they
 * are rendered but disabled in the UI).
 */
export function getAvailableSlots<T extends SlotInfo>(
  slots: T[],
  dateStr: string,
): T[] {
  return slots.filter((s) => s.date === dateStr && !s.isPast);
}
