using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.TransactionalEvents.Extensions;

public class TransactionalEventsContextConfigurator
{
	public TransactionalEventsContextConfigurator(IServiceCollection services)
	{
		Services = services;
	}

	public IServiceCollection Services { get; private set; }
}