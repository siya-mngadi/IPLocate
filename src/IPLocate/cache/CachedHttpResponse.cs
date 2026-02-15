using System.Collections.Generic;

namespace IPLocate.cache
{
	public class CachedHttpResponse
	{
		public int StatusCode { get; set; }
		public byte[] Content { get; set; }
		public string Version { get; set; }
		public string ReasonPhrase { get; set; }
		public Dictionary<string, IEnumerable<string>> Headers { get; set; }
		public Dictionary<string, IEnumerable<string>> ContentHeaders { get; set; }
	}
}
