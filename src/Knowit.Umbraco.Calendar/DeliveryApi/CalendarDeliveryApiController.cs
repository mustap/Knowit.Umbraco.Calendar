using Asp.Versioning;
using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Delivery.Controllers;
using Umbraco.Cms.Api.Delivery.Filters;
using Umbraco.Cms.Api.Delivery.Routing;

namespace Knowit.Umbraco.Calendar.DeliveryApi;

/// <summary>
/// Public/headless calendar endpoint, surfaced through the Content Delivery API so SPA and
/// headless frontends can read events. Only admin-allowlisted calendars are exposed; events are
/// returned as the normalized <see cref="CalendarEvent"/> (never raw Google/Graph payloads).
/// Base route: <c>/umbraco/delivery/api/v2/knowit-calendar</c>.
/// </summary>
[ApiVersion("2.0")]
[VersionedDeliveryApiRoute("knowit-calendar")]
[ApiExplorerSettings(GroupName = "Knowit Calendar")]
[DeliveryApiAccess]
public sealed class CalendarDeliveryApiController : DeliveryApiControllerBase
{
    private readonly ICalendarService _calendarService;

    public CalendarDeliveryApiController(ICalendarService calendarService)
        => _calendarService = calendarService;

    /// <summary>Lists the calendars exposed to headless clients (the admin allowlist).</summary>
    [HttpGet("calendars")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<IEnumerable<CalendarRef>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendars(CancellationToken cancellationToken)
        => Ok(await _calendarService.GetAvailableCalendarsAsync(cancellationToken));

    /// <summary>Returns upcoming events for an allowlisted calendar.</summary>
    [HttpGet("events")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<IEnumerable<CalendarEvent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string providerKey,
        [FromQuery] string calendarId,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int days = 30,
        [FromQuery] int max = 250,
        CancellationToken cancellationToken = default)
    {
        var available = await _calendarService.GetAvailableCalendarsAsync(cancellationToken);
        var calendar = available.FirstOrDefault(c =>
            c.ProviderKey == providerKey && c.CalendarId == calendarId);

        // Don't allow querying arbitrary calendars — only the configured allowlist.
        if (calendar is null)
        {
            return NotFound();
        }

        // An explicit from/to (e.g. month/year navigation) wins; otherwise a window from now.
        var start = from ?? DateTimeOffset.UtcNow;
        var end = to ?? start.AddDays(Math.Clamp(days, 1, 366));
        if (end <= start)
        {
            end = start.AddDays(1);
        }
        if ((end - start).TotalDays > 400)
        {
            end = start.AddDays(400);
        }

        var query = new CalendarQuery(start, end, Math.Clamp(max, 1, 500));
        var events = await _calendarService.GetEventsAsync(calendar, query, cancellationToken);

        return Ok(events);
    }
}
