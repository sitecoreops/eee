namespace ExperienceEdgeEmu.Web.DataStore;

public class JsonFileChangeWorker(ILogger<JsonFileChangeWorker> logger, JsonFileChangeQueue queue, FileDataStore fileDataStore) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (filePath, changeType) in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                switch (changeType)
                {
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Changed:
                        await fileDataStore.ProcessJsonFile(filePath, stoppingToken);

                        break;
                    case WatcherChangeTypes.Deleted:
                        fileDataStore.RemoveItem(filePath);

                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing file {FilePath}.", filePath);
            }
        }
    }
}
