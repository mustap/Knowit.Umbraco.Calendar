import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import {
  css,
  customElement,
  html,
  nothing,
  property,
  repeat,
  state,
  unsafeHTML,
  type PropertyValues,
  type TemplateResult,
} from "@umbraco-cms/backoffice/external/lit";
import type { CalendarEvent, CalendarRef } from "./types.js";
import { getPreview } from "./api/calendar.api.js";

const MS_PER_DAY = 86_400_000;
type CalendarView = "month" | "day" | "year" | "list";

/**
 * Live preview of a calendar with month / day / year / agenda views, shown inside the picker so
 * editors see what they're selecting before saving.
 */
@customElement("knowit-calendar-preview")
export class KnowitCalendarPreviewElement extends UmbLitElement {
  @property({ type: Object })
  calendar?: CalendarRef;

  @state() private _events: CalendarEvent[] = [];
  @state() private _loading = false;
  @state() private _error?: string;
  @state() private _selected?: CalendarEvent;
  @state() private _view: CalendarView = "month";
  @state() private _anchor: Date = this.#startOf(new Date());

  protected override willUpdate(changed: PropertyValues<this>): void {
    if (changed.has("calendar")) {
      this._anchor = this.#startOf(new Date());
      this.#load();
    }
  }

  // --- date helpers ---

