# Knowit.Umbraco.Calendar — Architecture & Build Plan

**Status:** Draft v0.1 · **Date:** 3 June 2026 (`2026-06-03`)

---

## 1. Purpose and scope

A distributable NuGet package that lets **editors** in Umbraco display Google and Microsoft
calendar events on a website — without editors ever touching OAuth, service accounts, or
`appsettings.json`.

### 1.1 Role split (the load-bearing principle)

| Role | What they do | What they do *not* do |
|---|---|---|
| **Developer / admin** | Installs the package, creates a Google service account + Microsoft app registration, puts credentials in a secret store, defines which calendars are available | Doesn't touch content day to day |
| **Editor** | Picks a calendar per page via a property editor, sees a preview in the backoffice, adjusts display options | Doesn't touch credentials or configuration |

The whole architecture is designed around this split.

### 1.2 Scope decisions (v1)

*Interpretation of "useful for editors" → concrete choices. These are recommendations, not your hard requirements — say the word if any should change.*

| Decision | v1 choice | Rationale |
|---|---|---|
| **Direction** | **Read-only** (display events) | Editors almost always want to *show* a calendar. Two-way sync needs write scopes, conflict resolution, and idempotency — the wrong default for a reusable v1. Write-back is a later phase. |
| **Auth** | **App-level** (one Google service account + one Microsoft app registration with *application permissions*). Per-user OAuth is opt-in phase 2 | Editors can't and shouldn't run OAuth consent. Storing per-editor refresh tokens is a security and GDPR burden. |
| **Configuration model** | **Both** — developer configures credentials + allowed calendars globally; editor **picks a calendar per node** via a property editor | The editor-friendly core: a dropdown of allowed calendars on a page, no config files. |
| **Surface** | Frontend rendering (Tag Helper / View Component) **+** Bellissima backoffice property editor with live preview **+** Content Delivery API extension (headless) | Editors *pick and preview* in the backoffice; the site *renders*; headless setups get an *API*. |

### 1.3 Explicitly out of scope for v1

- Two-way sync / creating events from Umbraco.
- Per-editor personal calendars (delegated OAuth).
- Webhook-based push (Google `watch` / Graph subscriptions) — v1 uses polling + cache.
- iCal/CalDAV providers beyond Google/Microsoft.

---

## 2. Platform foundation (verified)

> **Fact.** Umbraco 17 is an **LTS** released **27 November 2025** (`2025-11-27`), built on
> **.NET 10 LTS**, supported until **November 2028**. Relevant new features: load-balanced
> backoffice and consistent **UTC date handling with time-zone support**.¹

