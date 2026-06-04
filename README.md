# Knowit.Umbraco.Calendar

Display Google and Microsoft (Outlook/Teams) calendar events in **Umbraco 17+**. A developer
configures credentials once; editors then pick a calendar per page and the events render on the
site — read-only, cached, and GDPR-aware.

- **Target:** Umbraco 17 LTS / .NET 10
- **Direction:** read-only (display) in v1
- **Auth:** app-level (one Google service account + one Microsoft app registration)
- **Surfaces:** frontend rendering, a backoffice property editor with live preview, and a
  Content Delivery API endpoint for headless

> Calendar data is fetched and displayed by software. Output is shown to editors and site
> visitors with its source and last-updated time; the package makes no automated decisions about
> people (EU AI Act transparency).

## Install

```sh
dotnet add package Knowit.Umbraco.Calendar
```

The package is a Razor Class Library and self-registers on startup (composer + auto-discovered
property editor, controllers, and view component). No `Program.cs` changes are required.

## Configure

Add an allowlist of calendars under `Knowit:Calendar`. **Secrets must come from a secret store**
(user-secrets in development, environment variables or Azure Key Vault in production) — never
commit them to `appsettings.json`.

```jsonc
{
  "Knowit": {
    "Calendar": {
      "DefaultCacheSeconds": 300,
      "StaleFallbackHours": 24,
      "Google": {
        "Enabled": true,
        // From a secret store, not here:
        "ServiceAccountJson": "",
        "AllowedCalendars": [
          { "id": "team@knowit.dk", "displayName": "Team calendar" }
        ]
      },
      "Microsoft": {
        "Enabled": true,
        // From a secret store, not here:
        "TenantId": "", "ClientId": "", "ClientSecret": "",
        "AllowedCalendars": [
          { "mailbox": "events@knowit.dk", "calendarId": "primary", "displayName": "Events" }
        ]
      }
    }
  }
}
```

Editors can only ever pick a calendar from `AllowedCalendars`.

### Google setup

1. In Google Cloud, create a **service account** and download its JSON key.
2. Enable the **Google Calendar API** for the project.
3. **Share each calendar** you want to expose with the service-account email address
   (read access is enough). The service account has no calendar of its own.
4. Put the JSON in `Knowit:Calendar:Google:ServiceAccountJson` via your secret store.

### Microsoft setup

1. Register an application in Entra ID (Azure AD).
2. Grant the **application** permission `Calendars.Read` (Microsoft Graph) and **grant admin
   consent**.
3. Create a client secret.
4. **Constrain access.** `Calendars.Read` (application) can read *every* mailbox in the tenant.
   Limit the app to only the intended mailboxes with an
   [application access policy](https://learn.microsoft.com/graph/auth-limit-mailbox-access).
   This is the most important security step.
5. Put `TenantId`, `ClientId`, `ClientSecret` in your secret store.

## Use

### Frontend (Razor)

The picked value is exposed as a typed `CalendarRef`:

```cshtml
@* View Component (default view is overridable) *@
@await Component.InvokeAsync("KnowitCalendar",
    new { calendar = Model.Value<CalendarRef>("eventCalendar"), days = 30 })

@* Tag Helper (requires @addTagHelper *, Knowit.Umbraco.Calendar in _ViewImports) *@
<knowit-calendar calendar="@Model.Value<CalendarRef>("eventCalendar")" days="30" max="20" />
```

Override the default markup by placing your own file at
`Views/Shared/Components/KnowitCalendar/Default.cshtml`.

### Headless (Content Delivery API)

```
GET /umbraco/delivery/api/v2/knowit-calendar/calendars
GET /umbraco/delivery/api/v2/knowit-calendar/events?providerKey=microsoft&calendarId=events@knowit.dk&days=30
```

Only allowlisted calendars are served. Requires the Delivery API to be enabled on the site.

## Caching & resilience

Provider responses are cached (`DefaultCacheSeconds`, with jitter) via `IDistributedCache`, so a
provider is never hit synchronously in a request hot path. If a provider call fails, the last
known-good result is served for up to `StaleFallbackHours` instead of breaking the page. For a
load-balanced backoffice, register a real distributed cache.

## Observability

Metrics are published on the meter **`Knowit.Umbraco.Calendar`** (collect via OpenTelemetry or
`dotnet-counters`): `cache.hits`, `cache.misses`, `cache.stale_served`, `provider.errors`.

## Data protection (GDPR)

Calendar events can contain personal data (organizer, attendees, subjects).

- Event **descriptions are sanitized** (Google HTML) / HTML-encoded (Microsoft) before they reach
  any consumer, so they are safe to render.
- Cached data has a **short TTL**; use an encrypted distributed cache where calendar contents are
  sensitive.
- The package does **not** request attendee lists.
- You are the data controller for the calendars you expose. Only expose calendars whose contents
  are appropriate for the audience (public site visitors and/or backoffice editors).

## License

See repository.
