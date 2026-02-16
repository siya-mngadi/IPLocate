using System.Net;

namespace IPLocate.Exceptions
{
	public class IPLocateRateLimitException : IPLocateApiException
	{
		public IPLocateRateLimitException(string message)
			:base(message, HttpStatusCode.TooManyRequests)
		{
		}
	}
}
