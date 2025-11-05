using ExperienceEdgeEmu.Web.Media;
using System.Text.Json;

namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

public partial class ItemPostProcessingWorker(ItemPostProcessingQueue postProcessingQueue, MediaDownloadQueue mediaDownloadQueue, ILogger<ItemPostProcessingWorker> logger, JsonMediaUrlReplacer mediaUrlReplacer) : BackgroundService
{
    private static readonly JsonSerializerOptions _fileWritingJsonSerializerOptions = new() { WriteIndented = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in postProcessingQueue.DequeueAllAsync(stoppingToken))
        {
            logger.LogInformation("Processing message for file {FilePath}.", message.FilePath);

            try
            {
                // replace media urls
                var changes = mediaUrlReplacer.ReplaceMediaUrlsInFields(message.JsonData);

                if (changes.Count > 0)
                {
                    // save the modified json data
                    using var stream = await CreateFileWithRetryAsync(message.FilePath);

                    await JsonSerializer.SerializeAsync(stream, new { data = new { item = message.JsonData } }, _fileWritingJsonSerializerOptions, stoppingToken);

                    logger.LogInformation("Saved post processed item data to {FilePath}", message.FilePath);

                    // queue download media messages
                    foreach (var change in changes)
                    {
                        var originalUri = new Uri(change.Key, UriKind.RelativeOrAbsolute);
                        var newUri = new Uri(change.Value, UriKind.RelativeOrAbsolute);

                        await mediaDownloadQueue.QueueMessageAsync(new MediaDownloadMessage(originalUri, newUri));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "File post processing failed for file {FilePath}.", message.FilePath);
            }
        }
    }

    public async Task<Stream> CreateFileWithRetryAsync(string filePath, int maxRetries = 3, int delayMs = 100)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                return File.Create(filePath);
            }
            catch (IOException) when (attempt < maxRetries)
            {
                logger.LogWarning("Attempt {Attempt} to write to {FilePath} failed, retrying...", attempt, filePath);

                attempt++;

                await Task.Delay(delayMs);
            }
        }
    }
}
