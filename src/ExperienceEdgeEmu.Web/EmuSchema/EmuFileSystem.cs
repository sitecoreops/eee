﻿using Microsoft.Extensions.Options;

namespace ExperienceEdgeEmu.Web.EmuSchema;

public enum DataCategory
{
    Unknown = 0,
    Item = 1,
    Site = 2
}

public class EmuFileSystem
{
    private readonly string _dataRootPath;

    public EmuFileSystem(IHostEnvironment env, IOptions<EmuSettings> options)
    {
        if (Path.IsPathFullyQualified(options.Value.DataRootPath))
        {
            _dataRootPath = options.Value.DataRootPath;
        }
        else
        {
            _dataRootPath = Path.Combine(env.ContentRootPath, options.Value.DataRootPath);
        }

        _dataRootPath = Path.GetFullPath(_dataRootPath);
    }

    public FileSystemWatcher CreateJsonFileWatcher()
    {
        EnsureDataRootPathExists();

        return new(_dataRootPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
            Filter = "*.json",
            IncludeSubdirectories = true
        };
    }

    private void EnsureDataRootPathExists()
    {
        if (!Directory.Exists(_dataRootPath))
        {
            Directory.CreateDirectory(_dataRootPath);
        }
    }

    public string[] GetSchemaFilePaths()
    {
        EnsureDataRootPathExists();

        return Directory.GetFiles(_dataRootPath, "*.graphqls");
    }

    public string[] GetJsonFilePaths()
    {
        EnsureDataRootPathExists();

        return Directory.GetFiles(_dataRootPath, "*.json", SearchOption.AllDirectories);
    }

    public string MakeAbsoluteDataPath(string relativePath) => Path.Combine(_dataRootPath, relativePath);

    public DataCategory GetDataCategory(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return DataCategory.Unknown;
        }

        // normalize and convert to '/'
        string Normalize(string p)
        {
            return Path.GetFullPath(p)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .TrimEnd('/');
        }

        var root = Normalize(_dataRootPath);
        var path = Normalize(filePath);

        // remove root if exists
        if (path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
        {
            path = path[(root.Length + 1)..];
        }

        // select category
        if (path.StartsWith("item/", StringComparison.OrdinalIgnoreCase))
        {
            return DataCategory.Item;
        }

        if (path.StartsWith("site/", StringComparison.OrdinalIgnoreCase))
        {
            return DataCategory.Site;
        }

        return DataCategory.Unknown;
    }

    public string GetMediaFilePath(Uri mediaUri) => GetMediaFilePath(mediaUri.AbsolutePath);

    public string GetMediaFilePath(string urlPath)
    {
        var relativeMediaPath = urlPath.Replace("/-/media/", "").Replace('/', Path.DirectorySeparatorChar);

        return Path.Combine(_dataRootPath, "media", relativeMediaPath);
    }
}
