using System.Text.Json;
using Shared.Utilities;

namespace Domain.TransactionalEvents;

public class TransactionalEvent
{
	private static readonly JsonSerializerOptions JsonSerializerOptions = new()
	{
		IncludeFields = true
	};

	public TransactionalEvent(string category, string type, string data)
	{
		Category = category;
		Type = type;
		Data = data;
	}

	public TransactionalEvent(string category, object @event, Type? type = null)
	{
		Category = category;
		Type = type?.FullName! ?? @event.GetType().FullName!;
		Data = JsonSerializer.Serialize(@event);
	}

	public string Category { get; private set; }
	public string Type { get; }
	public string Data { get; }

	public object? GetEvent()
	{
		if (string.IsNullOrEmpty(Type) || string.IsNullOrEmpty(Data)) return null;

		var type = TypeCache.GetType(Type);

		if (type == null) return null;

		return JsonSerializer.Deserialize(Data, type, JsonSerializerOptions);
	}
}