import { BasePage } from "./BasePage";
import type { Locator, Page } from "@playwright/test";

export class BookingPage extends BasePage {
  calendarNextBtn(): Locator {
    return this.page.locator("#cal-next");
  }

  formPhone(): Locator {
    return this.page.locator("#field-phone");
  }

  formDogCount(): Locator {
    return this.page.locator("#field-dogs");
  }

  formSpecialRequests(): Locator {
    return this.page.locator("#field-requests");
  }

  formError(field: string): Locator {
    return this.page.locator(`#error-${field}`);
  }

  slotPicker(): Locator {
    return this.page.locator("#slot-picker");
  }

  bookingFormContainer(): Locator {
    return this.page.locator("#booking-form-container");
  }

  sessionCards(): Locator {
    return this.page.locator(".session-card");
  }

  sessionCardCta(index: number): Locator {
    return this.page.locator(".session-card__cta").nth(index);
  }

  modalOverlay(): Locator {
    return this.page.locator(".session-modal-overlay--open");
  }

  modalTitle(): Locator {
    return this.page.locator(".session-modal-overlay--open .session-modal__title");
  }

  modalClose(): Locator {
    return this.page.locator(".session-modal-overlay--open [data-close-modal]");
  }

  modalCta(): Locator {
    return this.page.locator(".session-modal-overlay--open .session-modal__cta");
  }

  calendarDays(): Locator {
    return this.page.locator(".calendar__day--available");
  }

  slotButtons(): Locator {
    return this.page.locator(".slot-picker__slot:not(.slot-picker__slot--booked):not(.slot-picker__slot--blocked)");
  }

  formName(): Locator {
    return this.page.locator("#field-name");
  }

  formEmail(): Locator {
    return this.page.locator("#field-email");
  }

  submitButton(): Locator {
    return this.page.locator("#submit-btn");
  }

  confirmationTitle(): Locator {
    return this.page.locator(".booking-confirmation__title");
  }
}
