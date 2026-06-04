using Azure.Identity;
using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Models;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using MGraph = Microsoft.Graph.Models;

namespace Knowit.Umbraco.Calendar.Core.Providers.Microsoft;

/// <summary>
/// Read-only Microsoft 365 calendar provider using Microsoft Graph with app-level
/// (client-credentials) auth and the <c>calendarView</c> endpoint, which returns recurring
/// occurrences expanded within the requested window.
/// </summary>
public sealed class MicrosoftGraphProvider : ICalendarProvider
{
    public const string ProviderKey = "microsoft";

    private const string UtcPreferHeader = "outlook.timezone=\"UTC\"";

    private static readonly string[] EventSelect =
    [
        "id", "subject", "bodyPreview", "start", "end", "isAllDay", "location",
        "organizer", "webLink", "onlineMeeting", "isOnlineMeeting", "onlineMeetingProvider",
        "categories", "type", "seriesMasterId",
    ];

    private readonly MicrosoftOptions _options;
    private readonly GraphServiceClient _graph;

    public MicrosoftGraphProvider(MicrosoftOptions options)
    {
        _options = options;

        var credential = new ClientSecretCredential(
            options.TenantId, options.ClientId, options.ClientSecret);

        _graph = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    public string Key => ProviderKey;

    public Task<IReadOnlyList<CalendarRef>> ListCalendarsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<CalendarRef> refs = _options.AllowedCalendars
            .Select(c => new CalendarRef(ProviderKey, ComposeId(c.Mailbox, c.CalendarId), c.DisplayName))
            .ToList();

        return Task.FromResult(refs);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string calendarId,
        CalendarQuery query,
        CancellationToken ct = default)
    {
        var (mailbox, specificCalendarId) = ParseId(calendarId);
        var start = query.From.ToUniversalTime().ToString("o");
        var end = query.To.ToUniversalTime().ToString("o");
        var top = Math.Clamp(query.MaxResults, 1, 1000);

        // The default-calendar and specific-calendar request builders have distinct query-parameter
        // types, so the first-page call is branched; paging is shared via PageIterator below.
        MGraph.EventCollectionResponse? firstPage;
        if (string.IsNullOrEmpty(specificCalendarId))
        {
            firstPage = await _graph.Users[mailbox].CalendarView.GetAsync(rc =>
            {
                rc.QueryParameters.StartDateTime = start;
                rc.QueryParameters.EndDateTime = end;
                rc.QueryParameters.Top = top;
                rc.QueryParameters.Orderby = ["start/dateTime"];
                rc.QueryParameters.Select = EventSelect;
                rc.Headers.Add("Prefer", UtcPreferHeader);
            }, ct).ConfigureAwait(false);
        }
        else
        {
            firstPage = await _graph.Users[mailbox].Calendars[specificCalendarId].CalendarView.GetAsync(rc =>
            {
                rc.QueryParameters.StartDateTime = start;
                rc.QueryParameters.EndDateTime = end;
                rc.QueryParameters.Top = top;
                rc.QueryParameters.Orderby = ["start/dateTime"];
                rc.QueryParameters.Select = EventSelect;
                rc.Headers.Add("Prefer", UtcPreferHeader);
            }, ct).ConfigureAwait(false);
        }

        if (firstPage is null)
        {
            return [];
        }

        var results = new List<CalendarEvent>();
        var iterator = PageIterator<MGraph.Event, MGraph.EventCollectionResponse>.CreatePageIterator(
            _graph,
            firstPage,
            e =>
            {
                results.Add(MicrosoftEventMapper.Map(e));
                return results.Count < query.MaxResults; // false stops paging
            },
            // Re-apply the UTC timezone preference on follow-up page requests.
            request =>
            {
                request.Headers.Add("Prefer", UtcPreferHeader);
                return request;
            });

        await iterator.IterateAsync(ct).ConfigureAwait(false);
        return results;
    }

    private static string ComposeId(string mailbox, string? calendarId)
        => string.IsNullOrEmpty(calendarId) || string.Equals(calendarId, "primary", StringComparison.OrdinalIgnoreCase)
            ? mailbox
            : $"{mailbox}|{calendarId}";

    private static (string mailbox, string? calendarId) ParseId(string id)
    {
        var separator = id.IndexOf('|');
        return separator < 0
            ? (id, null)
            : (id[..separator], id[(separator + 1)..]);
    }
}