> **Fact.** The backoffice is "Bellissima" (Umbraco 14+): web components built with **Lit +
> TypeScript**. Extensions live in `App_Plugins` and are registered in **`umbraco-package.json`**.
> A property editor has two parts: a **Property Editor Schema** (the data side, C#) and a
> **Property Editor UI** (a web component).²

> **Fact.** Packages are distributed as **NuGet via a Razor Class Library (RCL)** (full support
> since Umbraco 11). `App_Plugins` files go in the RCL's `wwwroot` and are served automatically at
> the consuming site — no `.targets` file or copying required.³

> **Fact.** `UmbracoApiController` was **removed in Umbraco 15+**. Custom Delivery API endpoints
> inherit from `ContentApiControllerBase`. Notification handlers are registered via `IComposer`
> using `builder.AddNotificationHandler<TNotification, THandler>()`; the Delivery API is enabled
> with `.AddDeliveryApi()`.⁴

**Target framework:** `net10.0`. The package targets Umbraco 17 LTS (CMS `>= 17.0.0 < 18.0.0`).

---

## 3. High-level architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                     Consuming Umbraco 17 site                          │
│                                                                        │
│  Frontend (Razor)        Backoffice (Bellissima)      Headless         │
│  ┌──────────────┐        ┌────────────────────┐      ┌─────────────┐   │
│  │ <calendar/>  │        │ Calendar Picker UI │      │ Delivery API│   │
│  │ TagHelper /  │        │ + Live Preview     │      │ extension   │   │
│  │ ViewComponent│        │ (web component)    │      │ endpoint    │   │
│  └──────┬───────┘        └─────────┬──────────┘      └──────┬──────┘   │
│         │                          │                        │          │
│         └──────────────┬───────────┴────────────────────────┘          │
│                        ▼                                                │
│            ┌────────────────────────────┐                              │
│            │   ICalendarService (facade) │  ← typed, DI-registered      │
│            └─────────────┬──────────────┘                              │
│                          ▼                                              │
│            ┌────────────────────────────┐                              │
│            │  Caching layer (IMemoryCache│                              │
│            │  / IDistributedCache)       │  ← TTL + jitter, stale-fallback│
│            └─────────────┬──────────────┘                              │
│                          ▼                                              │
│        ┌─────────────────────────────────────────┐                     │
│        │   ICalendarProvider (abstraction)        │                     │
│        ├──────────────────────┬──────────────────┤                     │
│        │ GoogleCalendarProvider│ MicrosoftGraph…  │                     │
│        │ Google.Apis.Calendar  │ Microsoft.Graph  │                     │
│        └──────────┬───────────┴────────┬─────────┘                     │
└───────────────────┼────────────────────┼───────────────────────────────┘
                    ▼                     ▼
            Google Calendar API     Microsoft Graph API
         (service account creds)   (app registration, client credentials)
```

**Design principles**

1. **Provider-agnostic core.** Frontend, backoffice, and API only talk to `ICalendarService`,
   which returns a normalized domain model — never Google or Graph types.
2. **The read path is always cached.** The external API is never hit synchronously in a request
   hot path without going through cache with TTL + stale-while-revalidate fallback.
3. **Secrets never leave the server.** Credentials are injected via `IOptions`/secret store; the
   editor UI only ever sees calendar *names/IDs*, never tokens.
4. **Per-provider fault isolation.** If Google fails, Microsoft must still render (and vice versa).

---

## 4. Solution structure

```
Knowit.Umbraco.Calendar.sln
│
├── src/
│   ├── Knowit.Umbraco.Calendar.Core/            (net10.0 — no Umbraco dependency)
│   │   ├── Abstractions/   ICalendarProvider, ICalendarService, models
│   │   ├── Models/         CalendarEvent, CalendarRef, CalendarQuery, EventOccurrence
│   │   ├── Providers/
│   │   │   ├── Google/     GoogleCalendarProvider, GoogleOptions
│   │   │   └── Microsoft/  MicrosoftGraphProvider, MicrosoftOptions
│   │   ├── Caching/        CachingCalendarService (decorator)
│   │   └── Configuration/  CalendarOptions, provider options, validation
│   │
│   ├── Knowit.Umbraco.Calendar/                  (net10.0 — RCL; Umbraco integration)
│   │   ├── Composing/      CalendarComposer (IComposer): DI + handlers
│   │   ├── PropertyEditors/CalendarPickerConfiguration (Schema, C#)
│   │   ├── Controllers/    Backoffice management API (calendar list, preview)
│   │   ├── DeliveryApi/    Calendar Delivery API extension
│   │   ├── Rendering/      CalendarTagHelper, CalendarViewComponent
│   │   └── wwwroot/
│   │       └── App_Plugins/Knowit.Umbraco.Calendar/
│   │           ├── umbraco-package.json
│   │           └── dist/   (bundled TS → JS: picker UI + preview web components)
│   │
│   └── Knowit.Umbraco.Calendar.Client/           (TypeScript/Lit source → built to dist/)
│       ├── package.json, vite.config.ts, tsconfig.json
│       └── src/  calendar-picker.element.ts, calendar-preview.element.ts, api/, context/
│
├── tests/
│   ├── Knowit.Umbraco.Calendar.Core.Tests/       (unit: providers vs fakes, caching)
│   └── Knowit.Umbraco.Calendar.Integration.Tests/(against real test calendars, opt-in)
│
└── samples/
    └── Sample.Umbraco17.Site/                    (demo site, not packed)
```

**Why two C# projects?** `Core` has zero Umbraco dependency → testable in isolation, and reusable
in a pure-API/worker context later. `Knowit.Umbraco.Calendar` (the RCL) is the only NuGet ID the
consumer installs; it pulls `Core` in as a dependency.

---

## 5. Domain model (normalized)

Both providers map to one model, so rendering never knows the provider type.

```csharp
public sealed record CalendarRef(
    string ProviderKey,      // "google" | "microsoft"
    string CalendarId,       // provider-specific id (opaque to the editor)
    string DisplayName);

public sealed record CalendarEvent(
    string Id,
    string Title,
    string? Description,      // HTML sanitized before rendering
    string? Location,
    DateTimeOffset Start,     // always normalized to UTC (per Umbraco 17 UTC handling)
    DateTimeOffset End,
    bool IsAllDay,
    string? OrganizerName,
    string? HtmlLink,         // "open in Google/Outlook"
    OnlineMeeting? OnlineMeeting,  // Teams/Meet join link, if any
    IReadOnlyList<string> Categories,
    RecurrenceInfo? Recurrence);

public sealed record OnlineMeeting(
    string JoinUrl,           // the "join" link rendered as a button
    OnlineMeetingProvider Provider,
    string? ProviderDisplayName);  // raw provider label, e.g. "Google Meet"

public enum OnlineMeetingProvider { Unknown, MicrosoftTeams, GoogleMeet, Other }

public sealed record CalendarQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    int MaxResults = 250,
    bool ExpandRecurring = true);   // → calendarView / singleEvents=true
```

**Online-meeting mapping.** Both providers expose conference info, under different shapes — we
normalize to `OnlineMeeting` (null when the event isn't online):
- **Google:** prefer `event.ConferenceData.EntryPoints` where `EntryPointType == "video"` → `Uri`
  (fall back to the legacy `event.HangoutLink`). Provider derived from
  `ConferenceData.ConferenceSolution` (`key.type == "hangoutsMeet"` → `GoogleMeet`).
- **Microsoft:** `event.OnlineMeeting?.JoinUrl`, gated by `event.IsOnlineMeeting`; provider from
  `event.OnlineMeetingProvider` (e.g. `teamsForBusiness` → `MicrosoftTeams`).

**Time zones.** We store/transport **UTC** and let the rendering layer format to the site/culture.
This aligns with Umbraco 17's UTC model.¹

**Recurring events.** We expand at *read time*:
- Google: `Events.List` with `singleEvents=true` + `orderBy=startTime`.
- Microsoft: use **`calendarView`** (not `events`) — it returns occurrences/exceptions within a
  time range.⁴ᵃ

---

## 6. Provider abstraction

```csharp
public interface ICalendarProvider
{
    string Key { get; }                                   // "google" | "microsoft"
    Task<IReadOnlyList<CalendarRef>> ListCalendarsAsync(CancellationToken ct);
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string calendarId, CalendarQuery query, CancellationToken ct);
}
```

### 6.1 Google provider

> **Fact.** `Google.Apis.Calendar.v3` (Google APIs Client Library for .NET) is used with a
> **`ServiceAccountCredential`** loaded from service-account JSON. The service account has **no
> calendar of its own** — the actual calendar must be **shared** with the service-account email
> (read-only is enough). The library is in *maintenance mode*.⁵

```csharp
var credential = GoogleCredential
    .FromJson(options.ServiceAccountJson)
    .CreateScoped(CalendarService.Scope.CalendarEventsReadonly);

