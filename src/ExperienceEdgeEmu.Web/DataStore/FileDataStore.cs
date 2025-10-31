using ExperienceEdgeEmu.Web.EmuSchema;
using System.Text.Json;

namespace ExperienceEdgeEmu.Web.DataStore;

public class FileDataStore(EmuFileSystem emuFileSystem, ILogger<FileDataStore> logger, InMemoryItemStore itemStore, InMemorySiteDataStore siteDataStore)
{
    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Indexing data from data root...");

        var startupWatch = System.Diagnostics.Stopwatch.StartNew();

        // add test data
        SeedTestData();

        // add user data
        await Parallel.ForEachAsync(emuFileSystem.GetJsonFilePaths(), new ParallelOptions { CancellationToken = cancellationToken }, async (filePath, ct) =>
        {
            await ProcessJsonFile(filePath, cancellationToken);
        });

        logger.LogInformation("Finished indexing,  duration={Duration}, items={ItemCount}, sites={SitesCount},", startupWatch.Elapsed, itemStore.Count, siteDataStore.Count);
    }

    private void SeedTestData()
    {
        var testItem = new SitecoreItem(
                        "UnknownItem",
                        Guid.Empty.ToString("N"),
                        "minimal",
                        "Minimal Test Item",
                        1,
                        false,
                        "/sitecore/content/tests/minimal",
                        null,
                        new SitecoreTemplate(Guid.Empty.ToString("N"), []),
                        new SitecoreUrl("/minimal", "https://localhost/minimal", "localhost", "https", "test"),
                        new SitecoreLanguage("English", "English", "English", "en"),
                        [],
                        [],
                        null,
                        null,
                        new SitecoreChildren(new PageInfo(false, null), 0, []),
                        []
                    );

        itemStore.AddOrUpdate(new ItemRecord(testItem.Id, testItem.Path, null, testItem.Language.Name, "tests/minimal-item.json", testItem));
    }

    public async Task ProcessJsonFile(string filePath, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing {FilePath}.", filePath);

        try
        {
            var jsonDocument = JsonDocument.Parse(await ReadFileWithRetryAsync(filePath, cancellationToken));

            switch (emuFileSystem.GetDataCategory(filePath))
            {
                case DataCategory.Item:
                    {
                        if (TryGetItemNode(jsonDocument, out var itemNode))
                        {
                            var item = itemNode.Deserialize<SitecoreItem>(JsonSerializerOptions.Web);

                            if (item != null)
                            {
                                // if the item node contains rendered output, it can also be indexed as a layout route.
                                TryGetRoutePathFromItemNode(itemNode, out var routePath);

                                itemStore.AddOrUpdate(new ItemRecord(item.Id, item.Path, routePath, item.Language.Name, filePath, item));
                            }
                        }

                        break;
                    }

                case DataCategory.Site:
                    {
                        if (TryGetSiteDataNode(jsonDocument, out var siteNode))
                        {
                            var siteData = siteNode.Deserialize<SitecoreSiteData>(JsonSerializerOptions.Web);

                            if (siteData != null)
                            {
                                siteDataStore.SetSiteData(siteData);
                            }
                        }
                        else if (TryGetSiteInfoDataNode(jsonDocument, out var siteDataNode))
                        {
                            var siteInfo = siteDataNode.Deserialize<SiteInfoLanguageDataRead>(JsonSerializerOptions.Web);

                            if (siteInfo != null)
                            {
                                var language = Path.GetFileNameWithoutExtension(filePath);

                                siteDataStore.AddOrUpdate(new SiteDataRecord(siteInfo.Name, language, filePath, siteInfo));
                            }
                        }

                        break;
                    }

                default:
                    logger.LogError("Unexpected json file location detected: {FilePath}", filePath);

                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse or index file: {File}", filePath);
        }
    }

    private bool TryGetItemNode(JsonDocument jsonDocument, out JsonElement itemNode)
    {
        itemNode = default;

        if (!jsonDocument.RootElement.TryGetProperty("data", out var dataNode))
        {
            return false;
        }

        if (dataNode.TryGetProperty("layout", out var layoutNode) && layoutNode.TryGetProperty("item", out itemNode) && itemNode.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        if (dataNode.TryGetProperty("item", out itemNode) && itemNode.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        return false;
    }

    private bool TryGetSiteDataNode(JsonDocument jsonDocument, out JsonElement siteNode)
    {
        siteNode = default;

        if (!jsonDocument.RootElement.TryGetProperty("data", out var dataNode))
        {
            return false;
        }

        if (dataNode.TryGetProperty("site", out siteNode) && siteNode.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        return false;
    }

    private bool TryGetSiteInfoDataNode(JsonDocument jsonDocument, out JsonElement dataNode)
    {
        if (jsonDocument.RootElement.TryGetProperty("siteInfoData", out dataNode) && dataNode.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        return false;
    }

    private bool TryGetRoutePathFromItemNode(JsonElement itemNode, out string routePath)
    {
        routePath = string.Empty;

        if (itemNode.TryGetProperty("rendered", out var renderedNode) && renderedNode.ValueKind == JsonValueKind.Object &&
            renderedNode.TryGetProperty("sitecore", out var sitecoreNode) && sitecoreNode.ValueKind == JsonValueKind.Object &&
            sitecoreNode.TryGetProperty("context", out var contextNode) && contextNode.ValueKind == JsonValueKind.Object &&
            contextNode.TryGetProperty("itemPath", out var itemPathNode) && itemPathNode.ValueKind == JsonValueKind.String)
        {
            routePath = itemPathNode.GetString()!;

            return !string.IsNullOrEmpty(routePath);
        }

        return false;
    }

    public SitecoreSiteData? GetSites()
    {
        var sites = siteDataStore.GetSiteData();

        foreach (var site in sites?.AllSiteInfo.Results ?? [])
        {
            foreach (var language in site.InternalLanguageData.Keys)
            {
                var siteData = site.InternalLanguageData[language];

                if (siteData == null || siteData.Routes == null)
                {
                    logger.LogError("No route data found for site {Site} and language {Language}.", site.Name, language);

                    continue;
                }

                var validRouteResults = new List<RouteResult>();

                foreach (var route in siteData.Routes.Results)
                {
                    var routeItem = GetItemById(route.Route.Id, language);

                    if (routeItem != null)
                    {
                        validRouteResults.Add(new RouteResult { Route = routeItem, RoutePath = route.RoutePath });
                    }
                    else
                    {
                        logger.LogWarning("Item not found for route {RoutePath} and id {ItemId}, site {Site} and language {Language}.", route.RoutePath, route.Route.Id, site.Name, language);
                    }
                }

                siteData.Routes.Results = [.. validRouteResults];
            }
        }

        return sites;
    }

    public SitecoreLayout? GetLayoutByRoute(string site, string language, string routePath)
    {
        if (routePath != "/")
        {
            routePath = routePath.TrimEnd('/');
        }

        return new(itemStore.GetByRoute(site, routePath, language)?.Data);
    }

    public SitecoreItem? GetItemByPath(string path, string language)
    {
        if (Guid.TryParse(path, out _))
        {
            return GetItemById(path, language);
        }

        if (path != "/")
        {
            path = path.TrimEnd('/');
        }

        return itemStore.GetByPath(path, language)?.Data;
    }

    public SitecoreItem? GetItemById(string id, string language) => itemStore.GetById(id, language)?.Data;

    private async Task<string> ReadFileWithRetryAsync(string filePath, CancellationToken token, int retries = 3, int delayMs = 100)
    {
        for (var i = 0; i < retries; i++)
        {
            try
            {
                // open the file with FileShare.Read to allow reading even if another process has it open for reading.
                await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new StreamReader(stream);

                return await reader.ReadToEndAsync(token);
            }
            catch (IOException) when (i < retries - 1)
            {
                await Task.Delay(delayMs, token);

                delayMs *= 2; // exponential backoff
            }
        }

        throw new IOException($"Could not read {filePath} after {retries} attempts.");
    }

    public void RemoveItem(string filePath)
    {
        switch (emuFileSystem.GetDataCategory(filePath))
        {
            case DataCategory.Item:
                {
                    itemStore.RemoveByFile(filePath);

                    logger.LogInformation("Removed all item data for {FilePath}.", filePath);

                    break;
                }

            case DataCategory.Site:
                {
                    siteDataStore.RemoveByFile(filePath);

                    logger.LogInformation("Removed all site data for {FilePath}.", filePath);

                    break;
                }
        }
    }
}
