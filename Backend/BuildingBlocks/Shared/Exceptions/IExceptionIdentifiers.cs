namespace Shared.Exceptions;

public interface IExceptionIdentifier
{
	bool Identify(Exception ex, params object?[] entities);
}