var service = new CalendarService(new BaseClientService.Initializer
{
    HttpClientInitializer = credential,
    ApplicationName = "Knowit.Umbraco.Calendar"
});

var request = service.Events.List(calendarId);
request.TimeMinDateTimeOffset = query.From;
request.TimeMaxDateTimeOffset = query.To;
request.SingleEvents = query.ExpandRecurring;     // expand recurrences
request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
request.MaxResults = query.MaxResults;
// handle NextPageToken pagination
```

**Admin setup (documented in README):** create a service account → download JSON → share each
desired calendar with the service-account email via Google Calendar settings.

### 6.2 Microsoft provider

> **Fact.** Microsoft Graph .NET SDK v6 (`Microsoft.Graph` 6.x) + `Azure.Identity`. For app-level
> access, use **client credentials** (`ClientSecretCredential`) with the **application permission
> `Calendars.Read`** ("Read calendars in all mailboxes", **requires admin consent**). To read
> expanded events, use **`calendarView`** with a time range. Time zone is controlled via the
> `Prefer: outlook.timezone` header.⁴ᵃ ⁶

```csharp
var credential = new ClientSecretCredential(
    options.TenantId, options.ClientId, options.ClientSecret);

var graph = new GraphServiceClient(credential,
    new[] { "https://graph.microsoft.com/.default" });

var view = await graph.Users[mailboxUpn].Calendars[calendarId].CalendarView
    .GetAsync(rc =>
    {
        rc.QueryParameters.StartDateTime = query.From.ToString("o");
        rc.QueryParameters.EndDateTime   = query.To.ToString("o");
        rc.QueryParameters.Top           = query.MaxResults;
        rc.Headers.Add("Prefer", "outlook.timezone=\"UTC\"");
    }, ct);
