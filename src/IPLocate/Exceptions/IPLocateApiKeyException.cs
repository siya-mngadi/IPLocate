using System.Net;

namespace IPLocate.Exceptions
{
	public class IPLocateApiKeyException : IPLocateApiException
	{
		public IPLocateApiKeyException(string message) 
			: base(message, (int)HttpStatusCode.Forbidden)
		{
		}
	}
}
