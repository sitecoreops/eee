using ExperienceEdgeEmu.Web.EmuSchema;
using System.Collections.Concurrent;

namespace ExperienceEdgeEmu.Web.DataStore;

public class JsonFileWatcherWorker(ILogger<JsonFileWatcherWorker> logger, EmuFileSystem emuFileSystem, JsonFileChangeQueue queue) : BackgroundService
{
    private readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(50);
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(10);
    private FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, FileChangeInfo> _eventTimes = new();
    private Timer? _debounceTimer;
    private Timer? _healthTimer;
    private DateTime _lastEventTime = DateTime.MinValue;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StartWatcher();

        _debounceTimer = new Timer(OnDebounceTimer, null, 0, 100);
        _healthTimer = new Timer(OnHealthCheck, null, 0, (int)_healthCheckInterval.TotalMilliseconds);

        stoppingToken.Register(() =>
        {
            logger.LogInformation("File watcher service stopping...");

            _watcher?.Dispose();
            _debounceTimer?.Dispose();
            _healthTimer?.Dispose();
        });

        return Task.CompletedTask;
    }

    private void StartWatcher()
    {
        _watcher?.Dispose();
        _watcher = emuFileSystem.CreateJsonFileWatcher();
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;
        _watcher.EnableRaisingEvents = true;
        _watcher.InternalBufferSize *= 2;

        logger.LogInformation("File watcher started on {Path}.", _watcher.Path);
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => RegisterEvent(e.FullPath, e.ChangeType);

    private void OnRenamed(object sender, RenamedEventArgs e) => RegisterEvent(e.FullPath, WatcherChangeTypes.Renamed, e.OldFullPath);

    private void RegisterEvent(string path, WatcherChangeTypes changeType, string? oldPath = null)
    {
        if (Path.GetExtension(path) != ".json")
        {
            // this is needed because sometimes other file types can trigger events

            return;
        }

        _lastEventTime = DateTime.UtcNow;
        _eventTimes.AddOrUpdate(path,
            _ => new FileChangeInfo
            {
                Path = path,
                ChangeType = changeType,
                OldPath = oldPath,
                LastEventTime = _lastEventTime
            },
            (_, existing) =>
            {
                existing.ChangeType = changeType;
                existing.OldPath = oldPath;
                existing.LastEventTime = _lastEventTime;

                return existing;
            });
    }

    private void OnDebounceTimer(object? state)
    {
        var now = DateTime.UtcNow;

        foreach (var kvp in _eventTimes)
        {
            var info = kvp.Value;

            if (now - info.LastEventTime <= _debounceTime)
            {
                continue;
            }

            if (_eventTimes.TryRemove(kvp.Key, out _))
            {
                if (info.ChangeType == WatcherChangeTypes.Renamed && info.OldPath != null)
                {
                    logger.LogInformation("File renamed: {Old} -> {New}.", info.OldPath, info.Path);

                    queue.QueueMessage(info.OldPath, WatcherChangeTypes.Deleted);
                    queue.QueueMessage(info.Path, WatcherChangeTypes.Created);
                }
                else
                {
                    logger.LogInformation("File {ChangeType}: {Path}.", info.ChangeType, info.Path);

                    queue.QueueMessage(info.Path, info.ChangeType);
                }
            }
        }
    }

    private void OnHealthCheck(object? state)
    {

        try
        {
            if (_watcher is null || !_watcher.EnableRaisingEvents)
            {
                logger.LogWarning("File watcher inactive, restarting...");

                StartWatcher();
            }
            else if (_lastEventTime > DateTime.MinValue && (DateTime.UtcNow - _lastEventTime).TotalMinutes > 10)
            {
                logger.LogInformation("No events in 10 minutes, restarting file watcher...");

                StartWatcher();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during file watcher health check.");
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        logger.LogError(e.GetException(), "File watcher error, restarting...");

        StartWatcher();
    }

    private class FileChangeInfo
    {
        public required string Path { get; set; }
        public WatcherChangeTypes ChangeType { get; set; }
        public string? OldPath { get; set; }
        public DateTime LastEventTime { get; set; }
    }
}
