namespace EventBus.RabbitMQ;

public interface IRabbitMQRequeuePolicy
{
	bool ShouldRequeue(Exception exception);
}