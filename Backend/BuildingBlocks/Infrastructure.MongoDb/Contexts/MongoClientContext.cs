using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Infrastructure.MongoDb.Contexts;

public class MongoClientContext : IMongoClientContext
{
	private readonly MongoDbContextConfiguration _config;
	private readonly IServiceProvider _services;
	private readonly object _syncLock;
	private bool _executingTransactionScope;

	private Task<IClientSessionHandle>? _startSessionTask;

	public MongoClientContext(IServiceProvider services, IMongoClient mongoClient,
		IOptions<MongoDbContextConfiguration> config)
	{
		_services = services;
		MongoClient = mongoClient;
		_config = config.Value;
		_syncLock = new object();
	}

	public IMongoClient MongoClient { get; init; }
	public IClientSessionHandle? CurrentSession { get; private set; }

	public bool IsInTransaction =>
		CurrentSession != null && (CurrentSession.IsInTransaction || _executingTransactionScope);

	public Task<IClientSessionHandle> StartSessionAsync(ClientSessionOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		lock (_syncLock)
		{
			if (_startSessionTask != null) return _startSessionTask;

			if (options == null) options = _config.DefaultClientSessionOptions;

			async Task<IClientSessionHandle> DoStartSession(ClientSessionOptions? options = null,
				CancellationToken cancellationToken = default)
			{
				CurrentSession = await MongoClient.StartSessionAsync(options, cancellationToken);
				return CurrentSession;
			}

			_startSessionTask = DoStartSession(options, cancellationToken);
			return _startSessionTask;
		}
	}

	public async Task<long> CommitAsync(CancellationToken cancellationToken = default)
	{
		var executingRetryScope = _executingTransactionScope;
		var collectionContexts = _services.GetServices<IMongoCollectionContextBase>();

		var writeOperationsCount = collectionContexts.Sum(x => x.WriteOperationsCount);

		try
		{
			if (writeOperationsCount == 1) return await CommitSingleAsync(collectionContexts, cancellationToken);

			if (writeOperationsCount > 1) return await CommitInTransactionAsync(collectionContexts, cancellationToken);
		}
		catch (MongoException)
		{
			if (CurrentSession != null && CurrentSession.IsInTransaction)
				await CurrentSession.AbortTransactionAsync(cancellationToken);

			throw;
		}

		return 0;
	}

	public async Task ExecuteTransactionAsync(Func<Task> task, TransactionOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		if (CurrentSession == null) await StartSessionAsync(_config.DefaultClientSessionOptions, cancellationToken);

		using (var scope = new TransactionScope(this))
		{
			Task taskToAwait;

			if (_executingTransactionScope)
			{
				taskToAwait = task.Invoke();
			}
			else
			{
				scope.Execute();
				taskToAwait = RunTransactionAsync(task, options, cancellationToken);
			}

			await taskToAwait;
		}
	}

	public IMongoCollectionContext<TDocument> GetCollection<TDocument>()
	{
		return _services.GetRequiredService<IMongoCollectionContext<TDocument>>();
	}

	public void Dispose()
	{
		ResetSession();
	}

	public void Reset()
	{
		var collectionContexts = _services.GetServices<IMongoCollectionContextBase>();
		foreach (var collectionContext in collectionContexts) collectionContext.Reset();
	}

	private static async Task<long> CommitSingleAsync(IEnumerable<IMongoCollectionContextBase> collectionContexts,
		CancellationToken cancellationToken)
	{
		return (await Task.WhenAll(collectionContexts.Select(x => x.CommitAsync(cancellationToken)))).Sum();
	}

	private async Task<long> CommitInTransactionAsync(IEnumerable<IMongoCollectionContextBase> collectionContexts,
		CancellationToken cancellationToken)
	{
		if (CurrentSession != null && CurrentSession.IsInTransaction)
			return (await Task.WhenAll(collectionContexts.Select(x => x.CommitAsync(cancellationToken)))).Sum();

		if (CurrentSession == null) await StartSessionAsync(_config.DefaultClientSessionOptions, cancellationToken);

		long updatedCount = 0;

		await ExecuteTransactionAsync(
			async () =>
			{
				updatedCount =
					(await Task.WhenAll(collectionContexts.Select(x => x.CommitAsync(cancellationToken)))).Sum();
			}, null, cancellationToken);

		return updatedCount;
	}

	private async Task RunTransactionAsync(Func<Task> task, TransactionOptions? options,
		CancellationToken cancellationToken)
	{
		await CurrentSession!.WithTransactionAsync<object?>(
			async (session, cancellationToken) =>
			{
				try
				{
					await task.Invoke();
				}
				catch (MongoException ex)
					when (ex.HasErrorLabel("TransientTransactionError") ||
					      ex.HasErrorLabel("UnknownTransactionCommitResult"))
				{
					await Task.Delay(_config.TransactionRetryDelay);
					throw;
				}

				return null;
			},
			options,
			cancellationToken);
	}

	private void ResetSession()
	{
		if (CurrentSession != null)
		{
			CurrentSession.Dispose();
			CurrentSession = null;
			_startSessionTask = null;
		}
	}

	private class TransactionScope : IDisposable
	{
		private readonly MongoClientContext _context;
		private bool _executing;

		public TransactionScope(MongoClientContext context)
		{
			_context = context;
		}

		public void Dispose()
		{
			if (_executing) _context._executingTransactionScope = false;
		}

		public void Execute()
		{
			_executing = true;
			_context._executingTransactionScope = true;
		}
	}
}