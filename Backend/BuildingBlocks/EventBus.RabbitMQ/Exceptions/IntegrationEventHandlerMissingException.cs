namespace EventBus.RabbitMQ.Exceptions;

public class IntegrationEventHandlerMissingException : Exception
{
	public IntegrationEventHandlerMissingException(string eventTypeName)
	{
		EventTypeName = eventTypeName;
	}

	public string EventTypeName { get; }

	public override string ToString()
	{
		return $"The integration event handler for {EventTypeName} is not registered";
	}
}