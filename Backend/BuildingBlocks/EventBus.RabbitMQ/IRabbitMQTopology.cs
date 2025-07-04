﻿using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ;

public interface IRabbitMQTopology
{
	Task DeclareTopology(IChannel channel, IServiceProvider services, ILogger logger);
	EventTopicDefinition? GetTopicDefinition(Type eventType);
	IReadOnlyList<EventQueueDefinition> GetEventQueueDefinitions();
	(Type eventType, Type eventHandlerType)? GetEventAndHandlerType(Type queueType, string eventTypeName);
}