using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Knowit.Umbraco.Calendar.Controllers;

/// <summary>
/// Backoffice management API for the calendar picker: lists the admin-allowed calendars and
/// previews upcoming events. Consumed by the property editor UI (Phase 4).
/// Base route: <c>/umbraco/management/api/v1/knowit-calendar</c>.
/// </summary>
[VersionedApiBackOfficeRoute("knowit-calendar")]
[ApiExplorerSettings(GroupName = "Knowit Calendar")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class CalendarManagementController : ManagementApiControllerBase
{
    private readonly ICalendarService _calendarService;

    public CalendarManagementController(ICalendarService calendarService)
        => _calendarService = calendarService;

    /// <summary>Lists every calendar an editor is allowed to pick, across enabled providers.</summary>
    [HttpGet("calendars")]
    [ProducesResponseType<IEnumerable<CalendarRef>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendars(CancellationToken cancellationToken)
        => Ok(await _calendarService.GetAvailableCalendarsAsync(cancellationToken));

    /// <summary>Previews upcoming events for a calendar, used for the live preview in the editor.</summary>
    [HttpGet("preview")]
    [ProducesResponseType<IEnumerable<CalendarEvent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreview(
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

        if (calendar is null)
        {
            return NotFound();
        }

        // An explicit from/to (used for month navigation, incl. past dates) takes precedence;
        // otherwise default to a window starting now.
        var start = from ?? DateTimeOffset.UtcNow;
        var end = to ?? start.AddDays(Math.Clamp(days, 1, 366));
        if (end <= start)
        {
            end = start.AddDays(1);
        }
        if ((end - start).TotalDays > 400)
        {
            end = start.AddDays(400); // guard against oversized ranges (allows a full year view)
        }

        var query = new CalendarQuery(start, end, Math.Clamp(max, 1, 500));
        var events = await _calendarService.GetEventsAsync(calendar, query, cancellationToken);

        return Ok(events);
    }
}
