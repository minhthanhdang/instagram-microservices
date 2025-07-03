using RabbitMQ.Client;

namespace EventBus.RabbitMQ;

public interface IRabbitMqConnection
{
	bool IsConnected { get; }
	bool IsConnecting { get; }

	IConnection GetConnection();
	Task Connect(CancellationToken stoppingToken, out CancellationToken connectionAborted);
	Task Stop();
}