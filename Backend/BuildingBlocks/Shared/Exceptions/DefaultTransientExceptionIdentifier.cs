using System.Net;

namespace Shared.Exceptions;

public class DefaultTransientExceptionIdentifier : IExceptionIdentifier
{
	public bool Identify(Exception ex, params object?[] entities)
	{
		if (ex is OperationCanceledException) return true;

		if (ex is TransientException) return true;

		if (ex is TimeoutException) return true;

		if (ex is AppException appEx)
			if (appEx.StatusCode != null &&
			    (appEx.StatusCode == (int)HttpStatusCode.RequestTimeout ||
			     appEx.StatusCode >= (int)HttpStatusCode.InternalServerError))
				return true;

		return false;
	}
}