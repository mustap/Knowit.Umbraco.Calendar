import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import type { UmbClassInterface } from "@umbraco-cms/backoffice/class-api";
import type { CalendarEvent, CalendarRef } from "../types.js";

const BASE = "/umbraco/management/api/v1/knowit-calendar";

async function authedFetch<T>(host: UmbClassInterface, url: string): Promise<T> {
  const auth = await host.getContext(UMB_AUTH_CONTEXT);
  const token = await auth?.getLatestToken();
  const response = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    throw new Error(`Calendar API request failed (${response.status}).`);
  }
  return (await response.json()) as T;
}

/** Lists the admin-allowed calendars across enabled providers. */
export async function getCalendars(host: UmbClassInterface): Promise<CalendarRef[]> {
  // tryExecute returns the data intersected with an optional `error` (UmbApiResponse<T>).
  const response = await tryExecute(host, authedFetch<CalendarRef[]>(host, `${BASE}/calendars`));
  if (response.error) {
    throw response.error;
  }
  return response as CalendarRef[];
}

/** Previews events for a calendar within an explicit date range (used for month navigation). */
export async function getPreview(
  host: UmbClassInterface,
  calendar: CalendarRef,
  from: Date,
  to: Date,
  max = 250,
): Promise<CalendarEvent[]> {
  const query = new URLSearchParams({
    providerKey: calendar.providerKey,
    calendarId: calendar.calendarId,
    from: from.toISOString(),
    to: to.toISOString(),
    max: String(max),
  });
  const response = await tryExecute(host, authedFetch<CalendarEvent[]>(host, `${BASE}/preview?${query.toString()}`));
  if (response.error) {
    throw response.error;
  }
  return response as CalendarEvent[];
}
