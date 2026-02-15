using System;

namespace IPLocate.Exceptions
{
	public class IPLocateServiceException : IPLocateApiException
	{
		public IPLocateServiceException(string message, int statusCode) 
			:base(message, statusCode)
		{
		}

		public IPLocateServiceException(string message, int statusCode, Exception inner)
			: base(message, statusCode, inner)
		{		
		}
	}
}
