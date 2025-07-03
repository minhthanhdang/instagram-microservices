using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ;

public class PendingDeadLetterEvent : PendingEventBase
{
	public PendingDeadLetterEvent(string deadLetterQueue, ReadOnlyMemory<byte> body, Exception failure)
	{
		DeadLetterQueue = deadLetterQueue;
		Body = body;
		Failure = failure;
	}

	public string DeadLetterQueue { get; }
	public ReadOnlyMemory<byte> Body { get; }
	public Exception Failure { get; }

	public override async ValueTask PublishAsync(IChannel channel, IRabbitMQTopology topology, ILogger logger,
		Action<ulong>? onObtainSeqNo,
		CancellationToken cancellationToken = default)
	{
		var basicProperties = new BasicProperties
		{
			Persistent = true,
			Headers = new Dictionary<string, object?>
			{
				{ "Failure.Message", Failure.Message },
				{ "Failure.StackTrace", Failure.StackTrace ?? string.Empty }
			}
		};

		var seqNo = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
		onObtainSeqNo?.Invoke(seqNo);

		await channel.BasicPublishAsync(string.Empty,
			DeadLetterQueue,
			false,
			basicProperties,
			Body,
			cancellationToken);
	}
}