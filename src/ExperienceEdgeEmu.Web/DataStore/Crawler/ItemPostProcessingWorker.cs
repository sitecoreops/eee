using ExperienceEdgeEmu.Web.Media;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

public partial class ItemPostProcessingWorker(ItemPostProcessingQueue postProcessingQueue, MediaDownloadQueue mediaDownloadQueue, ILogger<ItemPostProcessingWorker> logger, MediaUrlReplacer mediaUrlReplacer, IOptions<EmuSettings> options) : BackgroundService
{
    private readonly EmuSettings _settings = options.Value;
    private static readonly JsonSerializerOptions _fileWritingJsonSerializerOptions = new() { WriteIndented = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in postProcessingQueue.DequeueAllAsync(stoppingToken))
        {
            logger.LogInformation("Processing message for file {FilePath}.", message.FilePath);

            try
            {
                // replace media urls
                var changes = mediaUrlReplacer.ReplaceMediaUrlsInFields(message.JsonData, _settings.MediaHost);

                if (changes.Count > 0)
                {
                    // save the modified json data
                    await using var stream = File.Create(message.FilePath);

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
}
