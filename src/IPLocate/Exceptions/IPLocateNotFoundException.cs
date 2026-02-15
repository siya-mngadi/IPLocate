using System.Net;

namespace IPLocate.Exceptions
{
	public class IPLocateNotFoundException : IPLocateApiException
	{
		public IPLocateNotFoundException(string message) 
			: base(message, (int)HttpStatusCode.NotFound)
		{
		}
	}
}
