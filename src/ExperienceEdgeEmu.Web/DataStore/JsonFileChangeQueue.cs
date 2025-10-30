using System.Threading.Channels;

namespace ExperienceEdgeEmu.Web.DataStore;

public class JsonFileChangeQueue
{
    private readonly Channel<(string, WatcherChangeTypes)> _queue;

    public JsonFileChangeQueue() => _queue = Channel.CreateUnbounded<(string, WatcherChangeTypes)>();

    public void QueueMessage(string filePath, WatcherChangeTypes changeType)
    {
        if (!_queue.Writer.TryWrite((filePath, changeType)))
        {
            throw new InvalidOperationException("Failed to queue file change message.");
        }
    }

    public IAsyncEnumerable<(string, WatcherChangeTypes)> ReadAllAsync(CancellationToken cancellationToken = default) => _queue.Reader.ReadAllAsync(cancellationToken);
}
