using System.Net;

namespace IPLocate.Exceptions
{
	public class IPLocateInvalidIPException : IPLocateApiException
	{
		public IPLocateInvalidIPException(string message)
			: base(message, (int)HttpStatusCode.BadRequest)
		{
		}
	}
}
