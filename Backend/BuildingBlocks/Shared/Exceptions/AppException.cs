namespace Shared.Exceptions;

public class AppException : Exception
{
	public AppException(string? message, Exception? inner, int? statusCode)
		: base(message, inner)
	{
		StatusCode = statusCode;
	}

	public int? StatusCode { get; set; }
}