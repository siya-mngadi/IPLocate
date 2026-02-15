using IPLocate.cache;
using Moq;
using Moq.Protected;
using System.Net;

namespace IPLocateTests;

public class CacheDelegatingHandlerTests
{
	private HttpClient CreateHttpClient(HttpResponseMessage response, TimeSpan expiration)
	{
		var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

		mockHandler.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>()
			)
			.ReturnsAsync(response);

		var cachingHandler = new CacheDelegatingHandler(expiration)
		{
			InnerHandler = mockHandler.Object
		};

		return new HttpClient(cachingHandler);
	}

	[Fact]
	public async Task ReturnsCachedResponse_OnSecondCall()
	{
		// Arrange
		var response = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("Hello Universe")
		};

		var client = CreateHttpClient(response, TimeSpan.FromMinutes(5));

		var request1 = new HttpRequestMessage(HttpMethod.Get, "http://test.com");
		var request2 = new HttpRequestMessage(HttpMethod.Get, "http://test.com");

		// Act
		var firstResponse = await client.SendAsync(request1);
		var secondResponse = await client.SendAsync(request2);

		var firstContent = await firstResponse.Content.ReadAsStringAsync();
		var secondContent = await secondResponse.Content.ReadAsStringAsync();

		// Assert
		Assert.Equal("Hello Universe", firstContent);
		Assert.Equal("Hello Universe", secondContent);
		Assert.NotSame(firstResponse, secondResponse);
	}

	[Fact]
	public async Task ExpiredCache_IsReplaced()
	{
		// Arrange
		var response1 = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("First Hello World")
		};

		var response2 = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("Second Hello World")
		};

		var mockHandler = new Mock<HttpMessageHandler>();
		var callCount = 0;

		mockHandler.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>()
			)
			.ReturnsAsync(() =>
			{
				callCount++;
				return callCount == 1 ? response1 : response2;
			});

		var cachingHandler = new CacheDelegatingHandler(TimeSpan.FromMilliseconds(50))
		{
			InnerHandler = mockHandler.Object
		};

		var client = new HttpClient(cachingHandler);
		var request1 = new HttpRequestMessage(HttpMethod.Get, "http://test.com");
		var request2 = new HttpRequestMessage(HttpMethod.Get, "http://test.com");

		// Act
		var firstResponse = await client.SendAsync(request1);
		await Task.Delay(100); // expire cache
		var secondResponse = await client.SendAsync(request2);

		var firstContent = await firstResponse.Content.ReadAsStringAsync();
		var secondContent = await secondResponse.Content.ReadAsStringAsync();

		// Assert
		Assert.Equal("First Hello World", firstContent);
		Assert.Equal("Second Hello World", secondContent);
	}

	[Fact]
	public void Constructor_ThrowsOnNonPositiveExpiration()
	{
		// Arrange & Act & Assert
		Assert.Throws<ArgumentOutOfRangeException>(() => new CacheDelegatingHandler(TimeSpan.Zero));
		Assert.Throws<ArgumentOutOfRangeException>(() => new CacheDelegatingHandler(TimeSpan.FromMilliseconds(-1)));
	}
}
