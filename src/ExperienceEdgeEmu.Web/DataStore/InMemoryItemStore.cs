using ExperienceEdgeEmu.Web.EmuSchema;
using System.Collections.Concurrent;

namespace ExperienceEdgeEmu.Web.DataStore;

public sealed record ItemRecord(string Id, string Path, string? Route, string Language, string SourceFilePath, SitecoreItem Data);

public sealed class InMemoryItemStore
{
    private readonly ConcurrentDictionary<ItemKey, ItemRecord> _items = new(new ItemKeyComparer());
    private readonly ConcurrentDictionary<PathKey, string> _byPath = new(new PathKeyComparer());
    private readonly ConcurrentDictionary<RouteKey, string> _byRoute = new(new RouteKeyComparer());
    private readonly ConcurrentDictionary<string, HashSet<(string Id, string Lang)>> _byFile = new();

    public void AddOrUpdate(ItemRecord record)
    {
        _items[new ItemKey(record.Id, record.Language)] = record;
        _byPath[new PathKey(record.Path, record.Language)] = record.Id;

        if (!string.IsNullOrEmpty(record.Route))
        {
            _byRoute[new RouteKey(record.Data.Url.SiteName, record.Route, record.Language)] = record.Id;
        }

        _byFile.AddOrUpdate(record.SourceFilePath, _ => [(record.Id, record.Language)], (_, ids) =>
        {
            ids.Add((record.Id, record.Language));

            return ids;
        });
    }

    public ItemRecord? GetById(string id, string language)
    {
        if (_items.TryGetValue(new ItemKey(id, language), out var item))
        {
            foreach (var field in item.Data.Fields)
            {
                switch (field)
                {
                    case SitecoreLookupField lookupField:
                        {
                            if (lookupField.TargetId() != null && Guid.TryParse(lookupField.TargetId(), out var lookupTargetId))
                            {
                                lookupField.TargetItem = GetById(lookupTargetId.ToString("N").ToUpperInvariant(), language)?.Data;
                            }

                            break;
                        }

                    case SitecoreLinkField linkField:
                        {
                            if (linkField.TargetId() != null && Guid.TryParse(linkField.TargetId(), out var linkTargetId))
                            {
                                linkField.TargetItem = GetById(linkTargetId.ToString("N").ToUpperInvariant(), language)?.Data;
                            }

                            break;
                        }
                    case SitecoreMultilistField multilistField:
                        {
                            var targetItems = new List<SitecoreItem>();

                            foreach (var targetIdString in multilistField.TargetIds() ?? [])
                            {
                                if (Guid.TryParse(targetIdString, out var targetId))
                                {
                                    var target = GetById(targetId.ToString("N").ToUpperInvariant(), language)?.Data;

                                    if (target != null)
                                    {
                                        targetItems.Add(target);
                                    }
                                }
                            }

                            multilistField.TargetItems = [.. targetItems];

                            break;
                        }
                }
            }

            return item;
        }
        else
        {
            return null;
        }
    }

    public ItemRecord? GetByPath(string path, string language) => _byPath.TryGetValue(new PathKey(path, language), out var id) ? GetById(id, language) : null;

    public ItemRecord? GetByRoute(string siteName, string route, string language) => _byRoute.TryGetValue(new RouteKey(siteName, route, language), out var id) ? GetById(id, language) : null;

    public void RemoveByFile(string filePath)
    {
        if (!_byFile.TryRemove(filePath, out var ids))
        {
            return;
        }

        foreach (var (Id, Lang) in ids)
        {
            if (!_items.TryRemove(new ItemKey(Id, Lang), out var rec))
            {
                continue;
            }

            _byPath.TryRemove(new PathKey(rec.Path, rec.Language), out _);

            if (rec.Route != null)
            {
                _byRoute.TryRemove(new RouteKey(rec.Data.Url.SiteName, rec.Route, rec.Language), out _);
            }
        }
    }

    public IEnumerable<ItemRecord> All => _items.Values;

    public int Count => _items.Count;

    private sealed class ItemKeyComparer : IEqualityComparer<ItemKey>
    {
        public bool Equals(ItemKey x, ItemKey y) =>
            string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Language, y.Language, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ItemKey obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Language));
    }

    private sealed class PathKeyComparer : IEqualityComparer<PathKey>
    {
        public bool Equals(PathKey x, PathKey y) =>
            string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Language, y.Language, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(PathKey obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Language));
    }

    private sealed class RouteKeyComparer : IEqualityComparer<RouteKey>
    {
        public bool Equals(RouteKey x, RouteKey y) =>
            string.Equals(x.SiteName, y.SiteName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Language, y.Language, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Route, y.Route, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(RouteKey obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SiteName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Language),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Route));
    }

    public record struct ItemKey(string Id, string Language);
    public record struct PathKey(string Path, string Language);
    public record struct RouteKey(string SiteName, string Language, string Route);
}