```

> **Security recommendation.** `Calendars.Read` (application) grants access to *all* mailboxes in
> the tenant. Document strongly that the admin **should** narrow this with an **application access
> policy** so the app can only read the intended mailboxes/calendars.⁶ This is the single most
> important security recommendation in the whole package.

---

## 7. Caching and refresh

*Recommendation.*

- **Decorator pattern:** `CachingCalendarService` wraps `ICalendarService` → providers are never
  hit directly from a request.
- **Backing store:** `IDistributedCache` where available (load-balanced backoffice in U17¹),
  otherwise `IMemoryCache`. Key = `provider:calendarId:from:to:hash(query)`.
- **TTL:** default 5 min (configurable per provider), + random **jitter** to avoid a thundering
  herd under load balancing.
- **Stale-while-revalidate:** on provider failure, return the last cached result + log a warning
  instead of breaking the page render.
- **Rate-limit respect:** retry with exponential backoff on Google 403 `rateLimitExceeded` and
  Graph 429 (`Retry-After`).
- **Phase 2:** webhooks (Google `events.watch`, Graph change notifications) → cache invalidation
  instead of pure polling.

---

## 8. Backoffice — property editor (the editor's touch point)

Two parts, per Umbraco 17's property-editor model².

### 8.1 Schema (C#)
Defines the "Calendar Picker" data type. The stored value = a serialized `CalendarRef` (provider +
calendarId + display options), **not** event data.

### 8.2 UI (Lit web component, TypeScript)
`umbraco-package.json` registers:

```jsonc
{
  "name": "Knowit.Umbraco.Calendar",
  "extensions": [
    {
      "type": "propertyEditorUi",
      "alias": "Knowit.Umbraco.Calendar.Picker",
      "name": "Calendar Picker",
      "element": "/App_Plugins/Knowit.Umbraco.Calendar/dist/calendar-picker.element.js",
      "meta": {
        "label": "Calendar",
        "propertyEditorSchemaAlias": "Knowit.Umbraco.Calendar.Picker",
        "icon": "icon-calendar",
        "group": "Pickers"
      }
    }
  ]
}
```

**Editor's flow:**
1. Add a "Calendar Picker" property on a Document Type.
2. On the page: a dropdown fetches **only the allowed calendars** from a backoffice management
   controller (`/umbraco/management/api/v1/knowit-calendar/calendars`) → which calls
   `ICalendarService.ListCalendarsAsync`.
3. A **live preview** web component shows the next events right inside the editor, so the editor
   sees what they're choosing before saving.

> **EU AI Act / transparency:** preview and frontend clearly label that data comes from an
> external calendar (source + last-updated timestamp). No automated decisions are made about people.

---

## 9. Frontend rendering

Ship **both** a Tag Helper and a View Component (different team preferences):

```cshtml
@* Tag Helper *@
<knowit-calendar property="@Model.Value("eventCalendar")"
                 from="today" days="30" view="list" max="20" />

@* View Component *@
@await Component.InvokeAsync("KnowitCalendar",
    new { calendar = Model.Value<CalendarRef>("eventCalendar"), days = 30 })
