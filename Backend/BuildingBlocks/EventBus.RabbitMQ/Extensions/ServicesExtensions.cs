using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Extensions;

public static class ServicesExtensions
{
	public static IEventBusBuilder AddRabbitMqEventBus(
		this IServiceCollection services,
		Action<IRabbitMQEventBusConfigurator>? config = null)
	{
		List<Type> integrationEventTypes = new List<Type>();
		List<Type> integrationEventHandlerTypes = new List<Type>();

		var builder = new EventBusBuilder(services, integrationEventTypes, integrationEventHandlerTypes);

		var configurator = new RabbitMQEventBusConfigurator();

		config?.Invoke(configurator);

		services.TryAddSingleton(_ => new RabbitMQEventBusConfiguration(configurator));

		services.AddHostedService<RabbitMqEventBusService>();

		services.TryAddSingleton<IRabbitMqConnection>(sp =>
		{
			return new RabbitMqConnection(
				configurator.ConnectionFactory,
				sp.GetRequiredService<ILogger<RabbitMqConnection>>());
		});

		services.TryAddSingleton<RabbitMQEventBus>();
		services.TryAddSingleton<IEventBus>(sp => sp.GetRequiredService<RabbitMQEventBus>());
		services.TryAddSingleton<IDeadLetterEventBus>(sp => sp.GetRequiredService<RabbitMQEventBus>());
		services.TryAddSingleton<IPendingEvents>(sp => sp.GetRequiredService<RabbitMQEventBus>());

		services.TryAddSingleton<IRabbitMQTopology>(_ =>
			new RabbitMQTopology(integrationEventTypes, integrationEventHandlerTypes));

		if (configurator.HealthCheckConfig != null)
			services.AddHealthChecks().AddRabbitMQ(
				sp =>
				{
					var cf = new ConnectionFactory();
					configurator.ConnectionFactory(cf);
					return cf.CreateConnectionAsync();
				},
				configurator.HealthCheckConfig.Name,
				configurator.HealthCheckConfig.FailureStatus,
				configurator.HealthCheckConfig.Tags,
				configurator.HealthCheckConfig.Timeout);

		return builder;
	}
}