using System.Globalization;
using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Knowit.Umbraco.Calendar.Rendering;

/// <summary>
/// Renders a calendar via a Razor view. Invoke from a template:
/// <c>@await Component.InvokeAsync("KnowitCalendar", new { calendar })</c>.
/// The view (month/day/year/list) and focused date are read from the query string
/// (<c>?kcView=</c>, <c>?kcDate=yyyy-MM-dd</c>) so the built-in switcher and navigation work.
/// The default view ships in the package and can be overridden by the consuming site at
/// <c>Views/Shared/Components/KnowitCalendar/Default.cshtml</c>.
/// </summary>
public sealed class KnowitCalendarViewComponent : ViewComponent
{
    private readonly ICalendarService _calendarService;

    public KnowitCalendarViewComponent(ICalendarService calendarService)
        => _calendarService = calendarService;

    /// <param name="calendar">The calendar to render.</param>
    /// <param name="view">Default view when none is in the query string.</param>
    /// <param name="days">Window (days) for the list/agenda view.</param>
    public async Task<IViewComponentResult> InvokeAsync(
        CalendarRef? calendar,
        string view = CalendarRenderModel.Month,
        int days = 60)
    {
        if (calendar is null)
        {
            return Content(string.Empty);
        }

        var query = HttpContext?.Request.Query;
        var resolvedView = (query?["kcView"].ToString() is { Length: > 0 } v ? v : view).ToLowerInvariant();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var anchor = today;
        if (query?["kcDate"].ToString() is { Length: > 0 } d
            && DateOnly.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            anchor = parsed;
        }

        var (from, to, max) = resolvedView switch
        {
            CalendarRenderModel.Day => (Utc(anchor.AddDays(-1)), Utc(anchor.AddDays(2)), 250),
            CalendarRenderModel.Year => (Utc(new DateOnly(anchor.Year, 1, 1).AddDays(-1)),
                                         Utc(new DateOnly(anchor.Year + 1, 1, 1).AddDays(1)), 1000),
            CalendarRenderModel.List => (DateTimeOffset.Now, DateTimeOffset.Now.AddDays(Math.Clamp(days, 1, 366)), 250),
            _ => (Utc(CalendarRenderModel.MondayOnOrBefore(new DateOnly(anchor.Year, anchor.Month, 1)).AddDays(-1)),
                  Utc(CalendarRenderModel.MondayOnOrBefore(new DateOnly(anchor.Year, anchor.Month, 1)).AddDays(43)), 250),
        };

        var events = await _calendarService.GetEventsAsync(calendar, new CalendarQuery(from, to, max));

        return View(new CalendarRenderModel(calendar, events, resolvedView, anchor));
    }

    private static DateTimeOffset Utc(DateOnly date)
        => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
