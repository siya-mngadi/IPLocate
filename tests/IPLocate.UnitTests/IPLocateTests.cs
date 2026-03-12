using FluentAssertions;
using IPLocate;
using IPLocate.Exceptions;
using IPLocate.Models;
using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IPLocateTests
{
	public class IPLocateClientTests : IDisposable
	{
		private readonly WireMockServer _server;

		private readonly string HEADER_API_KEY = "X-API-Key";
		public IPLocateClientTests()
		{
			_server = WireMockServer.Start(8010);
		}

		private string GetTestApiBaseUrl() => $"http://localhost:{_server.Port}/api";

		[Fact]
		public async Task Successful_lookup()
		{
			var apiKey = "test-key";
			var ip = "8.8.8.8";

			var json = """
        {
          "ip": "8.8.8.8",
          "country": "United States",
          "country_code": "US",
          "is_eu": false,
          "city": "Mountain View",
          "asn": {
            "asn": "AS15169"
          },
          "privacy": {
            "is_hosting": true
          }
        }
        """;

			_server.Given(
				Request.Create()
					.WithPath($"/api/lookup/{ip}")
					.WithHeader(HEADER_API_KEY, apiKey)
					.UsingGet()
			)
			.RespondWith(
				Response.Create()
					.WithStatusCode(200)
					.WithHeader("Content-Type", "application/json")
					.WithBody(json.Trim())
			);

			var client = IPLocateClientFactory.Client(apiKey, GetTestApiBaseUrl());
			var response = await client.LookupAsync(ip);

			response.Should().NotBeNull();
			response.Ip.Should().Be("8.8.8.8");
			response.Country.Should().Be("United States");
			response.CountryCode.Should().Be("US");
			response.Asn.Asn.Should().Be("AS15169");
			response.Privacy.Hosting.Should().BeTrue();
		}

		[Fact]
		public async Task Successful_current_ip_lookup()
		{
			var apiKey = "current-ip-key";

			var jsonObj = new
			{
				ip = "1.2.3.4",
				country = "Somewhere",
				country_code = "SW"
			};

			_server.Given(
				Request.Create()
					.WithPath($"/api/lookup")
					.WithHeader(HEADER_API_KEY, apiKey)
					.UsingGet()
			)
			.RespondWith(
				Response.Create()
					.WithStatusCode(200)
					.WithHeader("Content-Type", "application/json")
					.WithBodyAsJson(jsonObj)
			);

			var client = IPLocateClientFactory.Client(apiKey, GetTestApiBaseUrl());
			var response = await client.LookupCurrentIpAsync();

			response.Ip.Should().Be("1.2.3.4");
			response.Country.Should().Be("Somewhere");
		}

		[Fact]
		public async Task Unauthorized_error_throws_api_key_exception()
		{
			var apiKey = "invalid-key";
			var ip = "1.1.1.1";

			_server.Given(
				Request.Create()
					.WithPath($"/api/lookup/{ip}")
					.WithHeader(HEADER_API_KEY, apiKey)
					.UsingGet()
			)
			.RespondWith(
				Response.Create()
					.WithStatusCode(HttpStatusCode.Unauthorized)
					.WithBodyAsJson(new { error = "Unknown token" })
			);

			var client = IPLocateClientFactory.Client(apiKey, GetTestApiBaseUrl());
			await Assert.ThrowsAsync<IPLocateApiKeyException>(async () => await client.LookupAsync(ip));
		}

		[Fact]
		public async Task Rate_limit_error()
		{
			var apiKey = "rate-limited-key";
			var ip = "1.1.1.1";

			_server.Given(
				Request.Create()
					.WithPath($"/api/lookup/{ip}")
					.WithHeader(HEADER_API_KEY, apiKey)
					.UsingGet()
			)
			.RespondWith(
				Response.Create()
					.WithStatusCode(429)
					.WithBody("{\"error\":\"Rate limit exceeded\"}")
			);

			var client = IPLocateClientFactory.Client(apiKey, GetTestApiBaseUrl());
			await Assert.ThrowsAsync<IPLocateRateLimitException>(async () => await client.LookupAsync(ip));
		}

		[Fact]
		public async Task Server_error_throws_service_exception()
		{
			var apiKey = "any-key";
			var ip = "1.2.3.4";

			_server.Given(
				Request.Create()
					.WithPath($"/api/lookup/{ip}")
					.WithHeader(HEADER_API_KEY, apiKey)
					.UsingGet()
			)
			.RespondWith(
				Response.Create()
					.WithStatusCode(500)
					.WithBody("Internal server error")
			);

			var client = IPLocateClientFactory.Client(apiKey, GetTestApiBaseUrl());

			var ex = await Assert.ThrowsAsync<IPLocateServiceException>(async () => await client.LookupAsync(ip));
			ex.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
			ex.Message.Should().Contain("Internal server error");
		}

		[Fact]
		public void Null_api_key_throws()
		{
			Assert.Throws<ArgumentException>(() => IPLocateClientFactory.Client(null));
		}

		[Fact]
		public void Empty_api_key_throws()
		{
			Assert.Throws<ArgumentException>(() => IPLocateClientFactory.Client(string.Empty));
		}

		[Fact]
		public async Task Successful_batch_ip_list_lookup()
		{
			var apiKey = "current-ip-key";

			string[] ipList = [ "8.8.8.8", "1.1.1.1", "2001:4860:4860::8888"];

			var jsonObj = new Dictionary<string, IPLocateResponse>
			{
				{ "8.8.8.8", new IPLocateResponse { Ip = "8.8.8.8", Country = "Ireland", CountryCode="IE" }},
				{ "1.1.1.1", new IPLocateResponse { Ip = "1.1.1.1", Country = "United Kingdom", CountryCode="UK" } },
				{ "2001:4860:4860::8888", new IPLocateResponse { Ip = "2001:4860:4860:0000:0000:0000:0000:8888", Country = "United States", CountryCode = "US" }}
			};

			_server.Given(
				Request.Create()
					.WithPath($"/api/batch")
					.WithHeader(HEADER_API_KEY, apiKey)
					.UsingPost()
					.WithBodyAsJson(ipList)
			)
			.RespondWith(
				Response.Create()
					.WithStatusCode(200)
					.WithHeader("Content-Type", "application/json")
					.WithBodyAsJson(jsonObj)
			);

			var client = IPLocateClientFactory.Client(apiKey, GetTestApiBaseUrl());
			var response = await client.BatchLookupAsync(ipList);

			response.Should().NotBeNull();
			response.Should().BeOfType<Dictionary<string, (IPLocateResponse Result, ErrorResponse Error)>>();

			// 8.8.8.8
			var Ip1 = response["8.8.8.8"];
			Ip1.Should().NotBeNull();
			Ip1.Error.Should().BeNull();
			Ip1.Result.Should().NotBeNull();
			Ip1.Result.Country.Should().Be("Ireland");
			Ip1.Result.CountryCode.Should().Be("IE");

			// 1.1.1.1
			var Ip2 = response["1.1.1.1"];
			Ip2.Should().NotBeNull();
			Ip2.Error.Should().BeNull();
			Ip2.Result.Should().NotBeNull();
			Ip2.Result.Country.Should().Be("United Kingdom");
			Ip2.Result.CountryCode.Should().Be("UK");

			// 2001:4860:4860:0000:0000:0000:0000:8888
			var Ip3 = response["2001:4860:4860::8888"];
			Ip3.Error.Should().BeNull();
			Ip3.Result.Should().NotBeNull();
			Ip3.Result.Country.Should().Be("United States");
			Ip3.Result.CountryCode.Should().Be("US");	
		}

		[Fact]
		public async Task Batch_ip_list_lookup_with_an_invalid_ip()
		{
			var apiKey = "current-ip-key";

			string[] ipList = ["invalid-ip", "1.1.1.1"];

			var jsonObj = new Dictionary<string, object>
				{
				  ["invalid-ip"] = new {
				   error = "invalid ip"
				  },
				  ["1.1.1.1"] = new {
				    ip = "1.1.1.1",
				    country = "United Kingdom",
				    country_code = "UK",
				  }
				};

			_server.Given(
				Request.Create()
					.WithPath($"/api/batch")
					.WithHeader(HEADER_API_KEY, apiKey)
					.UsingPost()
					.WithBodyAsJson(ipList)
			)
			.RespondWith(
				Response.Create()
					.WithStatusCode(200)
					.WithHeader("Content-Type", "application/json")
					.WithBodyAsJson(jsonObj)
			);

			var client = IPLocateClientFactory.Client(apiKey, GetTestApiBaseUrl());
			var response = await client.BatchLookupAsync(ipList);

			response.Should().NotBeNull();
			response.Should().BeOfType<Dictionary<string, (IPLocateResponse Result, ErrorResponse Error)>>();

			// invalid-ip
			var Ip1 = response["invalid-ip"];
			Ip1.Should().NotBeNull();
			Ip1.Error.Should().NotBeNull();
			Ip1.Result.Should().BeNull();
			Ip1.Error.Error.Should().Be("invalid ip");

			// 1.1.1.1
			var Ip2 = response["1.1.1.1"];
			Ip2.Should().NotBeNull();
			Ip2.Error.Should().BeNull();
			Ip2.Result.Should().NotBeNull();
			Ip2.Result.Country.Should().Be("United Kingdom");
			Ip2.Result.CountryCode.Should().Be("UK");
		}

		[Fact]
		public async Task Successful_historical_ip_lookup()
		{
			var apiKey = "test-key";
			var ip = "8.8.8.8";

			var at = $"{DateTime.Now.AddMonths(-1):yyyy-MM-dd}";

			var json = """
        {
          "ip": "8.8.8.8",
          "country": "United States",
          "country_code": "US",
          "is_eu": false,
          "city": "Mountain View",
          "asn": {
            "asn": "AS15169"
          },
          "privacy": {
            "is_hosting": true
          }
        }
        """;

			_server.Given(
				Request.Create()
					.WithPath($"/api/lookup/{ip}")
					.WithParam("at", at)
					.WithHeader(HEADER_API_KEY, apiKey)
					.UsingGet()
			)
			.RespondWith(
				Response.Create()
					.WithStatusCode(200)
					.WithHeader("Content-Type", "application/json")
					.WithBody(json.Trim())
			);

			var client = IPLocateClientFactory.Client(apiKey, GetTestApiBaseUrl());
			var response = await client.LookupAtAsync(ip, DateTime.Parse(at));

			response.Should().NotBeNull();
			response.Ip.Should().Be("8.8.8.8");
			response.Country.Should().Be("United States");
			response.CountryCode.Should().Be("US");
			response.Asn.Asn.Should().Be("AS15169");
			response.Privacy.Hosting.Should().BeTrue();
		}


		public void Dispose()
		{
			_server.Stop();
			_server.Dispose();
		}
	}
}
