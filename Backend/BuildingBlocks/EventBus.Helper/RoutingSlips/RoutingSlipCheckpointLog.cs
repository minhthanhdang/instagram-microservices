using EventBus.Helper.RoutingSlips.Contracts;

namespace EventBus.Helper.RoutingSlips;

public class RoutingSlipCheckpointLog : IRoutingSlipCheckpointLog
{
	public RoutingSlipCheckpointLog(string name, RoutingSlipEventType type, DateTimeOffset date, bool success,
		string? message)
	{
		Name = name;
		Type = type;
		Date = date;
		Success = success;
		Message = message;
	}

	public string Name { get; set; }
	public RoutingSlipEventType Type { get; set; }
	public DateTimeOffset Date { get; set; }
	public bool Success { get; set; }
	public string? Message { get; set; }
}