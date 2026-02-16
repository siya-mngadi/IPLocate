using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace IPLocate.cache
{
	public class CacheDelegatingHandler : DelegatingHandler
	{
		private readonly ConcurrentDictionary<string, (DateTime expiry, CachedHttpResponse response)> _cache = new ConcurrentDictionary<string, (DateTime expiry, CachedHttpResponse response)>(StringComparer.Ordinal);
		private readonly TimeSpan _expirationDuration;

		public CacheDelegatingHandler(TimeSpan expirationDuration)
		{
			if (expirationDuration <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(expirationDuration)); 

			_expirationDuration = expirationDuration;
		}

		protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request.Method != HttpMethod.Get)
				return await base.SendAsync(request, cancellationToken);

			var cacheKey = request.RequestUri.AbsoluteUri;

			if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.expiry)
			{
				return CreateResponseMessage(cached.response, request);
			}

			_= _cache.TryRemove(cacheKey, out _);

			var response = await base.SendAsync(request, cancellationToken);

			if (!response.IsSuccessStatusCode)
				return response;

			var cachedResponse = await CreateCachedResponse(response);

			var expiry = DateTime.UtcNow.Add(_expirationDuration);

			_= _cache.TryAdd(cacheKey, (expiry, cachedResponse));

			return CreateResponseMessage(cachedResponse, request);
		}

		private HttpResponseMessage CreateResponseMessage(CachedHttpResponse original, HttpRequestMessage request)
		{
			var response = new HttpResponseMessage(original.StatusCode)
			{
				RequestMessage = request,
				ReasonPhrase = original.ReasonPhrase,
				Version = new Version(original.Version),
			};

			foreach (var header in original.Headers)
				response.Headers.TryAddWithoutValidation(header.Key, header.Value);

			if (original.Content != null)
			{
				response.Content = new ByteArrayContent(original.Content);

				foreach (var header in original.Headers)
					response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			return response;
		}

		private async Task<CachedHttpResponse> CreateCachedResponse(HttpResponseMessage response)
		{
			var contentBytes = response.Content != null
				? await response.Content.ReadAsByteArrayAsync()
				: null;

			return new CachedHttpResponse
			{
				Version = response.Version.ToString(),
				StatusCode = response.StatusCode,
				ReasonPhrase = response.ReasonPhrase,
				Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value),
				ContentHeaders = response.Content?.Headers
					.ToDictionary(h => h.Key, h => h.Value),
				Content = contentBytes
			};
		}
	}
}
