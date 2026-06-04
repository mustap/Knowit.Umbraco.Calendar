using Knowit.Umbraco.Calendar.Core.Text;

namespace Knowit.Umbraco.Calendar.Core.Tests;

public class HtmlSanitizationTests
{
    [Fact]
    public void SanitizeHtml_strips_scripts_and_event_handlers()
    {
        var dirty = "<p>Hi</p><script>alert('x')</script><a href=\"https://x\" onclick=\"evil()\">link</a>";

        var clean = HtmlSanitization.SanitizeHtml(dirty);

        Assert.NotNull(clean);
        Assert.DoesNotContain("<script", clean);
        Assert.DoesNotContain("onclick", clean);
        Assert.Contains("<p>Hi</p>", clean);
        Assert.Contains("href=\"https://x\"", clean);
    }

    [Fact]
    public void SanitizeHtml_passes_through_null_and_empty()
    {
        Assert.Null(HtmlSanitization.SanitizeHtml(null));
        Assert.Equal(string.Empty, HtmlSanitization.SanitizeHtml(string.Empty));
    }

    [Fact]
    public void EncodeText_encodes_html_special_characters()
    {
        Assert.Equal("R&amp;D &lt;team&gt;", HtmlSanitization.EncodeText("R&D <team>"));
    }
}
