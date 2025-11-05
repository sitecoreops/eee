using Microsoft.Extensions.Caching.Memory;
using System.Text.Json.Nodes;

namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

public partial class JsonMediaUrlReplacer(IMemoryCache processedMediaUrls, MediaUrlRewriter urlRewriter)
{
    public Dictionary<string, string> ReplaceMediaUrlsInFields(JsonNode? node)
    {
        var changes = new Dictionary<string, string>(StringComparer.Ordinal);

        ReplaceMediaUrlsInFieldsInternal(node, changes);

        return changes;
    }

    private void ReplaceMediaUrlsInFieldsInternal(JsonNode? node, Dictionary<string, string> changes)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj.ToList())
            {
                if (kvp.Key.Equals("fields", StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value is JsonObject fieldsObj)
                    {
                        ReplaceStringsRecursivelyInObject(fieldsObj, changes);
                    }
                    else if (kvp.Value is JsonArray fieldsArr)
                    {
                        ReplaceStringsRecursivelyInArray(fieldsArr, changes);
                    }
                }
                else
                {
                    if (kvp.Value is JsonNode child)
                    {
                        ReplaceMediaUrlsInFieldsInternal(child, changes);
                    }
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr)
            {
                ReplaceMediaUrlsInFieldsInternal(child, changes);
            }
        }
    }

    private bool TryReplaceMediaUrl(string value, Dictionary<string, string> changes, out string? replaced)
    {
        replaced = null;
        var wasReplaced = false;
        var valueKey = $"{nameof(JsonMediaUrlReplacer)}:{value}";

        if (processedMediaUrls.TryGetValue(value, out replaced))
        {
            return true;
        }

        var tempString = urlRewriter.Rewrite(value);

        if (!string.Equals(value, tempString, StringComparison.Ordinal))
        {
            changes.TryAdd(value, tempString);

            replaced = tempString;
            wasReplaced = true;

            processedMediaUrls.GetOrCreate(valueKey, entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(30));

                return true;
            });
        }

        return wasReplaced;
    }

    private void ReplaceStringsRecursivelyInObject(JsonObject obj, Dictionary<string, string> changes)
    {
        foreach (var kvp in obj.ToList())
        {
            var key = kvp.Key;
            var value = kvp.Value;

            if (value is JsonValue val && val.TryGetValue(out string? s) && s is not null)
            {
                if (TryReplaceMediaUrl(s, changes, out var replaced))
                {
                    obj[key] = replaced;
                }
            }
            else if (value is JsonObject childObj)
            {
                ReplaceStringsRecursivelyInObject(childObj, changes);
            }
            else if (value is JsonArray childArr)
            {
                ReplaceStringsRecursivelyInArray(childArr, changes);
            }
        }
    }

    private void ReplaceStringsRecursivelyInArray(JsonArray arr, Dictionary<string, string> changes)
    {
        for (var i = 0; i < arr.Count; i++)
        {
            var v = arr[i];

            if (v is JsonValue val && val.TryGetValue(out string? s) && s is not null)
            {
                if (TryReplaceMediaUrl(s, changes, out var replaced))
                {
                    arr[i] = replaced;
                }
            }
            else if (v is JsonObject childObj)
            {
                ReplaceStringsRecursivelyInObject(childObj, changes);
            }
            else if (v is JsonArray childArr)
            {
                ReplaceStringsRecursivelyInArray(childArr, changes);
            }
        }
    }
}