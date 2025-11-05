using ExperienceEdgeEmu.Web.DataStore.Crawler;
using Microsoft.Extensions.Options;

namespace ExperienceEdgeEmu.Web.Tests;

public class MediaUrlRewriterShould
{
    private readonly IOptions<EmuSettings> _options;

    public MediaUrlRewriterShould() => _options = Options.Create(new EmuSettings { MediaHost = "https://localhost:9999/" });

    [Theory]
    [InlineData(
        "https://edge.sitecorecloud.io/tenant/media/Feature/JSS-Experience-Accelerator/Basic-Site/banner-image.jpg?h=2001&iar=0&w=3000",
        "https://localhost:9999/-/media/Feature/JSS-Experience-Accelerator/Basic-Site/banner-image.jpg?h=2001&iar=0&w=3000")]
    [InlineData(
        "https://edge.sitecorecloud.io/tenant/media/Project/demosites/skateparkdemosite/Sitemaps/skateparkdemosite/sitemap.xml",
        "https://localhost:9999/-/media/Project/demosites/skateparkdemosite/Sitemaps/skateparkdemosite/sitemap.xml")]
    [InlineData(
        "https://xmctenant.sitecorecloud.io/-/jssmedia/Project/DemoSites/testsite/mobile.jpg?h=300&iar=0&w=400&ttc=x&tt=y&hash=z",
        "https://localhost:9999/-/media/Project/DemoSites/testsite/mobile.jpg?h=300&iar=0&w=400&ttc=x&tt=y&hash=z")]
    public void RewriteValuesContainingMediaUrl(string inputUrl, string expectedUrl)
    {
        //// arrange
        var sut = new MediaUrlRewriter(_options);

        //// act
        var actualUrl = sut.Rewrite(inputUrl);

        //// assert
        Assert.Equal(expectedUrl, actualUrl);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("https://example.com/hello.jpg", "https://example.com/hello.jpg")]
    [InlineData("/hello.jpg", "/hello.jpg")]
    [InlineData("https://edge.sitecorecloud.io/media/Feature/JSS-Experience-Accelerator/Basic-Site/banner-image.jpg?h=2001&iar=0&w=3000",
        "https://edge.sitecorecloud.io/media/Feature/JSS-Experience-Accelerator/Basic-Site/banner-image.jpg?h=2001&iar=0&w=3000")]
    public void NotRewriteValuesContainingNonNotSitecoreMediaUrl(string inputUrl, string expectedUrl)
    {
        //// arrange
        var sut = new MediaUrlRewriter(_options);

        //// act
        var actualUrl = sut.Rewrite(inputUrl);

        //// assert
        Assert.Equal(expectedUrl, actualUrl);
    }
}
