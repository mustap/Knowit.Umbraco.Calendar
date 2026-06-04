using System.Globalization;
using Knowit.Umbraco.Calendar.Core.Models;

namespace Knowit.Umbraco.Calendar.Rendering;

/// <summary>
/// View model for the calendar view component. Supports month / day / year / list (agenda) views,
/// anchored on a date, with grid helpers.
/// </summary>
public sealed class CalendarRenderModel
{
    public const string Month = "month";
    public const string Day = "day";
    public const string Year = "year";
    public const string List = "list";

    private static readonly string[] KnownViews = [Month, Day, Year, List];

    private readonly Dictionary<string, int> _order = new();

    public CalendarRenderModel(
        CalendarRef calendar,
        IReadOnlyList<CalendarEvent> events,
        string view,
        DateOnly anchor)
    {
        Calendar = calendar;
        Events = events;
        View = KnownViews.Contains(view, StringComparer.OrdinalIgnoreCase)
            ? view.ToLowerInvariant()
            : Month;
        Anchor = anchor;
        Uid = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < events.Count; i++)
        {
            _order[events[i].Id] = i;
        }
    }

    public CalendarRef Calendar { get; }

    public IReadOnlyList<CalendarEvent> Events { get; }

    /// <summary>"month" | "day" | "year" | "list".</summary>
    public string View { get; }

    /// <summary>The focused date (drives the month/day/year shown).</summary>
    public DateOnly Anchor { get; }

    public string Uid { get; }

    public DateOnly MonthStart => new(Anchor.Year, Anchor.Month, 1);

    public DateOnly GridStart => MondayOnOrBefore(MonthStart);

    public int Year_ => Anchor.Year;

    /// <summary>Previous anchor for the nav arrow, by view unit (day/month/year).</summary>
    public DateOnly PrevAnchor => View switch
    {
        Day => Anchor.AddDays(-1),
        Year => Anchor.AddYears(-1),
        _ => Anchor.AddMonths(-1),
    };

    public DateOnly NextAnchor => View switch
    {
        Day => Anchor.AddDays(1),
        Year => Anchor.AddYears(1),
        _ => Anchor.AddMonths(1),
    };

    /// <summary>Header label for the current view.</summary>
    public string HeaderLabel => View switch
    {
        Day => Anchor.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture),
        Year => Anchor.Year.ToString(CultureInfo.CurrentCulture),
        List => "Upcoming events",
        _ => MonthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
    };

    public static DateOnly MondayOnOrBefore(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
        return date.AddDays(-offset);
    }

    /// <summary>The 42 cells (6 weeks) of a month's grid.</summary>
    public IEnumerable<DateOnly> GridDaysOf(DateOnly month)
    {
        var start = MondayOnOrBefore(new DateOnly(month.Year, month.Month, 1));
        for (var i = 0; i < 42; i++)
        {
            yield return start.AddDays(i);
        }
    }

    /// <summary>Month grid for the anchored month.</summary>
    public IEnumerable<DateOnly> GridDays() => GridDaysOf(MonthStart);

    /// <summary>The 12 months of the anchored year.</summary>
    public IEnumerable<DateOnly> MonthsOfYear()
    {
        for (var m = 1; m <= 12; m++)
        {
            yield return new DateOnly(Anchor.Year, m, 1);
        }
    }

    /// <summary>Events on a given day (handles all-day spans), ordered all-day first then by time.</summary>
    public IEnumerable<CalendarEvent> EventsOn(DateOnly day)
        => Events.Where(e => Covers(e, day)).OrderByDescending(e => e.IsAllDay).ThenBy(e => e.Start);

    public bool HasEventsOn(DateOnly day) => Events.Any(e => Covers(e, day));

    public int CountOn(DateOnly day) => Events.Count(e => Covers(e, day));

    /// <summary>Stable DOM id for an event's detail dialog (and its openers).</summary>
    public string DomId(CalendarEvent e)
        => $"kc-{Uid}-{(_order.TryGetValue(e.Id, out var i) ? i : 0)}";

    private static bool Covers(CalendarEvent e, DateOnly day)
    {
        DateOnly start, end;

        if (e.IsAllDay)
        {
            // All-day events are date-only at midnight UTC with an exclusive end.
            start = DateOnly.FromDateTime(e.Start.UtcDateTime);
            end = DateOnly.FromDateTime(e.End.UtcDateTime);
            if (end > start)
            {
                end = end.AddDays(-1);
            }
        }
        else
        {
            start = DateOnly.FromDateTime(e.Start.LocalDateTime);
            end = DateOnly.FromDateTime(e.End.LocalDateTime);
        }

        if (end < start)
        {
            end = start;
        }

        return day >= start && day <= end;
    }
}
