namespace EventBus;

public abstract class IntegrationEventBase
{
	internal IntegrationEventBase()
	{
		Id = Guid.NewGuid();
		CreateDate = DateTimeOffset.UtcNow;
	}

	public Guid Id { get; init; }
	public DateTimeOffset CreateDate { get; init; }
}

public abstract class IntegrationEvent<TTopic> : IntegrationEventBase where TTopic : IntegrationEventTopic { }

public abstract class IntegrationEvent : IntegrationEventBase { }