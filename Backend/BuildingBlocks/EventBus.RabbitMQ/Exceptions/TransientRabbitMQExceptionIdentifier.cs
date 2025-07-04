using System.Net.Sockets;
using RabbitMQ.Client.Exceptions;
using Shared.Exceptions;

namespace EventBus.RabbitMQ.Exceptions;

public class TransientRabbitMQExceptionIdentifier : IExceptionIdentifier
{
	public bool Identify(Exception ex, params object?[] entities)
	{
		if (ex is BrokerUnreachableException ||
		    ex is ConnectFailureException ||
		    ex is OperationInterruptedException ||
		    ex is SocketException ||
		    ex is AlreadyClosedException ||
		    ex is OperationCanceledException)
			return true;

		return false;
	}
}