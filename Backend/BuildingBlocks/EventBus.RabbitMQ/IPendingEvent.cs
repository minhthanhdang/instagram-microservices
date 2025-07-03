using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ;

public interface IPendingEvent
{
	ValueTask PublishAsync(IChannel channel, IRabbitMQTopology topology, ILogger logger, Action<ulong>? onObtainSeqNo,
		CancellationToken cancellationToken);

	Task WaitAsync();
	void SetComplete();
	void SetCanceled();
	void SetException(Exception exception);
}