```

- Default Razor views (list + month) ship in the RCL and can be **overridden** by the consumer by
  placing their own views in their project.
- Output is semantic HTML + optional `application/ld+json` `Event` schema.org for SEO.
- HTML in event descriptions is **sanitized** (HtmlSanitizer) before rendering (XSS).
- Dates are formatted to the site culture.

---

## 10. Headless — Content Delivery API extension

> **Fact.** Custom Delivery API endpoints inherit `ContentApiControllerBase`; the Delivery API is
> enabled via `.AddDeliveryApi()`.⁴

Expose events for SPA/headless frontends:

```
GET /umbraco/delivery/api/v1/knowit-calendar/{contentKey}/events?from=&to=
```

- Returns normalized `CalendarEvent` JSON (not raw Google/Graph).
- Respects the Delivery API's existing auth (API key / public) — no new auth surface.
- Reuses the same cached `ICalendarService` as the frontend → consistency.

---

## 11. Configuration

```jsonc
// appsettings.json (set by developer/admin)
{
  "Knowit": {
    "Calendar": {
      "DefaultCacheSeconds": 300,
      "Google": {
        "Enabled": true,
        "ServiceAccountJson": "",          // ← from user-secrets / Key Vault, NOT here
        "AllowedCalendars": [
          { "id": "team@knowit.dk", "displayName": "Team calendar" }
        ]
      },
      "Microsoft": {
        "Enabled": true,
        "TenantId": "", "ClientId": "", "ClientSecret": "",   // ← secret store
        "AllowedCalendars": [
          { "mailbox": "events@knowit.dk", "calendarId": "primary", "displayName": "Events" }
        ]
      }
    }
  }
}
```

- **`AllowedCalendars`** is the allowlist the editor's dropdown is built from → the editor can
  never pick a calendar the admin hasn't approved.
- `IValidateOptions<CalendarOptions>` validates at startup (fail fast if an enabled provider is
  missing credentials).
- **Secrets:** document user-secrets (dev) and Azure Key Vault / env vars (prod). Secrets must
  **never** sit in `appsettings.json` in source control.

---

## 12. Security, operations, and compliance

- **Least privilege:** read-only scopes (`CalendarEventsReadonly` / `Calendars.Read`).
- **Microsoft tenant scoping:** require documentation of an *application access policy* (§6.2).⁶
- **Secret handling:** Key Vault / user-secrets; secrets are never logged.
- **HTML sanitization** of event descriptions before rendering.
- **PII / GDPR:** calendar events can contain personal data (attendees, subjects).
  - Cache TTL is kept short; cached data is encrypted when using a distributed cache where possible.
  - The README gets a data-processing section: which data is fetched, where it's cached, for how long.
  - Default: don't fetch attendee lists unless explicitly enabled.
- **EU AI Act / governance:** this is not an AI decision system, but the deliverable clearly labels
  data source + update time (transparency). No automated decisions about people.
- **Observability:** structured logging + `Meter`-based metrics (cache hit rate, provider latency,
  error rate per provider) so operations can see health.

---

## 13. Test strategy

| Level | What | How |
|---|---|---|
| Unit | Provider mapping (Google/Graph DTO → `CalendarEvent`), caching decorator, options validation | xUnit; providers tested against recorded JSON responses |
| Contract | `ICalendarProvider` behavior consistent across providers | Shared test suite run against each provider fake |
| Integration (opt-in) | Real Google test calendar + Microsoft test tenant | CI only with secrets; skipped locally |
| Backoffice | Picker web component | Vitest + optional Playwright against the sample site |
| Sample site | End-to-end render | `samples/Sample.Umbraco17.Site` |

---

## 14. Step-by-step build plan

> Phases are sequential; each is a releasable milestone.

### Phase 0 — Foundation
1. Create the solution + the three `src` projects (`net10.0`), test projects, sample site.
2. Add `Umbraco.Cms` `17.x` to the RCL project; set up `<RazorClassLibrary>` and the
   `App_Plugins` folder under `wwwroot`.³
3. CI pipeline: build + test + `dotnet pack`.

### Phase 1 — Core domain + Google provider
4. Define the domain model (§5) and `ICalendarProvider` / `ICalendarService` (§6).
5. Implement `GoogleCalendarProvider` with `ServiceAccountCredential` + pagination (§6.1).⁵
6. Unit tests against recorded Google responses. Verify against one real shared test calendar.
7. **Milestone:** can fetch normalized events from Google via a console/test harness.

### Phase 2 — Microsoft provider + caching
8. Implement `MicrosoftGraphProvider` via `calendarView` + client credentials (§6.2).⁴ᵃ⁶
9. Implement the `CachingCalendarService` decorator (TTL, jitter, stale fallback, backoff) (§7).
10. Options + `IValidateOptions` (§11).
11. **Milestone:** both providers behind one cached facade, fault-isolated.

### Phase 3 — Umbraco integration: composer + frontend
12. `CalendarComposer : IComposer` — register providers, facade, options, handlers.⁴
13. Property Editor **Schema** (C#) for "Calendar Picker" (§8.1).
14. Backoffice **management controller**: list allowed calendars + preview endpoint.
15. `CalendarTagHelper` + `CalendarViewComponent` + default Razor views (§9).
16. **Milestone:** a developer can place a property, pick a calendarId in config, and render on the sample site.

### Phase 4 — Backoffice UI (Bellissima)
17. Set up the TypeScript/Lit project (`Client/`) with Vite bundling to the RCL's `dist/`.
18. `calendar-picker.element.ts` — dropdown from the management controller (§8.2).
19. `calendar-preview.element.ts` — live preview of upcoming events.
20. `umbraco-package.json` registration; verify in a running backoffice.²
21. **Milestone:** editor picks a calendar + sees a preview, end to end.

### Phase 5 — Headless + hardening
22. Content Delivery API extension (`ContentApiControllerBase`) (§10).⁴
23. HTML sanitization, schema.org output, culture-aware date formatting.
24. Logging/metrics, secret documentation, GDPR section in README (§12).
25. **Milestone:** feature-complete v1.

### Phase 6 — Release
26. README + setup guides (Google service account, Microsoft app registration + access policy).
27. Version (SemVer), NuGet metadata (icon, license, readme, repository URL).
28. Publish to internal/public NuGet feed; add to the Umbraco Marketplace (optional).
29. Tag the sample site as living documentation.

### Later phases (post-v1)
- **Phase 7:** Per-user delegated OAuth (editors' personal calendars).
- **Phase 8:** Webhooks → cache invalidation instead of polling.
- **Phase 9:** Write-back / two-way sync (needs conflict design + idempotency).

---

## 15. Risks and open questions

| # | Topic | Question / risk | Suggestion |
|---|---|---|---|
| 1 | Microsoft permissions | `Calendars.Read` (app) is broad → tenant admins may hesitate | Document an application access policy; consider group calendars via `Group.Read.All` for scoping |
| 2 | Google maintenance mode | The client library is minimally maintained⁵ | Acceptable (stable API); the abstraction allows switching to raw REST later |
| 3 | Recurring events | Time-zone edge cases in expansion | Test explicitly across DST boundaries |
| 4 | Distributed cache | PII in a shared cache | Encrypt / short TTL / opt out of sensitive fields |
| 5 | Backoffice load balancing | U17 load-balanced backoffice¹ | Use `IDistributedCache`, no in-proc-only state |
| 6 | Licensing / commercial model | Open source vs. commercial package? | To be decided — affects NuGet metadata and support model |

---

## Sources

1. Umbraco 17 LTS — release (27 November 2025, .NET 10 LTS, support to Nov 2028, UTC/time zone, load-balanced backoffice): [umbraco.com/blog/umbraco-17-lts-release](https://umbraco.com/blog/umbraco-17-lts-release/) · [releases.umbraco.com — Umbraco-CMS 17.0.0](https://releases.umbraco.com/release/umbraco/Umbraco-CMS/17.0.0)
2. Property editors (Schema + UI), `umbraco-package.json`, App_Plugins, Lit/TypeScript: [docs.umbraco.com — Property Editors](https://docs.umbraco.com/umbraco-cms/extend-your-project/backoffice-extensions/property-editors) · [docs — Creating a Property Editor](https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-property-editor) · [docs — Umbraco Package](https://docs.umbraco.com/umbraco-cms/extend-your-project/backoffice-extensions/umbraco-package)
3. RCL packages / NuGet distribution / `wwwroot/App_Plugins`: [docs.umbraco.com — Creating a Package](https://docs.umbraco.com/umbraco-cms/extending/packages/creating-a-package) · [Enkel Media — Converting a Umbraco Package to an RCL](https://www.enkelmedia.se/blogg/2023/5/9/converting-a-umbraco-package-to-a-razor-class-library)
4. `UmbracoApiController` removed in v15; `ContentApiControllerBase`; `.AddDeliveryApi()`; notification handlers via `IComposer`: [docs — Custom Delivery API endpoints](https://docs.umbraco.com/umbraco-cms/reference/content-delivery-api/custom-delivery-api-endpoints) · [docs — Notification Handler](https://docs.umbraco.com/umbraco-cms/reference/notifications/notification-handler) · [docs — Composing](https://docs.umbraco.com/umbraco-cms/implementation/composing)
   - 4a. Microsoft Graph **List calendarView** (expanded occurrences, `Prefer: outlook.timezone`): [learn.microsoft.com — List calendarView](https://learn.microsoft.com/graph/api/calendar-list-calendarview?view=graph-rest-1.0)
5. Google Calendar API .NET client + service account (`ServiceAccountCredential`, calendar must be shared, maintenance mode): [developers.google.com — Calendar v3 .NET](https://developers.google.com/api-client-library/dotnet/apis/calendar/v3) · [github.com/googleapis/google-api-dotnet-client](https://github.com/googleapis/google-api-dotnet-client)
6. Microsoft Graph **List events** + permissions (`Calendars.Read` application, admin consent, application access policy): [learn.microsoft.com — List events](https://learn.microsoft.com/graph/api/user-list-events?view=graph-rest-1.0) · [learn.microsoft.com — Graph permissions reference](https://learn.microsoft.com/graph/permissions-reference#all-permissions)
