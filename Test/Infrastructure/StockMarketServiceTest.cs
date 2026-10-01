using BudgetTracker.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;

namespace Test.Infrastructure;

[TestFixture]
public class StockMarketServiceTest
{
    private static StockMarketService BuildService(HttpMessageHandler handler)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["BrApi:Url"]).Returns("https://brapi.dev/api/");
        config.Setup(c => c["BrApi:Key"]).Returns("test-key");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        return new StockMarketService(httpClientFactory.Object, config.Object, NullLogger<StockMarketService>.Instance);
    }

    private static HttpMessageHandler RespondWith(HttpStatusCode status, string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status) { Content = new StringContent(json) });
        return handler.Object;
    }

    [Test]
    public async Task GetStockByTickerAsync_ValidPrice_ReturnsQuote()
    {
        var service = BuildService(RespondWith(HttpStatusCode.OK, """{"results":[{"symbol":"PETR4","regularMarketPrice":40.5}]}"""));

        var result = await service.GetStockByTickerAsync(["PETR4"]);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Ticker, Is.EqualTo("PETR4"));
        Assert.That(result[0].PriceMarket, Is.EqualTo(40.5m));
    }

    [Test]
    public async Task GetStockByTickerAsync_ErrorStatus_ReturnsEmptyWithoutThrowing()
    {
        var service = BuildService(RespondWith(HttpStatusCode.TooManyRequests, "{}"));

        var result = await service.GetStockByTickerAsync(["PETR4"]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetStockByTickerAsync_NetworkFailure_ReturnsEmptyWithoutThrowing()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));
        var service = BuildService(handler.Object);

        var result = await service.GetStockByTickerAsync(["PETR4"]);

        Assert.That(result, Is.Empty);
    }
}
