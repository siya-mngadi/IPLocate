# IPLocate Geolocation Client for C#

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

[![NuGet](https://img.shields.io/nuget/v/IPLocateIO.Client.svg?style=flat-square)](https://www.nuget.org/packages/IPLocateIO.Client/)

A C# client for the [IPLocate.io](https://iplocate.io) geolocation API. Look up detailed geolocation and threat intelligence data for any IP address:

- **IP geolocation**: IP to country, IP to city, IP to region/state, coordinates, timezone, postal code
- **ASN information**: Internet service provider, network details, routing information
- **Privacy & threat detection**: VPN, proxy, Tor, hosting provider detection
- **Company information**: Business details associated with IP addresses - company name, domain, type (ISP/hosting/education/government/business)
- **Abuse contact**: Network abuse reporting information
- **Hosting detection**: Cloud provider and hosting service detection using our proprietary hosting detection engine

See what information we can provide for [your IP address](https://iplocate.io/what-is-my-ip).

## Getting started

You can make 1,000 free requests per day with a [free account](https://iplocate.io/signup). For higher plans, check out [API pricing](https://www.iplocate.io/pricing).

## Requirements

- ✅ .NET 6.0+
- ✅ .NET 7.0
- ✅ .NET 8.0
- ✅ .NET Standard 2.1
- API Key from [IPLocate.io](https://iplocate.io/signup)

> [!NOTE]
> ❌ Not compatible with .NET Framework (which only supports .NET Standard 2.0)

## Installation

### Using .NET CLI

```bash
dotnet add package IPLocateIO.Client
```

### Using NuGet Package Manager

```bash
NuGet\Install-Package IPLocateIO.Client
```

### Using Paket CLI

```bash
paket add IPLocateIO.Client
```

## Authentication

Get your free API key from [IPLocate.io](https://iplocate.io/signup), and pass it to the `IpLocateClientFactory.Client` method:

```csharp
IPLocateClient client = IPLocateClientFactory.Client("YOUR_API_KEY");
```

### Quick start

```csharp
using IPLocate;

var client = IPLocateClientFactory.Client("YOUR_API_KEY");
var result = await client.LookupCurrentIpAsync();

Console.WriteLine($"IP: {result.Ip}, Country: {result.Country}");
```

### Dependency Injection

```csharp
services.AddHttpClient<IPLocateClient>((sp, http) =>
{
	var opts = sp.GetRequiredService<IOptions<MyApiOptions>>();
	http.BaseAddress = new Uri(opts.Value.BaseUrl);
	http.DefaultRequestHeaders.Add("X-Api-Key", opts.Value.ApiKey);
	http.DefaultRequestHeaders.Add("Accept", "application/json");
	http.DefaultRequestHeaders.Add("User-Agent", "IPLocateClient/1.0.0");
});
```

## Caching

### Quick start

```csharp
using IPLocate;

var client = IPLocateClientFactory.Client(apiKey:"YOUR_API_KEY", cacheDuration: TimeSpan.FromSeconds(15));
var result = await client.LookupCurrentIpAsync();

Console.WriteLine($"IP: {result.Ip}, Country: {result.Country}");
```

### Dependency Injection

```csharp
public static void AddIPLocateClient(this IServiceCollection services)
{
	services.AddHttpClient<IPLocateClient>((sp, http) =>
	{
		var opts = sp.GetRequiredService<IOptions<MyApiOptions>>();
		http.BaseAddress = new Uri(opts.Value.BaseUrl);
		http.DefaultRequestHeaders.Add("X-Api-Key", opts.Value.ApiKey);
		http.DefaultRequestHeaders.Add("Accept", "application/json");
		http.DefaultRequestHeaders.Add("User-Agent", "IPLocateClient/1.0.0");
	}).AddHttpMessageHandler((sp) =>
	{
		var opts = sp.GetRequiredService<IOptions<MyApiOptions>>();
		return new CacheDelegatingHandler(opts.Value.cacheDuration);
	});
}
```

## API reference

For complete API documentation, visit [iplocate.io/docs](https://iplocate.io/docs).

## License

This project is licensed under the MIT License - see the `LICENSE` file for details

## Testing

To run tests for this C# library:

```cmd
dotnet test
```
