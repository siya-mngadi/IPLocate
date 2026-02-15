namespace IPLocate.Exceptions
{
	public class IPLocateRateLimitException : IPLocateApiException
	{
		public IPLocateRateLimitException(string message)
			:base(message, 429)
		{
		}
	}
}
