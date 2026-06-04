namespace Knowit.Umbraco.Calendar.Core.Providers.Google;

/// <summary>
/// Developer/admin-supplied configuration for the Google provider. Bound from
/// <c>Knowit:Calendar:Google</c>. The service-account JSON must come from a secret store.
/// </summary>
public sealed class GoogleOptions
{
    public bool Enabled { get; set; }

    /// <summary>Raw service-account JSON. Inject from user-secrets / Key Vault, never appsettings.</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>The calendars editors are allowed to pick. Each must be shared with the service account.</summary>
    public IList<GoogleAllowedCalendar> AllowedCalendars { get; set; } = [];
}

/// <summary>A single Google calendar exposed to editors.</summary>
public sealed class GoogleAllowedCalendar
{
    /// <summary>Calendar id (often the shared mailbox/group address, or "primary").</summary>
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
