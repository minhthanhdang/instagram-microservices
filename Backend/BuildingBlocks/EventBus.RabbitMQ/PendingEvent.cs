using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.Utilities;

namespace EventBus.RabbitMQ;

public class PendingEvent : PendingEventBase
{
	private static readonly JsonSerializerOptions JsonSerializerOptions = new()
	{
		IncludeFields = true,
		WriteIndented = false
	};

	public PendingEvent(Type eventType, IntegrationEventBase @event, RabbitMQIntegrationEventProperties properties)
	{
		EventType = eventType;
		Event = @event;
		Properties = properties;
	}

	public Type EventType { get; }
	public IntegrationEventBase Event { get; }
	public RabbitMQIntegrationEventProperties Properties { get; }

	public override async ValueTask PublishAsync(IChannel channel, IRabbitMQTopology topology, ILogger logger,
		Action<ulong>? onObtainSeqNo, CancellationToken cancellationToken = default)
	{
		var eventTypeName = EventType.GetGenericTypeName();

		var data = JsonSerializer.Serialize(Event, EventType, JsonSerializerOptions)!;
		var body = JsonSerializer.SerializeToUtf8Bytes(new RabbitMQIntegrationEvent(eventTypeName, data),
			JsonSerializerOptions)!;

		var basicProperties = new BasicProperties
		{
			Persistent = Properties.Persistent ?? true,
			Priority = Properties.Priority ?? 0,
			Headers = new Dictionary<string, object?>()
		};

		if (Properties.Headers != null)
			foreach (var kv in Properties.Headers)
				basicProperties.Headers[kv.Key] = kv.Value;

		var exchangeName = string.Empty;

		if (!Properties.SkipExchange.HasValue || !Properties.SkipExchange.Value ||
		    string.IsNullOrEmpty(Properties.RoutingKey))
		{
			var topicDefinition = topology.GetTopicDefinition(EventType)!;

			if (topicDefinition == null) throw new ArgumentException("The event type is not registered to a topic");

			exchangeName = topicDefinition.ExchangeName;
			logger.LogInformation("Publishing event ({EventTypeName}) ({IntegrationEvent_Id}) to exchange ({ExchangeName})",
				eventTypeName, Event.Id, topicDefinition.ExchangeName);
		}
		else
		{
			logger.LogInformation("Publishing event ({EventTypeName}) ({IntegrationEvent_Id}) to queue ({Queue})",
				eventTypeName, Event.Id, Properties.RoutingKey);
		}

		var seqNo = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
		onObtainSeqNo?.Invoke(seqNo);

		await channel.BasicPublishAsync(exchangeName,
			Properties.RoutingKey != null ? Properties.RoutingKey : string.Empty,
			false,
			basicProperties,
			body,
			cancellationToken);
	}
}