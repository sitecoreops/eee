using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

public partial class MediaUrlReplacer
{
    [GeneratedRegex(@"(?:https?:\/\/[^\s\""]+)?(\/-\/media\/[^\s\""]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MediaRegex();

    public Dictionary<string, string> ReplaceMediaUrlsInFields(JsonNode? node, string baseUrl)
    {
        var changes = new Dictionary<string, string>(StringComparer.Ordinal);

        ReplaceMediaUrlsInFieldsInternal(node, baseUrl, changes);

        return changes;
    }

    private bool CouldBeMediaUrl(string s) => s.StartsWith("https://edge.sitecorecloud.io/", StringComparison.OrdinalIgnoreCase) || s.Contains("/-/media/", StringComparison.OrdinalIgnoreCase);

    private void ReplaceMediaUrlsInFieldsInternal(JsonNode? node, string baseUrl, Dictionary<string, string> changes)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj.ToList())
            {
                if (kvp.Key.Equals("fields", StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value is JsonObject fieldsObj)
                    {
                        ReplaceStringsRecursivelyInObject(fieldsObj, baseUrl, changes);
                    }
                    else if (kvp.Value is JsonArray fieldsArr)
                    {
                        ReplaceStringsRecursivelyInArray(fieldsArr, baseUrl, changes);
                    }
                }
                else
                {
                    if (kvp.Value is JsonNode child)
                    {
                        ReplaceMediaUrlsInFieldsInternal(child, baseUrl, changes);
                    }
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                var child = arr[i];

                ReplaceMediaUrlsInFieldsInternal(child, baseUrl, changes);
            }
        }
    }

    private void ReplaceStringsRecursivelyInObject(JsonObject obj, string baseUrl, Dictionary<string, string> changes)
    {
        foreach (var kvp in obj.ToList())
        {
            var key = kvp.Key;
            var value = kvp.Value;

            if (value is JsonValue val && val.TryGetValue(out string? s) && s is not null)
            {
                if (CouldBeMediaUrl(s))
                {
                    foreach (Match m in MediaRegex().Matches(s))
                    {
                        var originalUrl = m.Value;
                        var newUrl = MediaRegex().Replace(originalUrl, $"{baseUrl.TrimEnd('/')}$1");

                        if (!string.Equals(originalUrl, newUrl, StringComparison.Ordinal))
                        {
                            changes.TryAdd(originalUrl, newUrl);
                        }
                    }

                    var replaced = MediaRegex().Replace(s, $"{baseUrl.TrimEnd('/')}$1");

                    if (!string.Equals(replaced, s, StringComparison.Ordinal))
                    {
                        obj[key] = replaced;
                    }
                }
            }
            else if (value is JsonObject childObj)
            {
                ReplaceStringsRecursivelyInObject(childObj, baseUrl, changes);
            }
            else if (value is JsonArray childArr)
            {
                ReplaceStringsRecursivelyInArray(childArr, baseUrl, changes);
            }
        }
    }

    private void ReplaceStringsRecursivelyInArray(JsonArray arr, string baseUrl, Dictionary<string, string> changes)
    {
        for (var i = 0; i < arr.Count; i++)
        {
            var v = arr[i];

            if (v is JsonValue val && val.TryGetValue(out string? s) && s is not null)
            {
                if (CouldBeMediaUrl(s))
                {
                    foreach (Match m in MediaRegex().Matches(s))
                    {
                        var originalUrl = m.Value;
                        var newUrl = MediaRegex().Replace(originalUrl, $"{baseUrl.TrimEnd('/')}$1");

                        if (!string.Equals(originalUrl, newUrl, StringComparison.Ordinal))
                        {
                            changes.TryAdd(originalUrl, newUrl);
                        }
                    }

                    var replaced = MediaRegex().Replace(s, $"{baseUrl.TrimEnd('/')}$1");

                    if (!string.Equals(replaced, s, StringComparison.Ordinal))
                    {
                        arr[i] = replaced;
                    }
                }
            }
            else if (v is JsonObject childObj)
            {
                ReplaceStringsRecursivelyInObject(childObj, baseUrl, changes);
            }
            else if (v is JsonArray childArr)
            {
                ReplaceStringsRecursivelyInArray(childArr, baseUrl, changes);
            }
        }
    }
}