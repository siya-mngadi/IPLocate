using System.Net;

namespace IPLocate.Exceptions
{
	public class IPLocateInvalidRequestException : IPLocateApiException
	{
		public IPLocateInvalidRequestException(string message)
			: base(message, HttpStatusCode.BadRequest)
		{
		}
	}
}