  #startOf(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
  }

  #startOfMonth(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), 1);
  }

  #mondayOnOrBefore(date: Date): Date {
    const d = this.#startOf(date);
    const offset = (d.getDay() + 6) % 7;
    return new Date(d.getFullYear(), d.getMonth(), d.getDate() - offset);
  }

  #key(year: number, month: number, day: number): string {
    return `${year}-${month}-${day}`;
  }

  // --- navigation ---

  #setView(view: CalendarView): void {
    this._view = view;
    this.#load();
  }

  #shift(delta: number): void {
    const a = this._anchor;
    if (this._view === "day") {
      this._anchor = new Date(a.getFullYear(), a.getMonth(), a.getDate() + delta);
    } else if (this._view === "year") {
      this._anchor = new Date(a.getFullYear() + delta, a.getMonth(), a.getDate());
    } else {
      this._anchor = new Date(a.getFullYear(), a.getMonth() + delta, 1);
    }
    this.#load();
  }

  #goToday(): void {
    this._anchor = this.#startOf(new Date());
    this.#load();
  }

  #goto(view: CalendarView, anchor: Date): void {
    this._view = view;
    this._anchor = anchor;
    this.#load();
  }

  async #load(): Promise<void> {
    if (!this.calendar) {
      this._events = [];
      return;
    }

    const a = this._anchor;
    let from: Date;
    let to: Date;
    if (this._view === "day") {
      from = new Date(a.getFullYear(), a.getMonth(), a.getDate() - 1);
      to = new Date(a.getFullYear(), a.getMonth(), a.getDate() + 2);
    } else if (this._view === "year") {
      from = new Date(a.getFullYear() - 1, 11, 31);
      to = new Date(a.getFullYear() + 1, 0, 2);
    } else if (this._view === "list") {
      from = new Date();
      to = new Date(Date.now() + 60 * MS_PER_DAY);
    } else {
      const gridStart = this.#mondayOnOrBefore(this.#startOfMonth(a));
      from = new Date(gridStart.getTime() - MS_PER_DAY);
      to = new Date(gridStart.getTime() + 43 * MS_PER_DAY);
    }

    this._loading = true;
    this._error = undefined;
    try {
      this._events = await getPreview(this, this.calendar, from, to, this._view === "year" ? 1000 : 250);
    } catch (error) {
      this._error = error instanceof Error ? error.message : "Failed to load preview.";
    } finally {
      this._loading = false;
    }
  }

  #push(map: Map<string, CalendarEvent[]>, key: string, ev: CalendarEvent): void {
    (map.get(key) ?? map.set(key, []).get(key)!).push(ev);
  }

  #eventsByDay(): Map<string, CalendarEvent[]> {
    const map = new Map<string, CalendarEvent[]>();
    for (const ev of this._events) {
      const s = new Date(ev.start);
      const e = new Date(ev.end);
      if (ev.isAllDay) {
        let cur = Date.UTC(s.getUTCFullYear(), s.getUTCMonth(), s.getUTCDate());
        let last = Date.UTC(e.getUTCFullYear(), e.getUTCMonth(), e.getUTCDate());
        if (last > cur) last -= MS_PER_DAY;
        if (last < cur) last = cur;
        for (let t = cur; t <= last; t += MS_PER_DAY) {
          const d = new Date(t);
          this.#push(map, this.#key(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()), ev);
        }
      } else {
        const cur = new Date(s.getFullYear(), s.getMonth(), s.getDate()).getTime();
        const last = new Date(e.getFullYear(), e.getMonth(), e.getDate()).getTime();
        for (let t = cur; t <= Math.max(cur, last); t += MS_PER_DAY) {
          const d = new Date(t);
          this.#push(map, this.#key(d.getFullYear(), d.getMonth(), d.getDate()), ev);
        }
      }
    }
    return map;
  }

  #time(ev: CalendarEvent): string {
    return ev.isAllDay
      ? "All day"
      : new Date(ev.start).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
  }

  #headerLabel(): string {
    const a = this._anchor;
    switch (this._view) {
      case "day":
        return a.toLocaleDateString(undefined, { weekday: "long", year: "numeric", month: "long", day: "numeric" });
      case "year":
        return String(a.getFullYear());
      case "list":
        return "Upcoming";
      default:
        return a.toLocaleDateString(undefined, { month: "long", year: "numeric" });
    }
  }

  // --- detail dialog ---

  #dialog(): HTMLDialogElement | null {
    return this.renderRoot.querySelector("dialog");
  }

  #open(ev: CalendarEvent): void {
    this._selected = ev;
    this.updateComplete.then(() => this.#dialog()?.showModal());
  }

  #close(): void {
    this.#dialog()?.close();
    this._selected = undefined;
  }

  #backdrop(e: MouseEvent): void {
    if (e.target === this.#dialog()) {
      this.#close();
    }
  }

  #whenLabel(ev: CalendarEvent): string {
    const start = new Date(ev.start);
    if (ev.isAllDay) {
      return start.toLocaleDateString(undefined, { weekday: "long", year: "numeric", month: "long", day: "numeric" });
    }
    const end = new Date(ev.end);
    const date = start.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" });
    const from = start.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
    const to = end.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
    return `${date}, ${from} – ${to}`;
  }

  // --- view renderers ---

  #renderMonth(byDay: Map<string, CalendarEvent[]>): TemplateResult {
    const today = this.#startOf(new Date());
    const month = this.#startOfMonth(this._anchor);
    const gridStart = this.#mondayOnOrBefore(month);
    const days = Array.from({ length: 42 }, (_, i) => new Date(gridStart.getTime() + i * MS_PER_DAY));
    const weekdays = Array.from({ length: 7 }, (_, i) =>
      new Date(gridStart.getTime() + i * MS_PER_DAY).toLocaleDateString(undefined, { weekday: "short" }),
    );
    return html`<div class="grid">
      ${repeat(weekdays, (w) => w, (w) => html`<div class="weekday">${w}</div>`)}
      ${repeat(
        days,
        (d) => d.getTime(),
        (d) => {
          const events = byDay.get(this.#key(d.getFullYear(), d.getMonth(), d.getDate())) ?? [];
          const classes = [
            "day",
            d.getMonth() !== month.getMonth() ? "other-month" : "",
            d.getTime() === today.getTime() ? "today" : "",
            d.getTime() < today.getTime() ? "past" : "",
          ].join(" ");
          return html`<div class=${classes}>
            <span class="daynum">${d.getDate()}</span>
            ${events.map(
              (ev) => html`<button
                type="button"
                class="event ${ev.onlineMeeting ? "meeting" : ""}"
                title=${ev.title}
                @click=${() => this.#open(ev)}>
                ${ev.isAllDay ? nothing : html`<span class="t">${this.#time(ev)}</span>`}${ev.title}
              </button>`,
            )}
          </div>`;
        },
      )}
    </div>`;
  }

  #renderDay(byDay: Map<string, CalendarEvent[]>): TemplateResult {
    const a = this._anchor;
    const events = byDay.get(this.#key(a.getFullYear(), a.getMonth(), a.getDate())) ?? [];
    if (events.length === 0) {
      return html`<p class="muted">No events on this day.</p>`;
    }
    return html`<div class="dayview">
      ${events.map(
        (ev) => html`<button type="button" class="row ${ev.onlineMeeting ? "meeting" : ""}" @click=${() => this.#open(ev)}>
          <span class="rowtime">${this.#time(ev)}</span>
          <span class="rowtitle">${ev.title}</span>
        </button>`,
      )}
    </div>`;
  }

  #renderYear(byDay: Map<string, CalendarEvent[]>): TemplateResult {
    const today = this.#startOf(new Date());
    const year = this._anchor.getFullYear();
    const months = Array.from({ length: 12 }, (_, m) => new Date(year, m, 1));
    return html`<div class="year">
      ${months.map((month) => {
        const gridStart = this.#mondayOnOrBefore(month);
        const days = Array.from({ length: 42 }, (_, i) => new Date(gridStart.getTime() + i * MS_PER_DAY));
        return html`<div class="minimonth">
          <button type="button" class="mininame" @click=${() => this.#goto("month", month)}>
            ${month.toLocaleDateString(undefined, { month: "short" })}
          </button>
          <div class="minigrid">
            ${days.map((d) => {
              if (d.getMonth() !== month.getMonth()) {
                return html`<span class="minicell blank"></span>`;
              }
              const count = byDay.get(this.#key(d.getFullYear(), d.getMonth(), d.getDate()))?.length ?? 0;
              const cls = ["minicell", count > 0 ? "has-events" : "", d.getTime() === today.getTime() ? "today" : ""].join(" ");
              return count > 0
                ? html`<button type="button" class=${cls} title=${`${count} event(s)`} @click=${() => this.#goto("day", d)}>
                    <span class="miniday">${d.getDate()}</span><span class="minicount">${count}</span>
                  </button>`
                : html`<span class=${cls}><span class="miniday">${d.getDate()}</span></span>`;
            })}
          </div>
        </div>`;
      })}
    </div>`;
  }

  #renderList(): TemplateResult {
    if (this._events.length === 0) {
      return html`<p class="muted">No upcoming events.</p>`;
    }
    return html`<div class="dayview">
      ${this._events.map(
        (ev) => html`<button type="button" class="row ${ev.onlineMeeting ? "meeting" : ""}" @click=${() => this.#open(ev)}>
          <span class="rowtime">${this.#whenLabel(ev)}</span>
          <span class="rowtitle">${ev.title}</span>
        </button>`,
      )}
    </div>`;
  }

  #renderDialog(): TemplateResult {
    const ev = this._selected;
    return html`
      <dialog class="kc-dialog" @click=${this.#backdrop}>
        ${ev
          ? html`
              <button class="kc-close" aria-label="Close" @click=${this.#close}>&times;</button>
              <h3 class="kc-title">${ev.title}</h3>
              <p class="kc-when">${this.#whenLabel(ev)}</p>
              ${ev.location ? html`<p class="kc-meta">📍 ${ev.location}</p>` : nothing}
              ${ev.organizerName ? html`<p class="kc-meta">👤 ${ev.organizerName}</p>` : nothing}
              ${ev.categories?.length ? html`<p class="kc-meta">${ev.categories.join(", ")}</p>` : nothing}
              ${ev.description ? html`<div class="kc-desc">${unsafeHTML(ev.description)}</div>` : nothing}
              <div class="kc-actions">
                ${ev.onlineMeeting
                  ? html`<uui-button look="primary" href=${ev.onlineMeeting.joinUrl} target="_blank"
                      label=${ev.onlineMeeting.providerDisplayName ?? "Join online"}></uui-button>`
                  : nothing}
                ${ev.htmlLink
                  ? html`<uui-button look="secondary" href=${ev.htmlLink} target="_blank" label="Open in calendar"></uui-button>`
                  : nothing}
              </div>
            `
          : nothing}
      </dialog>
    `;
  }

  override render() {
    if (!this.calendar) return nothing;
    if (this._loading) return html`<uui-loader></uui-loader>`;
    if (this._error) return html`<div class="error">${this._error}</div>`;

    const byDay = this.#eventsByDay();
    const body =
      this._view === "day"
        ? this.#renderDay(byDay)
        : this._view === "year"
          ? this.#renderYear(byDay)
          : this._view === "list"
            ? this.#renderList()
            : this.#renderMonth(byDay);

    const viewBtn = (view: CalendarView, text: string) => html`<uui-button
      compact
      look=${this._view === view ? "primary" : "default"}
      label=${text}
      @click=${() => this.#setView(view)}>${text}</uui-button>`;

    return html`
      <div class="bar">
        ${this._view !== "list"
          ? html`<uui-button compact label="Previous" @click=${() => this.#shift(-1)}>‹</uui-button>
              <strong>${this.#headerLabel()}</strong>
              <uui-button compact label="Next" @click=${() => this.#shift(1)}>›</uui-button>
              <uui-button compact look="secondary" label="Today" @click=${this.#goToday}>Today</uui-button>`
          : html`<strong>${this.#headerLabel()}</strong>`}
      </div>
      <div class="views">
        ${viewBtn("day", "Day")}${viewBtn("month", "Month")}${viewBtn("year", "Year")}${viewBtn("list", "Agenda")}
      </div>
      ${body}
      ${this.#renderDialog()}
    `;
  }

  static override styles = css`
    :host { display: block; margin-top: var(--uui-size-space-3, 9px); }
    .bar { display: flex; align-items: center; gap: var(--uui-size-space-2, 6px); margin-bottom: var(--uui-size-space-1, 3px); }
    .bar strong { min-width: 8rem; text-align: center; }
    .bar uui-button[look="secondary"] { margin-left: auto; }
    .views { display: flex; gap: 2px; margin-bottom: var(--uui-size-space-2, 6px); }

    .grid { display: grid; grid-template-columns: repeat(7, 1fr); border: 1px solid var(--uui-color-border, #e3e3e8); border-radius: 6px; overflow: hidden; }
    .weekday { background: var(--uui-color-surface-alt, #f3f3f5); font-size: 0.65rem; font-weight: 600; text-transform: uppercase; color: var(--uui-color-text-alt, #8a8a93); text-align: center; padding: 0.2rem 0; border-bottom: 1px solid var(--uui-color-border, #e3e3e8); }
    .day { min-height: 56px; border-right: 1px solid var(--uui-color-border, #e3e3e8); border-bottom: 1px solid var(--uui-color-border, #e3e3e8); padding: 2px; display: flex; flex-direction: column; gap: 2px; }
    .day:nth-child(7n + 7) { border-right: 0; }
    .day.other-month { background: var(--uui-color-surface-alt, #fafafb); color: var(--uui-color-text-alt, #8a8a93); }
    .day.today { background: var(--uui-color-current, #eef3ff); }
    .day.past .daynum { color: var(--uui-color-text-alt, #b3b3b8); }
    .day.past .event { opacity: 0.55; }
    .daynum { font-size: 0.7rem; font-weight: 600; align-self: flex-end; }
    .event { display: block; width: 100%; text-align: left; border: 0; cursor: pointer; font-family: inherit; background: var(--uui-color-default, #2152ff); color: var(--uui-color-default-contrast, #fff); border-radius: 3px; padding: 0 3px; font-size: 0.62rem; line-height: 1.35; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .event:hover { filter: brightness(1.1); }
    .event.meeting { background: var(--uui-color-positive, #128a5b); }
    .event .t { font-weight: 700; margin-right: 2px; }

    .dayview { display: flex; flex-direction: column; gap: 4px; }
    .row { display: flex; gap: 0.6rem; align-items: baseline; width: 100%; text-align: left; border: 0; cursor: pointer; font: inherit; background: var(--uui-color-surface-alt, #f3f3f5); border-left: 3px solid var(--uui-color-default, #2152ff); border-radius: 4px; padding: 0.4rem 0.6rem; }
    .row.meeting { border-left-color: var(--uui-color-positive, #128a5b); }
    .rowtime { font-weight: 700; min-width: 4.5rem; font-size: 0.8rem; }
    .rowtitle { font-weight: 600; }

    .year { display: grid; grid-template-columns: repeat(auto-fill, minmax(120px, 1fr)); gap: 0.5rem; }
    .minimonth { border: 1px solid var(--uui-color-border, #e3e3e8); border-radius: 6px; padding: 4px; }
    .mininame { border: 0; background: none; cursor: pointer; font-weight: 700; font-size: 0.75rem; color: var(--uui-color-interactive, #2152ff); padding: 0 0 2px; }
    .minigrid { display: grid; grid-template-columns: repeat(7, 1fr); gap: 1px; }
    .minicell { font-size: 0.6rem; text-align: center; padding: 1px 0; color: var(--uui-color-text-alt, #555); border: 0; background: none; font-family: inherit; }
    .minicell.blank { visibility: hidden; }
    .minicell.has-events { background: rgba(33, 82, 255, 0.14); color: var(--uui-color-default, #2152ff); font-weight: 700; border-radius: 2px; cursor: pointer; }
    .minicell.today { outline: 1px solid var(--uui-color-default, #2152ff); }
    .minicount { font-size: 0.5rem; font-weight: 700; color: var(--uui-color-danger, #d42054); vertical-align: super; margin-left: 1px; }

    .muted { color: var(--uui-color-text-alt, #8a8a93); }
    .error { color: var(--uui-color-danger, #d42054); }

    .kc-dialog { border: 0; border-radius: 10px; padding: 1.25rem; max-width: 420px; width: calc(100% - 2rem); box-shadow: 0 12px 40px rgba(0, 0, 0, 0.25); color: var(--uui-color-text, #1b1b1f); }
    .kc-dialog::backdrop { background: rgba(0, 0, 0, 0.45); }
    .kc-close { float: right; border: 0; background: none; font-size: 1.4rem; line-height: 1; cursor: pointer; color: var(--uui-color-text-alt, #8a8a93); }
    .kc-title { margin: 0 0 0.4rem; font-size: 1.15rem; }
    .kc-when { font-weight: 600; margin: 0 0 0.6rem; }
    .kc-meta { margin: 0.2rem 0; color: var(--uui-color-text-alt, #515159); }
    .kc-desc { margin: 0.6rem 0; border-top: 1px solid var(--uui-color-border, #e3e3e8); padding-top: 0.6rem; }
    .kc-actions { display: flex; gap: 0.5rem; margin-top: 0.8rem; flex-wrap: wrap; }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    "knowit-calendar-preview": KnowitCalendarPreviewElement;
  }
}
