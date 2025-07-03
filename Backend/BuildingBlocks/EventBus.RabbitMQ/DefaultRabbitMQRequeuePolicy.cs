using Shared.Exceptions;

namespace EventBus.RabbitMQ;

public class DefaultRabbitMQRequeuePolicy : IRabbitMQRequeuePolicy
{
	public bool ShouldRequeue(Exception exception)
	{
		return exception.Identify(ExceptionCategories.Transient);
	}
}