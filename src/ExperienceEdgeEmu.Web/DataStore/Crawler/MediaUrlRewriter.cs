using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

public partial class MediaUrlRewriter(IOptions<EmuSettings> options)
{
    private readonly string _localPrefix = options.Value.MediaHost.TrimEnd('/') + "/-/media/";

    public string Rewrite(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        var trimmed = input.TrimStart();

        // if input looks like absolute or relative urls then use simple approach
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith('/'))
        {
            return RewriteSimple(input);
        }

        // else try markup approach
        return RewriteMarkup(input);
    }

    private string RewriteSimple(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        return SimpleUrlReplacerRegex().Replace(input, m => RewriteSingleUrl(m.Value));
    }

    private string RewriteMarkup(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        return MarkupUrlReplacerRegex().Replace(input, m =>
        {
            var url = m.Groups["url"].Value;
            var rewritten = RewriteSingleUrl(url);
            var quote = m.Value.StartsWith("\"") ? "\"" : "'";

            return quote + rewritten + quote;
        });
    }

    private string RewriteSingleUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        // skip speciel protocols
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // skip if url contains .ashx which happens on urls that are not media urls
        if (url.EndsWith(".ashx", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var uri = Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var parsed) ? parsed : null;
        var pathAndQuery = uri?.IsAbsoluteUri == true ? uri.PathAndQuery : url;
        var match = IsMediaPathRegex().Match(pathAndQuery);

        if (!match.Success)
        {
            return url;
        }

        var rest = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

        return _localPrefix + rest;
    }

    [GeneratedRegex(
        @"(?ix)
        \b
        (?:https?://[^/\s""']+/
          (?:
            -/(?:media|jssmedia)
            |
            [^/\s""']+/media # 2. segment only
          )
          (?<rest>[^?\s""'>]+)
          (?<qry>\?[^\s""'>]*)?
        )", RegexOptions.Compiled)]
    private static partial Regex SimpleUrlReplacerRegex();

    [GeneratedRegex(
        @"(?ix)
        (?<=\b(?:src|href)\s*=\s*)
        (?:
          ""(?<url>[^""]+)""
          |
          '(?<url>[^']+)'
        )", RegexOptions.Compiled)]
    private static partial Regex MarkupUrlReplacerRegex();

    [GeneratedRegex(
        @"(?ix)
        (?:.*/-/(?:jssmedia|media)/(.*))
        |
        ^/[^/]+/media/(.*)
        ", RegexOptions.Compiled)]
    private static partial Regex IsMediaPathRegex();
}