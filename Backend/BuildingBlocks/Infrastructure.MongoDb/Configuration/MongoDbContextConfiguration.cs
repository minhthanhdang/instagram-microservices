using MongoDB.Driver;

namespace Infrastructure.MongoDb;

public class MongoDbContextConfiguration
{
	public ClientSessionOptions? DefaultClientSessionOptions { get; set; }
	public TimeSpan TransactionRetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}