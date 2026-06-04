using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Models;

namespace Knowit.Umbraco.Calendar.Core.Providers.Google;

/// <summary>
/// Read-only Google Calendar provider using a service account. Each exposed calendar must be
/// shared with the service-account email (read access is sufficient).
/// </summary>
public sealed class GoogleCalendarProvider : ICalendarProvider, IDisposable
{
    public const string ProviderKey = "google";

    private readonly GoogleOptions _options;
    private readonly CalendarService _service;

    public GoogleCalendarProvider(GoogleOptions options)
    {
        _options = options;

        var credential = CredentialFactory
            .FromJson<ServiceAccountCredential>(options.ServiceAccountJson)
            .ToGoogleCredential()
            .CreateScoped(CalendarService.Scope.CalendarEventsReadonly);

        _service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Knowit.Umbraco.Calendar",
        });
    }

    public string Key => ProviderKey;

    public Task<IReadOnlyList<CalendarRef>> ListCalendarsAsync(CancellationToken ct = default)
    {
        // The admin-configured allowlist is the source of truth for what editors can pick.
        IReadOnlyList<CalendarRef> refs = _options.AllowedCalendars
            .Select(c => new CalendarRef(ProviderKey, c.Id, c.DisplayName))
            .ToList();

        return Task.FromResult(refs);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string calendarId,
        CalendarQuery query,
        CancellationToken ct = default)
    {
        var results = new List<CalendarEvent>();
        string? pageToken = null;

        do
        {
            var request = _service.Events.List(calendarId);
            request.TimeMinDateTimeOffset = query.From;
            request.TimeMaxDateTimeOffset = query.To;
            request.SingleEvents = query.ExpandRecurring;
            // startTime ordering is only valid when expanding the series into single events.
            request.OrderBy = query.ExpandRecurring
                ? EventsResource.ListRequest.OrderByEnum.StartTime
                : EventsResource.ListRequest.OrderByEnum.Updated;
            request.ShowDeleted = false;
            request.MaxResults = Math.Clamp(query.MaxResults, 1, 2500);
            request.PageToken = pageToken;

            var response = await request.ExecuteAsync(ct).ConfigureAwait(false);

            foreach (var e in response.Items ?? [])
            {
                if (string.Equals(e.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(GoogleEventMapper.Map(e));
                if (results.Count >= query.MaxResults)
                {
                    return results;
                }
            }

            pageToken = response.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken) && !ct.IsCancellationRequested);

        return results;
    }

    public void Dispose() => _service.Dispose();
}
