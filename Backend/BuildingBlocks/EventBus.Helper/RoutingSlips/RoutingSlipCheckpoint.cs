using EventBus.Helper.RoutingSlips.Contracts;

namespace EventBus.Helper.RoutingSlips;

public class RoutingSlipCheckpoint : IRoutingSlipCheckpoint
{
	public RoutingSlipCheckpoint(string name, string destination, string? propertiesData)
	{
		Name = name;
		Destination = destination;
		PropertiesData = propertiesData;
	}

	public string Name { get; set; }
	public string Destination { get; set; }
	public string? PropertiesData { get; set; }
}