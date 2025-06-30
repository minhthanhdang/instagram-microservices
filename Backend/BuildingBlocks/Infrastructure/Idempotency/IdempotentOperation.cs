namespace Infrastructure.Idempotency;

public class IdempotentOperation
{
	private IdempotentOperation(string id, DateTimeOffset date)
	{
		Id = id;
		Date = date;
	}

	public string Id { get; private set; }
	public DateTimeOffset Date { get; private set; }

	public static IdempotentOperation Create(string id, DateTimeOffset date)
	{
		return new IdempotentOperation(id, date);
	}
}