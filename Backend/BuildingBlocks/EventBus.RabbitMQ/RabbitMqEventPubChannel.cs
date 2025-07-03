using System.Collections.Concurrent;
using System.Net.Sockets;
using EventBus.RabbitMQ.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ;

public class RabbitMqEventPubChannel
{
	private readonly RabbitMQEventBusConfiguration _configuration;
	private readonly IConnection _connection;
	private readonly CancellationToken _connectionAborted;
	private readonly ILogger<RabbitMqEventPubChannel> _logger;
	private readonly ConcurrentDictionary<ulong, IPendingEvent> _outstandingConfirms;
	private readonly IPendingEvents _pendingEvents;
	private readonly AsyncPolicy _retryPolicy;

	private readonly IServiceProvider _serviceProvider;
	private readonly IRabbitMQTopology _topology;

	public RabbitMqEventPubChannel(IServiceProvider serviceProvider, IPendingEvents pendingEvents, IConnection connection,
		IRabbitMQTopology topology, RabbitMQEventBusConfiguration configuration, ILogger<RabbitMqEventPubChannel> logger,
		CancellationToken connectionAborted)
	{
		_serviceProvider = serviceProvider;
		_connection = connection;
		_topology = topology;
		_configuration = configuration;
		_pendingEvents = pendingEvents;
		_logger = logger;
		_connectionAborted = connectionAborted;
		_outstandingConfirms = new ConcurrentDictionary<ulong, IPendingEvent>();

		_retryPolicy = Policy
			.Handle<BrokerUnreachableException>()
			.Or<ConnectFailureException>()
			.Or<OperationInterruptedException>()
			.Or<SocketException>()
			.RetryAsync(_configuration.Publishing.MaxRetryCount);
	}

	public async Task RunChannel()
	{
		while (!_connectionAborted.IsCancellationRequested)
		{
			IChannel? channel = null;

			try
			{
				var channelAbortedSource = new CancellationTokenSource();
				var channelAborted = channelAbortedSource.Token;
				channel = await _connection.CreateChannelAsync(cancellationToken: channelAborted);

				channel.BasicAcksAsync += OnBasicAcks;
				channel.BasicNacksAsync += OnBasicNacks;
				channel.CallbackExceptionAsync += OnCallbackException(channelAbortedSource);

				await _topology.DeclareTopology(channel, _serviceProvider, _logger);

				using var cts = CancellationTokenSource.CreateLinkedTokenSource(_connectionAborted, channelAborted);
				await Run(channel, cts.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error has occurred when running pub channel");
			}
			finally
			{
				ClearOutstandingConfirms();
			}

			if (channel != null)
			{
				await DisposeChannelAsync(channel);
				channel = null;
			}

			if (!_connectionAborted.IsCancellationRequested) _logger.LogInformation("Restarting pub channel");
		}
	}

	private async Task DisposeChannelAsync(IChannel channel)
	{
		if (channel.IsOpen) await channel.CloseAsync(_connectionAborted);
		await channel.DisposeAsync();
	}

	// private async Task CreateChannelAsync(out IChannel channel, out CancellationToken channelAborted)
	// {
	// 	var channelAbortedSource = new CancellationTokenSource();
	// 	channelAborted = channelAbortedSource.Token;
	//
	// 	channel = await _connection.CreateChannelAsync(cancellationToken: channelAborted);
	//
	// 	channel.BasicAcksAsync += OnBasicAcks;
	// 	channel.BasicNacksAsync += OnBasicNacks;
	// 	channel.CallbackExceptionAsync += OnCallbackException(channelAbortedSource);
	//
	// 	_topology.DeclareTopology(channel, _serviceProvider, _logger);
	// }

	private AsyncEventHandler<CallbackExceptionEventArgs> OnCallbackException(
		CancellationTokenSource channelAbortedSource)
	{
		return async (sender, e) =>
		{
			_logger.LogWarning(e.Exception, "Callback exception occurred");
			await channelAbortedSource.CancelAsync();
		};
	}

	private async Task OnBasicAcks(object sender, BasicAckEventArgs e)
	{
		if (e.Multiple)
		{
			var confirmed = _outstandingConfirms.Where(k => k.Key <= e.DeliveryTag);

			foreach (var entry in confirmed) ConfirmAck(entry.Key, true);
		}
		else
		{
			ConfirmAck(e.DeliveryTag, true);
		}
	}

	private async Task OnBasicNacks(object? sender, BasicNackEventArgs e)
	{
		if (e.Multiple)
		{
			var confirmed = _outstandingConfirms.Where(k => k.Key <= e.DeliveryTag);

			foreach (var entry in confirmed) ConfirmAck(entry.Key, false);
		}
		else
		{
			ConfirmAck(e.DeliveryTag, false);
		}
	}

	private void ConfirmAck(ulong sequenceNumber, bool acked)
	{
		if (_outstandingConfirms.TryRemove(sequenceNumber, out var pendingIntegrationEvent))
		{
			if (acked)
				pendingIntegrationEvent.SetComplete();
			else
				pendingIntegrationEvent.SetException(new RabbitMQNackException());
		}
	}

	private void ClearOutstandingConfirms()
	{
		var outstandingConfirms = _outstandingConfirms.Values.ToList();
		_outstandingConfirms.Clear();

		outstandingConfirms.ForEach(x => x.SetException(new PublishChannelStoppedException()));
	}

	private async Task Run(IChannel channel, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			IPendingEvent? pendingEvent = null;
			try
			{
				pendingEvent = await _pendingEvents.PollPendingEvent(cancellationToken).ConfigureAwait(false);

				await _retryPolicy.ExecuteAsync(async () =>
				{
					await pendingEvent.PublishAsync(channel, _topology, _logger,
						seqNo => { _outstandingConfirms.TryAdd(seqNo, pendingEvent); }, cancellationToken);
				});
			}
			catch (Exception ex) when (ex is OperationCanceledException || ex is AlreadyClosedException)
			{
				_logger.LogInformation("Pub channel is stopping");

				if (pendingEvent != null) pendingEvent.SetException(new PublishChannelStoppedException());
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "An error has occurred during event publishing");

				if (pendingEvent != null) pendingEvent.SetException(ex);
			}
		}
	}
}