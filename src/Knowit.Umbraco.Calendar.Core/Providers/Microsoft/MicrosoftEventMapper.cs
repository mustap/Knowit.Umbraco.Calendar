using System.Globalization;
using Knowit.Umbraco.Calendar.Core.Models;
using Knowit.Umbraco.Calendar.Core.Text;
using MGraph = Microsoft.Graph.Models;

namespace Knowit.Umbraco.Calendar.Core.Providers.Microsoft;

/// <summary>
/// Pure mapping from Microsoft Graph's <see cref="MGraph.Event"/> to the normalized
/// <see cref="CalendarEvent"/>. Kept free of I/O so it can be unit-tested against POCOs.
/// Times are assumed to be UTC (the provider requests <c>Prefer: outlook.timezone="UTC"</c>).
/// </summary>
public static class MicrosoftEventMapper
{
    public static CalendarEvent Map(MGraph.Event e)
    {
        return new CalendarEvent(
            Id: e.Id ?? string.Empty,
            Title: e.Subject ?? string.Empty,
            // bodyPreview is plain text — HTML-encode so Description is uniformly safe HTML.
            Description: HtmlSanitization.EncodeText(e.BodyPreview),
            Location: e.Location?.DisplayName,
            Start: ParseDate(e.Start),
            End: ParseDate(e.End),
            IsAllDay: e.IsAllDay ?? false,
            OrganizerName: e.Organizer?.EmailAddress?.Name ?? e.Organizer?.EmailAddress?.Address,
            HtmlLink: e.WebLink,
            OnlineMeeting: ResolveOnlineMeeting(e),
            Categories: e.Categories?.ToList() ?? [],
            Recurrence: ResolveRecurrence(e));
    }

    private static DateTimeOffset ParseDate(MGraph.DateTimeTimeZone? dtz)
    {
        if (string.IsNullOrEmpty(dtz?.DateTime))
        {
            return default;
        }

        var dt = DateTime.Parse(
            dtz.DateTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        return new DateTimeOffset(dt, TimeSpan.Zero);
    }

    private static OnlineMeeting? ResolveOnlineMeeting(MGraph.Event e)
    {
        if (e.OnlineMeeting?.JoinUrl is not { Length: > 0 } joinUrl)
        {
            return null;
        }

        var (provider, displayName) = Classify(e.OnlineMeetingProvider);
        return new OnlineMeeting(joinUrl, provider, displayName);
    }

    private static (OnlineMeetingProvider, string?) Classify(MGraph.OnlineMeetingProviderType? type) => type switch
    {
        MGraph.OnlineMeetingProviderType.TeamsForBusiness => (OnlineMeetingProvider.MicrosoftTeams, "Microsoft Teams"),
        MGraph.OnlineMeetingProviderType.SkypeForBusiness => (OnlineMeetingProvider.Other, "Skype for Business"),
        MGraph.OnlineMeetingProviderType.SkypeForConsumer => (OnlineMeetingProvider.Other, "Skype"),
        _ => (OnlineMeetingProvider.Unknown, null),
    };

    private static RecurrenceInfo? ResolveRecurrence(MGraph.Event e) => e.Type switch
    {
        // calendarView expands series, so we mostly see occurrences/exceptions.
        MGraph.EventType.Occurrence or MGraph.EventType.Exception
            => new RecurrenceInfo(IsInstance: true, SeriesId: e.SeriesMasterId, Rules: []),
        MGraph.EventType.SeriesMaster
            => new RecurrenceInfo(IsInstance: false, SeriesId: null, Rules: []),
        _ => null,
    };
}
