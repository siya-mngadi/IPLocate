using IPLocate.Exceptions;
using IPLocate.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace IpLocate
{
	public sealed class IPLocateClient
	{
		private readonly HttpClient _client;
		public IPLocateClient(HttpClient client)
		{
			_client = client;
		}

		/// <summary>
		/// Asynchronously retrieves location information for the specified IP address.
		/// </summary>
		/// <remarks>This method sends a GET request to the lookup service and returns the response
		/// asynchronously.</remarks>
		/// <param name="ipAddress">The IP address to look up. Must be a valid IP address and cannot be null or empty.</param>
		/// <returns>An instance of <see cref="IPLocateResponse"/> containing the location details for the specified IP address.</returns>
		/// <exception cref="ArgumentException">Thrown if <paramref name="ipAddress"/> is null, empty, or not a valid IP address.</exception>
		public async Task<IPLocateResponse> LookupAsync(string ipAddress)
		{
			if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var ip))
			{
				throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddress));
			}
			var request = new HttpRequestMessage(HttpMethod.Get, GetUriAbsolutePath($"lookup/{ip}"));
			return await SendAsync<IPLocateResponse>(request);
		}

		/// <summary>
		/// Asynchronously retrieves location information for the specified IP address at a given date and time.
		/// </summary>
		/// <remarks>This method sends a GET request to the lookup service and returns the response as an <see
		/// cref="IPLocateResponse"/>. The date is formatted as YYYY-MM-DD in the request.</remarks>
		/// <param name="ipAddress">The IP address to look up. This parameter must not be null, empty, or contain only whitespace, and must be in a
		/// valid IP address format.</param>
		/// <param name="At">The date at which to retrieve the location information. </param>
		/// <returns>The result contains the location information for the
		/// specified IP address at the given date and time.</returns>
		/// <exception cref="ArgumentException">Thrown if <paramref name="ipAddress"/> is null, empty, contains only whitespace, or is not a valid IP address
		/// format.</exception>
		public async Task<IPLocateResponse> LookupAtAsync(string ipAddress, DateTime At)
		{
			if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var ip))
			{
				throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddress));
			}
			var path = GetUriAbsolutePath($"lookup/{ip}");
			var request = new HttpRequestMessage(HttpMethod.Get, $"{path}?at={At:yyyy-MM-dd}");
			return await SendAsync<IPLocateResponse>(request);
		}

		/// <summary>
		/// Retrieves information about the current IP address asynchronously.
		/// </summary>
		/// <remarks>This method sends an HTTP GET request to the configured endpoint to obtain IP location data.
		/// Ensure that network connectivity is available and the endpoint is accessible to avoid exceptions.</remarks>
		/// <returns>The result contains an <see cref="IPLocateResponse"/> with
		/// details about the current IP address, including location and related data.</returns>
		public async Task<IPLocateResponse> LookupCurrentIpAsync()
		{
			var request = new HttpRequestMessage(HttpMethod.Get, GetUriAbsolutePath());
			return await SendAsync<IPLocateResponse>(request);
		}

		/// <summary>
		/// Asynchronously retrieves location information for a batch of IP addresses.
		/// </summary>
		/// <remarks>This method sends a batch request to the location service and handles the responses for each IP
		/// address, returning both successful results and errors in a structured format.</remarks>
		/// <param name="ipList">The collection of IP addresses to look up. Must not be null or empty.</param>
		/// <returns>A dictionary where each key is an IP address and the value is a tuple containing the location response and any
		/// associated error information.</returns>
		/// <exception cref="ArgumentException">Thrown if <paramref name="ipList"/> is null or empty.</exception>
		public async Task<Dictionary<string, (IPLocateResponse result, ErrorResponse error)>> BatchLookupAsync(IEnumerable<string> ipList)
		{
			var dict = new Dictionary<string, (IPLocateResponse result, ErrorResponse error)>();
			if (ipList is null || !ipList.Any())
			{
				throw new ArgumentException("IP address batch cannot be null or empty", nameof(ipList));
			}
			var payload = JsonConvert.SerializeObject(ipList);
			var request = new HttpRequestMessage(HttpMethod.Post, GetUriAbsolutePath("batch"))
			{
				Content = new StringContent(payload, Encoding.UTF8, "application/json")
			};

			var dictResponse = await SendAsync<Dictionary<string, JObject>>(request);

			foreach (var item in dictResponse)
			{
				var obj = item.Value;
				try
				{
					if (obj["ip"] != null)
					{
						var result = JsonConvert.DeserializeObject<IPLocateResponse>(item.Value.ToString());
						dict.Add(item.Key, (result, null));
					}
					else if (obj["error"] != null)
					{
						var error = JsonConvert.DeserializeObject<ErrorResponse>(item.Value.ToString());
						dict.Add(item.Key, (null, error));
					}
					else
					{
						dict.Add(item.Key, (null, null)); // Unknown type
					}
				}
				catch (JsonException)
				{
					var error = new ErrorResponse
					{
						Error = "Unable to parse response. Raw response:" + item.Value
					};
					dict.Add(item.Key, (null, error));
				}
			}
			return dict;
		}

		private async Task<T> SendAsync<T>(HttpRequestMessage request)
		{
			try
			{
				var response = await _client.SendAsync(request);
				if (response.IsSuccessStatusCode)
				{
					var responseBody = await response.Content.ReadAsStringAsync();
					try
					{
						return JsonConvert.DeserializeObject<T>(responseBody);
					}
					catch (JsonException ex)
					{
						throw new IPLocateServiceException("Failed to parse IPLocate API response: " + ex.Message, response.StatusCode, ex);
					}
				}
				else
				{
					await HandleErrorResponse(response);
					// Should not be reached as handleErrorResponse is always thrown
					throw new IPLocateServiceException("Unexpected state after error handling.", response.StatusCode);
				}
			}
			catch (HttpRequestException ex)
			{
				throw new IPLocateServiceException($"Network error or problem reaching IPLocate API: {ex.Message}", HttpStatusCode.ServiceUnavailable, ex);
			}
		}

		private async Task HandleErrorResponse(HttpResponseMessage response)
		{
			var statusCode = response.StatusCode;
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

			if (statusCode >= HttpStatusCode.BadRequest && statusCode < HttpStatusCode.InternalServerError)
			{
				var responseBody = await response.Content.ReadAsStringAsync();
				try
				{
					var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
					var errorMessage = errorResponse?.Error ?? "Unknown error";

					throw statusCode switch
					{
						HttpStatusCode.TooManyRequests => new IPLocateRateLimitException(errorMessage),
						HttpStatusCode.BadRequest => new IPLocateInvalidRequestException(errorMessage),
						HttpStatusCode.NotFound => new IPLocateNotFoundException(errorMessage),
						HttpStatusCode.Unauthorized => new IPLocateApiKeyException(errorMessage),
						_ => new IPLocateApiException($"API error: {errorMessage}", statusCode),
					};
				}
				catch (JsonException)
				{
					string baseMessage = "API request failed with status code " + statusCode +
										  ". Unable to parse error response. Raw error: " + errorBodyString;
					throw statusCode switch
					{
						HttpStatusCode.TooManyRequests => new IPLocateRateLimitException(baseMessage),
						HttpStatusCode.BadRequest => new IPLocateInvalidRequestException(baseMessage),
						HttpStatusCode.NotFound => new IPLocateNotFoundException(baseMessage),
						HttpStatusCode.Unauthorized => new IPLocateApiKeyException(baseMessage),
						_ => new IPLocateApiException($"API error: {baseMessage}", statusCode),
					};
				}
				throw new IPLocateServiceException($"Authentication failed with status code {statusCode}. Response body: {errorBodyString}", statusCode);
			}
			else if (statusCode >= HttpStatusCode.InternalServerError)
			{
				throw new IPLocateServiceException($"Server error: {errorBodyString}", statusCode);
			}
			else
			{
				throw new IPLocateApiException($"Unexpected  Http status code: {statusCode}. Response: {errorBodyString}", statusCode);
			}
		}

		private string GetUriAbsolutePath(string pathSegment = null)
		{
			var lookupPath = string.IsNullOrWhiteSpace(pathSegment) ? "api/lookup" : $"api/{pathSegment}";
			if (!Uri.TryCreate(_client.BaseAddress, lookupPath, out var url))
			{
				throw new ArgumentException("Failed to create valid URL for IPLocate API request.");
			}
			return url.AbsolutePath;
		}
	}
}
