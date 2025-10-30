using ExperienceEdgeEmu.Web.EmuSchema;
using Microsoft.Extensions.Caching.Memory;

namespace ExperienceEdgeEmu.Web.Media;

public class MediaDownloadWorker(MediaDownloadQueue _queue, ILogger<MediaDownloadWorker> _logger, EmuFileSystem _emuFileSystem, IHttpClientFactory _httpClientFactory, IMemoryCache _processedMedia) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.DequeueAllAsync(stoppingToken))
        {
            var mediaUrl = message.Url;

            try
            {
                var uri = new Uri(mediaUrl, UriKind.RelativeOrAbsolute);

                if (!uri.IsAbsoluteUri)
                {
                    _logger.LogWarning("Skipping relative url: {MediaUrl}.", mediaUrl);

                    continue;
                }

                var key = uri.GetLeftPart(UriPartial.Path);

                if (_processedMedia.TryGetValue(key, out _))
                {
                    _logger.LogDebug("Media {MediaUrl} has already been processed, skipping.", mediaUrl);

                    continue;
                }

                var filePath = _emuFileSystem.GetMediaFilePath(uri);
                var directory = Directory.GetParent(filePath)!.FullName;

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _logger.LogInformation("Downloading media {MediaUrl} into {FilePath}.", mediaUrl, filePath);

                using (var mediaStream = await _httpClientFactory.CreateClient().GetStreamAsync(uri, stoppingToken))
                {
                    using var fileStream = new FileStream(filePath, FileMode.Create);

                    await mediaStream.CopyToAsync(fileStream, stoppingToken);
                }

                _processedMedia.GetOrCreate(key, entry =>
                {
                    entry.SetSlidingExpiration(TimeSpan.FromMinutes(30));

                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Media download failed for {MediaUrl}.", mediaUrl);
            }
        }
    }
}
