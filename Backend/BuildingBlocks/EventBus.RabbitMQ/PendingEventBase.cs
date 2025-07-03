using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ;

public abstract class PendingEventBase : IPendingEvent
{
	private readonly TaskCompletionSource _complete;

	public PendingEventBase()
	{
		_complete = new TaskCompletionSource();
	}

	public abstract ValueTask PublishAsync(IChannel channel, IRabbitMQTopology topology, ILogger logger,
		Action<ulong>? onObtainSeqNo,
		CancellationToken cancellationToken = default);

	public void SetCanceled()
	{
		_complete.SetCanceled();
	}

	public void SetComplete()
	{
		_complete.SetResult();
	}

	public void SetException(Exception exception)
	{
		_complete.SetException(exception);
	}

	public Task WaitAsync()
	{
		return _complete.Task;
	}
}