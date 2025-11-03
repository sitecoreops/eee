using System.Threading.Channels;

namespace ExperienceEdgeEmu.Web.Media;

public record MediaDownloadMessage(string Url);

public class MediaDownloadQueue
{
    private readonly Channel<MediaDownloadMessage> _channel = Channel.CreateUnbounded<MediaDownloadMessage>();

    public ValueTask QueueMessageAsync(MediaDownloadMessage message) => _channel.Writer.WriteAsync(message);

    public IAsyncEnumerable<MediaDownloadMessage> DequeueAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);

    public bool IsEmpty() => _channel.Reader.Count == 0;
}
