using Knowit.Umbraco.Calendar.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Knowit.Umbraco.Calendar.Composing;

/// <summary>
/// Wires the calendar package into the Umbraco DI container on startup. No changes to the
/// consuming site's Program.cs are required. The property editor schema, value converter,
/// and view component are auto-discovered; the management API controller lives in this RCL,
/// so its assembly is registered as an MVC application part explicitly.
/// </summary>
public sealed class CalendarComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddKnowitCalendar(builder.Config);

        // Controllers in a class library are not discovered automatically — register this
        // assembly as an application part so the backoffice management API endpoints route.
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(CalendarComposer).Assembly);
    }
}
