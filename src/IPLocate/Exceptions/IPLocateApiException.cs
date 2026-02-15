using System;

namespace IPLocate.Exceptions
{
	public class IPLocateApiException : Exception
	{
		public int StatusCode { get; set; }
		public IPLocateApiException(string message, int statusCode)
			: base(message)
		{
			StatusCode = statusCode; 
		}

		public IPLocateApiException(string message, int statusCode, Exception innerException) 
			: base(message, innerException)
		{
			StatusCode = statusCode;
		}
	}
}
