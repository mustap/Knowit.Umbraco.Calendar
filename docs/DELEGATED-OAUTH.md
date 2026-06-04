# Phase 7 — Per-user delegated OAuth (NOT IMPLEMENTED)

**Status:** Draft v0.1 · **Date:** 3 June 2026

## 1. Goal

Let a backoffice **editor connect their own Google / Microsoft account** and pick from **their
personal calendars** — in addition to the app-level shared calendars from Phases 1–6. Read-only.

This is the architecture's deferred "per-user OAuth (delegated)" model. It coexists with app-level;
nothing about Phases 0–6 changes.

## 2. Why it's different from what we have

| | App-level (built) | Delegated (this phase) |
|---|---|---|
| Auth | Service account / app-only client credentials | OAuth 2.0 **authorization code + PKCE**, per user |
| Credentials | One Google service account + one Entra app (application perms) | An OAuth **web client** (Google OAuth Client ID; Entra app with **delegated** perms + redirect URI) |
| Whose calendars | Fixed allowlist of org/shared calendars | The signed-in editor's own calendars |
| Secret stored | App secret (dev owns) | **Per-user refresh token** (sensitive personal data) |
| Editor action | None | Clicks "Connect", consents once |

The new, security-critical element is **storing per-editor refresh tokens**.

## 3. Design

### 3.1 Connect / disconnect flow
1. Editor opens a **"Calendar connections"** area in the backoffice and clicks **Connect Google**.
2. Backoffice calls a management endpoint that returns the provider **authorize URL**
   (auth-code + PKCE; Google `access_type=offline&prompt=consent`; Microsoft `offline_access`
   + `Calendars.Read`). State + PKCE verifier are kept server-side, tied to the Umbraco user.
3. Editor consents at Google/Microsoft → redirected back to a package **callback endpoint**.
4. Callback exchanges the code for tokens, **encrypts and stores the refresh token** keyed by
   Umbraco user + provider, and closes the popup.
5. **Disconnect** deletes the stored token and best-effort **revokes** it at the provider.

### 3.2 Token storage — *Recommendation*
Store per-user tokens via Umbraco's **`IKeyValueService`** under key
`Knowit.Calendar.Connection.{userKey}.{provider}`, value = JSON
`{ refreshToken, scopes, obtainedUtc }` **encrypted with `IDataProtector`** (ASP.NET Core Data
Protection, purpose-scoped). No schema/migration needed; refresh tokens never stored in plaintext.

*Alternative:* a dedicated EF Core table. More work; only worth it if we later need querying
across users. Start with key-value.

### 3.3 Delegated providers
New `ICalendarProvider` implementations that resolve **the current editor's** token:
- `GoogleDelegatedProvider` (key `google-user`) — `UserCredential` from the stored refresh token;
  lists `CalendarList`, reads events.
- `MicrosoftDelegatedProvider` (key `microsoft-user`) — Graph with an
  `OnBehalfOf`/refresh-token credential; `/me/calendars`, `/me/calendarView`.

They need the **acting user**, so resolution is request-scoped (the backoffice user, or the
content author for rendering — see §5 open questions).

### 3.4 Picker surfacing
When the editor has a connection, the picker's calendar list includes **"My calendars (Google)"**
fetched live from their account, alongside the allowlisted shared calendars. The stored
`CalendarRef` records the provider key (`google-user`) so rendering knows to use the delegated
provider.

## 4. Security & GDPR (the heavy part)

- **Refresh tokens are personal data** → encrypt at rest (`IDataProtector`), never log, never
  return to the client.
- **Least privilege:** read-only scopes (`calendar.readonly` / `Calendars.Read`).
- **Revocable & erasable:** disconnect revokes at the provider and deletes locally; deleting an
  Umbraco user deletes their connections (notification handler). Supports right-to-erasure.
- **CSRF/replay:** PKCE + state; callback validates state bound to the user session.
- **Document** in README: what is stored, where, encryption, retention, how to disconnect.
- **EU AI Act:** unchanged — no automated decisions; data clearly attributed.

## 5. Open questions / decisions

1. **Provider first?** Google is testable with a personal account today; Microsoft delegated needs
   a tenant with mailboxes (the test tenant has none). → *Recommend Google first.*
2. **Frontend rendering of personal calendars.** A delegated calendar is tied to the *editor who
   connected it*, not the anonymous site visitor. For public rendering we must read using **that
   editor's stored token** (resolved by the stored `CalendarRef` → owning user), not the current
   request user. → *Recommend: a `CalendarRef` for a delegated calendar carries the owning user
   key; the provider loads that user's token server-side.* (App-level calendars are unaffected.)
3. **Token storage:** key-value + Data Protection (recommended) vs EF table.
4. **Where editors connect:** a dedicated backoffice **dashboard** vs an action inside the picker.

## 6. Build plan

- **7.0** OAuth options + `IDataProtector` token store (`IConnectionStore`) + key-value impl. Unit-tested.
- **7.1** Google OAuth: authorize-URL + callback endpoints (PKCE/state), token exchange, store.
- **7.2** `GoogleDelegatedProvider` (list + read using stored token); register under `google-user`.
- **7.3** Backoffice "Calendar connections" UI (connect/disconnect, status) + picker shows personal calendars.
- **7.4** Disconnect + revoke; delete-on-user-deletion handler; README data-protection section.
- **7.5** Microsoft delegated (when a mailbox-bearing tenant is available).
- Each step: build + test; Google verified live with a real account.

## 7. Setup the developer will need (Google, delegated)

Different from the service account:
1. Google Cloud → **OAuth consent screen** configured.
2. **Credentials → OAuth client ID → Web application**, with redirect URI
   `https://<site>/umbraco/management/api/v1/knowit-calendar/oauth/google/callback`.
3. Client ID + secret into config (secret store).
