using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
	[TestClass]
	public class AmtrakerPassengerProviderClientTests
	{
		[DataTestMethod]
		[DataRow("N", "Northbound")]
		[DataRow("S", "Southbound")]
		[DataRow("E", "Eastbound")]
		[DataRow("W", "Westbound")]
		[DataRow("NE", "Northeastbound")]
		[DataRow("NW", "Northwestbound")]
		[DataRow("SE", "Southeastbound")]
		[DataRow("SW", "Southwestbound")]
		public async Task GetTrainsAsync_TranslatesRequestedHeadingValues(string rawHeading, string expectedHeading)
		{
			const string trainNumber = "8";
			var content = $$"""
				{
				  "{{trainNumber}}": [
					{
					  "provider": "Amtrak",
					  "routeName": "Hiawatha",
					  "trainNum": "{{trainNumber}}",
					  "trainID": "{{trainNumber}}-A",
					  "heading": "{{rawHeading}}",
					  "lat": 43.1,
					  "lon": -88.1,
					  "velocity": 55,
					  "updatedAt": "2026-06-11T12:34:56Z"
					}
				  ]
				}
				""";

			using var httpClient = new HttpClient(new StaticJsonHttpMessageHandler(content))
			{
				BaseAddress = new Uri("https://example.test/")
			};

			var logger = new Mock<ILogger<AmtrakerPassengerProviderClient>>();
			var client = new AmtrakerPassengerProviderClient(httpClient, logger.Object);

			var snapshots = await client.GetTrainsAsync(trainNumber, CancellationToken.None);
			var snapshot = snapshots.Single();

			Assert.AreEqual(expectedHeading, snapshot.Heading);
		}

		[TestMethod]
		public async Task GetTrainsAsync_ReturnsSnapshots_WhenResponseRootIsArray()
		{
			const string trainNumber = "8";
			var content = """
				[
				  {
					"provider": "Amtrak",
					"routeName": "Hiawatha",
					"trainNum": "8",
					"trainID": "8-A",
					"heading": "N",
					"lat": 43.1,
					"lon": -88.1,
					"velocity": 55,
					"updatedAt": "2026-06-11T12:34:56Z"
				  }
				]
				""";

			using var httpClient = new HttpClient(new StaticJsonHttpMessageHandler(content))
			{
				BaseAddress = new Uri("https://example.test/")
			};

			var logger = new Mock<ILogger<AmtrakerPassengerProviderClient>>();
			var client = new AmtrakerPassengerProviderClient(httpClient, logger.Object);

			var snapshots = (await client.GetTrainsAsync(trainNumber, CancellationToken.None)).ToList();

			Assert.AreEqual(1, snapshots.Count);
			Assert.AreEqual("8-A", snapshots[0].TrainId);
			Assert.AreEqual("Northbound", snapshots[0].Heading);
		}

		[TestMethod]
		public async Task GetTrainsAsync_ReturnsEmpty_WhenResponseRootTypeIsUnexpected()
		{
			const string trainNumber = "8";
			const string content = "\"unexpected\"";

			using var httpClient = new HttpClient(new StaticJsonHttpMessageHandler(content))
			{
				BaseAddress = new Uri("https://example.test/")
			};

			var logger = new Mock<ILogger<AmtrakerPassengerProviderClient>>();
			var client = new AmtrakerPassengerProviderClient(httpClient, logger.Object);

			var snapshots = await client.GetTrainsAsync(trainNumber, CancellationToken.None);

			Assert.AreEqual(0, snapshots.Count());
		}

		private sealed class StaticJsonHttpMessageHandler(string json) : HttpMessageHandler
		{
			private readonly string _json = json;

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				var response = new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(_json, Encoding.UTF8, "application/json")
				};

				return Task.FromResult(response);
			}
		}
	}
}
