using System.Text;
using System.Text.Encodings.Web;
using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Models;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Knowit.Umbraco.Calendar.Rendering;

/// <summary>
/// Renders a calendar inline from markup:
/// <c>&lt;knowit-calendar calendar="@Model.Value&lt;CalendarRef&gt;("eventCalendar")" days="30" max="20" /&gt;</c>.
/// Emits a semantic, self-contained list. For view-based/overridable rendering use the
/// <see cref="KnowitCalendarViewComponent"/> instead. Requires
/// <c>@addTagHelper *, Knowit.Umbraco.Calendar</c> in the consuming site's _ViewImports.
/// </summary>
[HtmlTargetElement("knowit-calendar")]
public sealed class CalendarTagHelper : TagHelper
{
    private readonly ICalendarService _calendarService;

    public CalendarTagHelper(ICalendarService calendarService)
        => _calendarService = calendarService;

    [HtmlAttributeName("calendar")]
    public CalendarRef? Calendar { get; set; }

    [HtmlAttributeName("days")]
    public int Days { get; set; } = 30;

    [HtmlAttributeName("max")]
    public int Max { get; set; } = 20;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "knowit-calendar");

        if (Calendar is null)
        {
            output.Content.SetHtmlContent(string.Empty);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var query = new CalendarQuery(now, now.AddDays(Math.Clamp(Days, 1, 366)), Math.Clamp(Max, 1, 100));
        var events = await _calendarService.GetEventsAsync(Calendar, query);

        output.Content.SetHtmlContent(BuildHtml(events));
    }

    private static string BuildHtml(IReadOnlyList<CalendarEvent> events)
    {
        var html = HtmlEncoder.Default;
        var sb = new StringBuilder();
        sb.Append("<ul class=\"knowit-calendar__list\">");

        foreach (var e in events)
        {
            sb.Append("<li class=\"knowit-calendar__item\" itemscope itemtype=\"https://schema.org/Event\">");
            sb.Append("<span class=\"knowit-calendar__title\" itemprop=\"name\">")
              .Append(html.Encode(e.Title))
              .Append("</span>");
            sb.Append("<time class=\"knowit-calendar__start\" itemprop=\"startDate\" datetime=\"")
              .Append(html.Encode(e.Start.ToString("o")))
              .Append("\">")
              .Append(html.Encode(e.Start.ToString("f")))
              .Append("</time>");
            sb.Append("<meta itemprop=\"endDate\" content=\"")
              .Append(html.Encode(e.End.ToString("o")))
              .Append("\" />");

            if (!string.IsNullOrEmpty(e.Location))
            {
                sb.Append("<span class=\"knowit-calendar__location\" itemprop=\"location\">")
                  .Append(html.Encode(e.Location))
                  .Append("</span>");
            }

            if (!string.IsNullOrEmpty(e.Description))
            {
                // Description is already sanitized HTML (see provider mappers) — emit as-is.
                sb.Append("<div class=\"knowit-calendar__description\" itemprop=\"description\">")
                  .Append(e.Description)
                  .Append("</div>");
            }

            if (e.OnlineMeeting is { } meeting)
            {
                sb.Append("<a class=\"knowit-calendar__join\" rel=\"noopener noreferrer\" target=\"_blank\" href=\"")
                  .Append(html.Encode(meeting.JoinUrl))
                  .Append("\">")
                  .Append(html.Encode(meeting.ProviderDisplayName ?? "Join online"))
                  .Append("</a>");
            }

            sb.Append("</li>");
        }

        sb.Append("</ul>");
        return sb.ToString();
    }
}
