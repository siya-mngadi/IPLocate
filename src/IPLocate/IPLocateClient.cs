using IPLocate.Exceptions;
using IPLocate.Models;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace IpLocate
{
	public sealed class IPLocateClient
	{
		private readonly HttpClient _client;

		private const int TooManyRequests = 429;
		public IPLocateClient(HttpClient client)
		{
			_client = client;
		}

		public async Task<IPLocateResponse> LookupAsync(string ipAddress)
		{
			if (string.IsNullOrWhiteSpace(ipAddress))
			{
				throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddress));
			}
			var result = await PerformLookup(ipAddress);
			return result;
		}

		public async Task<IPLocateResponse> LookupCurrentIpAsync()
		{
			var result = await PerformLookup(null);
			return result;
		}

		private async Task<IPLocateResponse> PerformLookup(string ipAddressPathSegment)
		{
			var lookupPath = string.IsNullOrWhiteSpace(ipAddressPathSegment) ? "api/lookup" : $"api/lookup/{ipAddressPathSegment}";
			if (!Uri.TryCreate(_client.BaseAddress, lookupPath, out var url))
			{
				throw new ArgumentException("Failed to create valid URL for IPLocate API request.");
			}

			var request = new HttpRequestMessage(HttpMethod.Get, url.AbsolutePath);

			try
			{
				var response = await _client.SendAsync(request);
				if (response.IsSuccessStatusCode)
				{
					var responseBody = await response.Content.ReadAsStringAsync();
					try
					{
						return JsonConvert.DeserializeObject<IPLocateResponse>(responseBody);
					}
					catch (JsonException ex)
					{
						throw new IPLocateServiceException("Failed to parse IPLocate API response: " + ex.Message, (int)response.StatusCode, ex);
					}
				}
				else
				{
					await HandleErrorResponse(response);
					// Should not be reached as handleErrorResponse is always thrown
					throw new IPLocateServiceException("Unexpected state after error handling.", (int)response.StatusCode);
				}
			}
			catch (HttpRequestException ex)
			{
				throw new IPLocateServiceException($"Network error or problem reaching IPLocate API: {ex.Message}", (int)HttpStatusCode.ServiceUnavailable, ex);
			}
		}

		private async Task HandleErrorResponse(HttpResponseMessage response)
		{
			var statusCode = (int)response.StatusCode;
			string errorBodyString;

			try
			{
				var body = await response.Content.ReadAsStringAsync();
				errorBodyString = body ?? "";
			}
			catch (JsonException ex)
			{
				// This might happen if reading the error body itself fails.
				// We still want to throw based on status code.
				errorBodyString = "Failed to read error response body. Cause: " + ex.Message;
			}

			if (string.IsNullOrWhiteSpace(errorBodyString))
			{
				errorBodyString = "No error body received from server. Status code: " + statusCode;
			}

			if (statusCode >= (int)HttpStatusCode.BadRequest && statusCode < (int)HttpStatusCode.InternalServerError)
			{
				var responseBody = await response.Content.ReadAsStringAsync();
				try
				{
					var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
					var errorMessage = errorResponse?.Error ?? "Unknown error";

					switch (statusCode)
					{
						case TooManyRequests: throw new IPLocateRateLimitException(errorMessage);
						case (int)HttpStatusCode.BadRequest: throw new IPLocateInvalidIPException(errorMessage);
						case (int)HttpStatusCode.NotFound: throw new IPLocateNotFoundException(errorMessage);
						case (int)HttpStatusCode.Forbidden: throw new IPLocateApiKeyException(errorMessage);
						default: throw new IPLocateApiException($"API error: {errorMessage}", statusCode);
					}
				}
				catch (JsonException)
				{
					string baseMessage = "API request failed with status code " + statusCode +
										  ". Unable to parse error response. Raw error: " + errorBodyString;
					switch(statusCode)
					{
						case TooManyRequests: throw new IPLocateRateLimitException(baseMessage);
						case (int)HttpStatusCode.BadRequest: throw new IPLocateInvalidIPException(baseMessage);
						case (int)HttpStatusCode.NotFound: throw new IPLocateNotFoundException(baseMessage);
						case (int)HttpStatusCode.Forbidden: throw new IPLocateApiKeyException(baseMessage);
						default: throw new IPLocateApiException($"API error: {baseMessage}", statusCode);
					}
				}
				throw new IPLocateServiceException($"Authentication failed with status code {statusCode}. Response body: {errorBodyString}", statusCode);
			}
			else if (statusCode >= (int)HttpStatusCode.InternalServerError)
			{
				throw new IPLocateServiceException($"Server error: {errorBodyString}",statusCode);
			}
			else
			{
				throw new IPLocateApiException($"Unexpected  Http status code: {statusCode}. Response: {errorBodyString}", statusCode);
			}
		}
	}
}
