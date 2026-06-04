// Mirrors the normalized domain model returned by the management API
// (System.Text.Json web defaults → camelCase).

export interface CalendarRef {
  providerKey: string;
  calendarId: string;
  displayName: string;
}

export interface OnlineMeeting {
  joinUrl: string;
  provider: string | number;
  providerDisplayName?: string | null;
}

export interface CalendarEvent {
  id: string;
  title: string;
  description?: string | null;
  location?: string | null;
  start: string; // ISO 8601 UTC
  end: string;
  isAllDay: boolean;
  organizerName?: string | null;
  htmlLink?: string | null;
  onlineMeeting?: OnlineMeeting | null;
  categories: string[];
  recurrence?: unknown;
}
