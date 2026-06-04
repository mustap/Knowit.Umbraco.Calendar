import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import {
  css,
  customElement,
  html,
  nothing,
  property,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import type { CalendarRef } from "./types.js";
import { getCalendars } from "./api/calendar.api.js";
import "./calendar-preview.element.js";

const NONE_VALUE = "";

/**
 * Property editor UI for picking a calendar. Loads the admin-allowed calendars from the
 * management API, lets the editor choose one, stores the chosen {@link CalendarRef}, and shows
 * a live preview of upcoming events.
 */
@customElement("knowit-calendar-picker")
export class KnowitCalendarPickerElement extends UmbLitElement implements UmbPropertyEditorUiElement {
  @property({ type: Object })
  value?: CalendarRef;

  @state() private _calendars: CalendarRef[] = [];
  @state() private _loading = true;
  @state() private _error?: string;

  override connectedCallback(): void {
    super.connectedCallback();
    this.#load();
  }

  async #load(): Promise<void> {
    this._loading = true;
    this._error = undefined;
    try {
      this._calendars = await getCalendars(this);
    } catch (error) {
      this._error = error instanceof Error ? error.message : "Failed to load calendars.";
    } finally {
      this._loading = false;
    }
  }

  #optionValue(calendar: CalendarRef): string {
    return `${calendar.providerKey}::${calendar.calendarId}`;
  }

  #onChange(event: Event): void {
    const selected = (event.target as HTMLInputElement).value;
    if (selected === NONE_VALUE) {
      this.value = undefined;
    } else {
      this.value = this._calendars.find((c) => this.#optionValue(c) === selected);
    }
    this.dispatchEvent(new UmbChangeEvent());
  }

  override render() {
    if (this._loading) {
      return html`<uui-loader></uui-loader>`;
    }
    if (this._error) {
      return html`<div class="error">${this._error}</div>`;
    }
    if (this._calendars.length === 0) {
      return html`<p class="muted">
        No calendars are configured. Ask an administrator to configure calendars under
        <code>Knowit:Calendar</code>.
      </p>`;
    }

    const options = [
      { name: "— Select a calendar —", value: NONE_VALUE, selected: this.value == null },
      ...this._calendars.map((calendar) => ({
        name: calendar.displayName,
        value: this.#optionValue(calendar),
        selected:
          this.value?.providerKey === calendar.providerKey &&
          this.value?.calendarId === calendar.calendarId,
      })),
    ];

    return html`
      <uui-select label="Calendar" .options=${options} @change=${this.#onChange}></uui-select>
      ${this.value
        ? html`<knowit-calendar-preview .calendar=${this.value}></knowit-calendar-preview>`
        : nothing}
    `;
  }

  static override styles = css`
    :host {
      display: block;
    }
    .muted {
      color: var(--uui-color-text-alt, #515159);
    }
    .error {
      color: var(--uui-color-danger, #d42054);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    "knowit-calendar-picker": KnowitCalendarPickerElement;
  }
}

export default KnowitCalendarPickerElement;
