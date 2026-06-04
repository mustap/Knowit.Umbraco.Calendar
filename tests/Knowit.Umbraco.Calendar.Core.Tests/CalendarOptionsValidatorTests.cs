using Knowit.Umbraco.Calendar.Core.Configuration;

namespace Knowit.Umbraco.Calendar.Core.Tests;

public class CalendarOptionsValidatorTests
{
    private readonly CalendarOptionsValidator _validator = new();

    [Fact]
    public void Succeeds_when_both_providers_disabled()
    {
        var result = _validator.Validate(null, new CalendarOptions());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Fails_when_google_enabled_without_credentials()
    {
        var options = new CalendarOptions();
        options.Google.Enabled = true;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("ServiceAccountJson"));
    }

    [Fact]
    public void Fails_when_microsoft_enabled_missing_secret()
    {
        var options = new CalendarOptions();
        options.Microsoft.Enabled = true;
        options.Microsoft.TenantId = "tenant";
        options.Microsoft.ClientId = "client";
        // ClientSecret intentionally missing

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("ClientSecret"));
    }

    [Fact]
    public void Fails_on_negative_cache_seconds()
    {
        var options = new CalendarOptions { DefaultCacheSeconds = -1 };
        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Succeeds_when_microsoft_fully_configured()
    {
        var options = new CalendarOptions();
        options.Microsoft.Enabled = true;
        options.Microsoft.TenantId = "tenant";
        options.Microsoft.ClientId = "client";
        options.Microsoft.ClientSecret = "secret";

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}
