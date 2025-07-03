using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ;

public class RabbitMqConnection : IDisposable, IRabbitMqConnection
{
	private readonly Action<ConnectionFactory> _configureConnectionFactory;

	private readonly object _lock;
	private readonly ILogger<RabbitMqConnection> _logger;
	private IConnection? _connection;

	private CancellationTokenSource _connectionAbortedSource;
	private bool _disposed;

	public RabbitMqConnection(Action<ConnectionFactory> configureConnectionFactory, ILogger<RabbitMqConnection> logger)
	{
		_configureConnectionFactory = configureConnectionFactory;
		_logger = logger;
		_lock = new object();
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		Task.Run(async () => await Stop());
	}

	public bool IsConnected => _connection != null && _connection.IsOpen && !_disposed;
	public bool IsConnecting { get; private set; }

	public Task Connect(CancellationToken stoppingToken, out CancellationToken connectionAborted)
	{
		lock (_lock)
		{
			if (IsConnecting) throw new InvalidOperationException("Already connecting");
			IsConnecting = true;
		}

		if (_connection != null) Stop();

		_connectionAbortedSource = new CancellationTokenSource();
		connectionAborted = _connectionAbortedSource.Token;

		var retryPolicy = Policy
			.Handle<BrokerUnreachableException>()
			.Or<ConnectFailureException>()
			.Or<OperationInterruptedException>()
			.Or<SocketException>()
			.WaitAndRetryForeverAsync(
				attempt => TimeSpan.FromSeconds(Math.Pow(2, Math.Min(5, attempt))),
				(ex, time) =>
					_logger.LogInformation(ex, "Retry to connect to RabbitMQ in {Seconds} seconds", time.Seconds));


		return retryPolicy.ExecuteAsync(async () =>
		{
			if (stoppingToken.IsCancellationRequested)
			{
				await _connectionAbortedSource.CancelAsync();
				return;
			}

			var connectionFactory = CreateConnectionFactory();

			try
			{
				_logger.LogInformation("Connecting to RabbitMQ ({HostName})", connectionFactory.Endpoint.HostName);
				_connection = await connectionFactory.CreateConnectionAsync(stoppingToken);

				if (IsConnected)
				{
					_connection.ConnectionShutdownAsync += OnConnectionShutdown;
					_connection.CallbackExceptionAsync += OnCallbackException;
					_connection.ConnectionBlockedAsync += OnConnectionBlocked;

					_logger.LogInformation("Connected to RabbitMQ ({HostName})", connectionFactory.Endpoint.HostName);

					_connectionAbortedSource.TryReset();
					IsConnecting = false;
				}
				else
				{
					_logger.LogError("Failed to connect to RabbitMQ ({HostName})", connectionFactory.Endpoint.HostName);
					throw new BrokerUnreachableException(new Exception("Failed to connect to RabbitMQ"));
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to connect to RabbitMQ ({HostName})", connectionFactory.Endpoint.HostName);
				throw;
			}
		});
		//});
	}

	public IConnection GetConnection()
	{
		if (IsConnecting || !IsConnected) throw new InvalidOperationException("Not yet connected to RabbitMQ");

		return _connection!;
	}

	public async Task Stop()
	{
		if (_connection != null)
		{
			try
			{
				if (_connection.IsOpen) await _connection.CloseAsync();
			}
			catch (IOException) { }

			_connection.ConnectionShutdownAsync -= OnConnectionShutdown;
			_connection.CallbackExceptionAsync -= OnCallbackException;
			_connection.ConnectionBlockedAsync -= OnConnectionBlocked;

			_connection.Dispose();
			_connection = null;
		}
	}

	private async Task OnCallbackException(object? sender, CallbackExceptionEventArgs e)
	{
		if (_disposed) return;

		_logger.LogWarning(e.Exception, "Callback exception is thrown");
		await _connectionAbortedSource.CancelAsync();
	}

	private async Task OnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
	{
		if (_disposed) return;

		_logger.LogWarning("Connection is blocked");
		await _connectionAbortedSource.CancelAsync();
	}

	private async Task OnConnectionShutdown(object? sender, ShutdownEventArgs e)
	{
		if (_disposed) return;

		_logger.LogWarning("Connection lost");
		await _connectionAbortedSource.CancelAsync();
	}

	private ConnectionFactory CreateConnectionFactory()
	{
		var connectionFactory = new ConnectionFactory();
		_configureConnectionFactory?.Invoke(connectionFactory);
		connectionFactory.AutomaticRecoveryEnabled = false;
		return connectionFactory;
	}
}