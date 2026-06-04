namespace Knowit.Umbraco.Calendar.Core.Models;

/// <summary>
/// A reference to a single calendar, normalized across providers. This is what an editor
/// effectively picks (the <see cref="CalendarId"/> is opaque to them) and what the rendering
/// layer resolves events from.
/// </summary>
/// <param name="ProviderKey">Provider discriminator, e.g. "google" or "microsoft".</param>
/// <param name="CalendarId">Provider-specific calendar identifier.</param>
/// <param name="DisplayName">Human-friendly name shown in the backoffice picker.</param>
public sealed record CalendarRef(
    string ProviderKey,
    string CalendarId,
    string DisplayName);
