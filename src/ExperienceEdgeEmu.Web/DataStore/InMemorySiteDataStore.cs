using ExperienceEdgeEmu.Web.EmuSchema;
using System.Collections.Concurrent;

namespace ExperienceEdgeEmu.Web.DataStore;

public sealed record SiteDataRecord(string SiteName, string Language, string SourceFilePath, SiteInfoLanguageDataRead Data);

public sealed class InMemorySiteDataStore
{
    private readonly ConcurrentDictionary<SiteKey, SiteDataRecord> _sites = new(new SiteKeyComparer());
    private readonly ConcurrentDictionary<string, HashSet<SiteKey>> _byFile = new();
    private SitecoreSiteData? _siteOverview;

    public void SetSiteData(SitecoreSiteData data) => _siteOverview = data;

    public SitecoreSiteData? GetSiteData()
    {
        if (_siteOverview == null)
        {
            return null;
        }

        foreach (var site in _siteOverview.AllSiteInfo.Results)
        {
            var allLangData = GetAllSiteInfo(site.Name);

            foreach (var langData in allLangData)
            {
                site.InternalLanguageData[langData.Language.ToLowerInvariant()] = langData.Data;
            }
        }

        return _siteOverview;
    }

    public void AddOrUpdate(SiteDataRecord record)
    {
        var key = new SiteKey(record.SiteName, record.Language);

        _sites[key] = record;

        _byFile.AddOrUpdate(record.SourceFilePath, _ => [key], (_, set) =>
        {
            set.Add(key);

            return set;
        });
    }

    private IEnumerable<SiteDataRecord> GetAllSiteInfo(string siteName) => _sites.Values.Where(x => x.SiteName.Equals(siteName, StringComparison.OrdinalIgnoreCase));

    public void RemoveByFile(string filePath)
    {
        if (!_byFile.TryRemove(filePath, out var keys))
        {
            return;
        }

        foreach (var key in keys)
        {
            _sites.TryRemove(key, out _);
        }
    }

    public int Count => _sites.Select(x => x.Value.SiteName).Distinct().Count();

    public record struct SiteKey(string SiteName, string Language);

    private sealed class SiteKeyComparer : IEqualityComparer<SiteKey>
    {
        public bool Equals(SiteKey x, SiteKey y) =>
            string.Equals(x.SiteName, y.SiteName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Language, y.Language, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(SiteKey obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SiteName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Language));
    }
}
