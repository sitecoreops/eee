using Microsoft.Extensions.Caching.Memory;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

public partial class MediaUrlReplacer(IMemoryCache processedMediaUrls)
{
    [GeneratedRegex(@"(?:https?:\/\/[^\s""\/]+)?(\/-\/(?:media|jssmedia)\/[^\s""?\)]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PreviewMediaRegex();

    [GeneratedRegex(@"https?:\/\/edge\.sitecorecloud\.io\/[^\/]+\/(media\/[^\s\""\?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EdgeMediaRegex();

    public Dictionary<string, string> ReplaceMediaUrlsInFields(JsonNode? node, string baseUrl)
    {
        var changes = new Dictionary<string, string>(StringComparer.Ordinal);

        ReplaceMediaUrlsInFieldsInternal(node, baseUrl, changes);

        return changes;
    }

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
            foreach (var child in arr)
            {
                ReplaceMediaUrlsInFieldsInternal(child, baseUrl, changes);
            }
        }
    }

    private bool TryReplaceMediaUrl(string value, string baseUrl, Dictionary<string, string> changes, out string? replaced)
    {
        replaced = null;
        var wasReplaced = false;
        var valueKey = $"{nameof(MediaUrlReplacer)}:{value}";

        if (processedMediaUrls.TryGetValue(value, out replaced))
        {
            return true;
        }

        // skip if value does not look like media urls from edge or preview instances
        if (!value.Contains(".sitecorecloud.io/", StringComparison.OrdinalIgnoreCase) && !value.Contains("/-/media/", StringComparison.OrdinalIgnoreCase) && !value.Contains("/-/jssmedia/", StringComparison.OrdinalIgnoreCase))
        {
            return wasReplaced;
        }

        // skip if value contains .ashx which happens on urls that are not media urls
        if (value.Contains(".ashx", StringComparison.OrdinalIgnoreCase))
        {
            return wasReplaced;
        }

        var tempString = value;

        if (EdgeMediaRegex().Match(value).Success)
        {
            var edgePattern = $"{baseUrl.TrimEnd('/')}/-/$1";

            tempString = EdgeMediaRegex().Replace(tempString, edgePattern);
        }
        else if (PreviewMediaRegex().Match(value).Success)
        {
            var relativePattern = $"{baseUrl.TrimEnd('/')}$1";

            tempString = PreviewMediaRegex().Replace(tempString, relativePattern);
        }

        if (!string.Equals(value, tempString, StringComparison.Ordinal))
        {
            changes.TryAdd(value, tempString);

            replaced = tempString;
            wasReplaced = true;

            processedMediaUrls.GetOrCreate(valueKey, entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromHours(30));

                return true;
            });
        }

        return wasReplaced;
    }

    private void ReplaceStringsRecursivelyInObject(JsonObject obj, string baseUrl, Dictionary<string, string> changes)
    {
        foreach (var kvp in obj.ToList())
        {
            var key = kvp.Key;
            var value = kvp.Value;

            if (value is JsonValue val && val.TryGetValue(out string? s) && s is not null)
            {
                if (TryReplaceMediaUrl(s, baseUrl, changes, out var replaced))
                {
                    obj[key] = replaced;
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
                if (TryReplaceMediaUrl(s, baseUrl, changes, out var replaced))
                {
                    arr[i] = replaced;
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