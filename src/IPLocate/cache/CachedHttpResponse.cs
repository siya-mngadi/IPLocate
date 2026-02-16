using System.Collections.Generic;
using System.Net;

namespace IPLocate.cache
{
	public class CachedHttpResponse
	{
		public HttpStatusCode StatusCode { get; set; }
		public byte[] Content { get; set; }
		public string Version { get; set; }
		public string ReasonPhrase { get; set; }
		public Dictionary<string, IEnumerable<string>> Headers { get; set; }
		public Dictionary<string, IEnumerable<string>> ContentHeaders { get; set; }
	}
}
