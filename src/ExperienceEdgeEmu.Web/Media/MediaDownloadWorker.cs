using ExperienceEdgeEmu.Web.EmuSchema;
using Microsoft.Extensions.Caching.Memory;

namespace ExperienceEdgeEmu.Web.Media;

public class MediaDownloadWorker(MediaDownloadQueue queue, ILogger<MediaDownloadWorker> logger, EmuFileSystem emuFileSystem, IHttpClientFactory httpClientFactory, IMemoryCache processedMedia) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                if (!message.Original.IsAbsoluteUri)
                {
                    logger.LogWarning("Skipping relative url: {MediaOrignalUri}.", message.Original);

                    continue;
                }

                var mediaKey = $"{nameof(MediaDownloadWorker)}:{message.Original.GetLeftPart(UriPartial.Path)}";

                if (processedMedia.TryGetValue(mediaKey, out _))
                {
                    logger.LogDebug("Media {MediaOrignalUri} has already been processed, skipping.", message.Original);

                    continue;
                }

                var filePath = emuFileSystem.GetMediaFilePath(message.New);
                var directory = Directory.GetParent(filePath)!.FullName;

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                logger.LogInformation("Downloading media {MediaOrignalUri} into {FilePath}.", message.Original, filePath);

                using var mediaStream = await httpClientFactory.CreateClient().GetStreamAsync(message.Original, stoppingToken);
                using var fileStream = new FileStream(filePath, FileMode.Create);

                await mediaStream.CopyToAsync(fileStream, stoppingToken);

                processedMedia.GetOrCreate(mediaKey, entry =>
                {
                    entry.SetSlidingExpiration(TimeSpan.FromMinutes(30));

                    return true;
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Media download failed for {MediaOrignalUri}.", message.Original);
            }
        }
    }
}
