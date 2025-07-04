using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ;

public class RabbitMqEventBusService : BackgroundService
{
	private readonly RabbitMQEventBusConfiguration _configuration;
	private readonly IRabbitMqConnection _connection;
	private readonly IDeadLetterEventBus _deadLetterEventBus;
	private readonly ILogger<RabbitMqEventBusService> _logger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IPendingEvents _pendingEvents;

	private readonly IServiceProvider _serviceProvider;
	private readonly IRabbitMQTopology _topology;

	public RabbitMqEventBusService(IServiceProvider serviceProvider, IDeadLetterEventBus deadLetterEventBus,
		IRabbitMqConnection connection, IRabbitMQTopology topology, IPendingEvents pendingEvents,
		RabbitMQEventBusConfiguration configuration, ILoggerFactory loggerFactory)
	{
		_serviceProvider = serviceProvider;
		_deadLetterEventBus = deadLetterEventBus;
		_connection = connection;
		_topology = topology;
		_pendingEvents = pendingEvents;
		_configuration = configuration;
		_loggerFactory = loggerFactory;

		_logger = loggerFactory.CreateLogger<RabbitMqEventBusService>();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Connect to RabbitMQ
				await _connection.Connect(stoppingToken, out var connectionAborted);

				// Run channels
				if (!stoppingToken.IsCancellationRequested && !connectionAborted.IsCancellationRequested)
				{
					_logger.LogInformation("Running channels");

					List<Task> tasks = new List<Task>();

					tasks.Add(RunPubChannel(connectionAborted));
					tasks.Add(RunSubChannel(connectionAborted));
					tasks.Add(ShutdownTask(stoppingToken, connectionAborted));

					// Wait until all channels stopped
					await Task.WhenAll(tasks);

					_logger.LogInformation("Channels stopped");
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error has occurred");
			}

			if (!stoppingToken.IsCancellationRequested) _logger.LogInformation("Trying to reconnect to RabbitMQ");
		}

		_connection.Stop();
	}

	private async Task ShutdownTask(CancellationToken stoppingToken, CancellationToken connectionAborted)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, connectionAborted);

		try
		{
			await Task.Delay(Timeout.Infinite, cts.Token);
		}
		catch (OperationCanceledException)
		{
			if (!connectionAborted.IsCancellationRequested) _connection.Stop();
		}
		finally
		{
			cts.Cancel();
		}
	}

	private async Task RunPubChannel(CancellationToken connectionAborted)
	{
		var channel = new RabbitMqEventPubChannel(
			_serviceProvider,
			_pendingEvents,
			_connection.GetConnection(),
			_topology,
			_configuration,
			_loggerFactory.CreateLogger<RabbitMqEventPubChannel>(),
			connectionAborted);

		await channel.RunChannel();
	}

	private async Task RunSubChannel(CancellationToken connectionAborted)
	{
		var channel = new RabbitMQEventSubChannel(
			_serviceProvider,
			_deadLetterEventBus,
			_connection.GetConnection(),
			_topology,
			_configuration,
			_loggerFactory.CreateLogger<RabbitMQEventSubChannel>(),
			_loggerFactory,
			connectionAborted);

		await channel.RunChannel();
	}
}