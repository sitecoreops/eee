using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

public record ItemPostProcessingMessage(string FilePath, JsonObject JsonData);

public class ItemPostProcessingQueue
{
    private readonly Channel<ItemPostProcessingMessage> _channel = Channel.CreateUnbounded<ItemPostProcessingMessage>();

    public ValueTask QueueMessageAsync(ItemPostProcessingMessage message) => _channel.Writer.WriteAsync(message);

    public IAsyncEnumerable<ItemPostProcessingMessage> DequeueAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);

    public bool IsEmpty() => _channel.Reader.Count == 0;
}
