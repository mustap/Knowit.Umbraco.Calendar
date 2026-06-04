using Knowit.Umbraco.Calendar.Core.Providers.Google;
using Knowit.Umbraco.Calendar.Core.Providers.Microsoft;

namespace Knowit.Umbraco.Calendar.Core.Configuration;

/// <summary>
/// Root configuration for the calendar package, bound from <c>Knowit:Calendar</c>.
/// </summary>
public sealed class CalendarOptions
{
    public const string SectionName = "Knowit:Calendar";

    /// <summary>How long fresh provider results are served from cache.</summary>
    public int DefaultCacheSeconds { get; set; } = 300;

    /// <summary>
    /// How long a stale copy is retained for fallback when a provider call fails.
    /// Should comfortably exceed <see cref="DefaultCacheSeconds"/>.
    /// </summary>
    public int StaleFallbackHours { get; set; } = 24;

    public GoogleOptions Google { get; set; } = new();

    public MicrosoftOptions Microsoft { get; set; } = new();
}
